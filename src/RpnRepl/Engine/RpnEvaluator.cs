using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using MiniRepl.Rpn.Diagnostics;
using MiniRepl.Rpn.Lexing;
using MiniRepl.Rpn.Memory;
using MiniRepl.Rpn.Types;

namespace MiniRepl.Rpn.Engine;

/// <summary>
/// Motor de evaluación en Notación Polaca Inversa (RPN).
/// Es intencionalmente sin estado mutable interno: recibe explícitamente la pila
/// (<see cref="EvaluationStack"/>), el entorno de símbolos (<see cref="SymbolEnvironment"/>)
/// y la configuración (<see cref="EvalOptions"/>) en cada subrutina para demostrar
/// paso por referencia de objeto, paso de <c>struct</c> con <c>in</c>, <c>ref</c> y <c>out</c>.
/// </summary>
public sealed class RpnEvaluator(IOperationRegistry operations)
{
    private readonly IOperationRegistry _operations = operations ?? throw new ArgumentNullException(nameof(operations));

    public RpnEvaluator()
        : this(new OperationRegistry())
    {
    }

    public EvalResult<EvaluationReport> EvaluateLine(
        string line,
        EvaluationStack stack,
        SymbolEnvironment environment,
        in EvalOptions options)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(environment);

        EvalResult<IReadOnlyList<Token>> lexResult = SpanLexer.Tokenize(line);
        if (!lexResult.TryGetValue(out IReadOnlyList<Token>? tokens, out RpnError? lexError))
        {
            return EvalResult<EvaluationReport>.Fail(lexError);
        }

        if (tokens.Count == 0)
        {
            return EvalResult<EvaluationReport>.Ok(new EvaluationReport(
                TopValue: ExtractTopValue(stack),
                ProducedStackOutput: false,
                StackCount: stack.Count,
                ScopeDepth: environment.CurrentDepth,
                Trace: Array.Empty<TraceStep>(),
                Events: Array.Empty<string>()));
        }

        StackSnapshot stackBackup = stack.CaptureSnapshot();
        EnvironmentSnapshot envBackup = environment.CaptureSnapshot();

        var trace = options.CaptureTrace ? new List<TraceStep>(tokens.Count) : null;
        var events = new List<string>();
        int stepCounter = 0;
        bool producedStackOutput = false;

        foreach (Token token in tokens)
        {
            EvalResult<Unit> stepResult = ExecuteToken(
                token,
                stack,
                environment,
                ref stepCounter,
                ref producedStackOutput,
                trace,
                events);

            if (!stepResult.TryGetValue(out _, out RpnError? runtimeError))
            {
                if (options.TransactionalRollback)
                {
                    stack.RestoreSnapshot(in stackBackup);
                    environment.RestoreSnapshot(in envBackup);
                }

                return EvalResult<EvaluationReport>.Fail(runtimeError);
            }
        }

        // Ningún identificador sin enlazar puede quedar colgando en la pila sin ser consumido por '='
        if (FindDanglingUndefinedSymbol(stack, out Token.Identifier? unresolved, out SourceSpan badSpan))
        {
            if (options.TransactionalRollback)
            {
                stack.RestoreSnapshot(in stackBackup);
                environment.RestoreSnapshot(in envBackup);
            }

            return EvalResult<EvaluationReport>.Fail(
                new RpnError.UndefinedVariable(unresolved.Name, badSpan));
        }

        return EvalResult<EvaluationReport>.Ok(new EvaluationReport(
            TopValue: ExtractTopValue(stack),
            ProducedStackOutput: producedStackOutput && !stack.IsEmpty,
            StackCount: stack.Count,
            ScopeDepth: environment.CurrentDepth,
            Trace: trace?.AsReadOnly() ?? (IReadOnlyList<TraceStep>)Array.Empty<TraceStep>(),
            Events: events.AsReadOnly()));
    }

    private EvalResult<Unit> ExecuteToken(
        Token token,
        EvaluationStack stack,
        SymbolEnvironment environment,
        ref int stepCounter,
        ref bool producedStackOutput,
        IList<TraceStep>? trace,
        IList<string> events)
    {
        stepCounter++;
        string lexemeForTrace;
        string actionDescription;

        switch (token)
        {
            case Token.IntLiteral(var value, _):
            {
                var rpnVal = new RpnValue.IntValue(value);
                stack.Push(new StackSlot.ValueSlot(rpnVal));
                producedStackOutput = true;
                lexemeForTrace = rpnVal.FormatValue();
                actionDescription = $"push {rpnVal}";
                break;
            }

            case Token.FloatLiteral(var value, _):
            {
                var rpnVal = new RpnValue.FloatValue(value);
                stack.Push(new StackSlot.ValueSlot(rpnVal));
                producedStackOutput = true;
                lexemeForTrace = rpnVal.FormatValue();
                actionDescription = $"push {rpnVal}";
                break;
            }

            case Token.BoolLiteral(var value, _):
            {
                var rpnVal = new RpnValue.BoolValue(value);
                stack.Push(new StackSlot.ValueSlot(rpnVal));
                producedStackOutput = true;
                lexemeForTrace = rpnVal.FormatValue();
                actionDescription = $"push {rpnVal}";
                break;
            }

            case Token.Identifier(var name, var span):
            {
                bool found = environment.TryLookup(name, out RpnValue? existing, out int depth);
                stack.Push(new StackSlot.SymbolSlot(name, existing, found ? depth : null, span));
                producedStackOutput = true;
                lexemeForTrace = name;
                actionDescription = found
                    ? $"push símbolo '{name}' -> {existing} (@ nivel {depth})"
                    : $"push L-value '{name}' (sin enlazar)";
                break;
            }

            case Token.Operator opToken:
            {
                EvalResult<RpnValue> opResult = ExecuteOperator(opToken, stack, environment);
                if (!opResult.TryGetValue(out RpnValue? produced, out RpnError? opError))
                {
                    return EvalResult<Unit>.Fail(opError);
                }

                producedStackOutput = true;
                lexemeForTrace = opToken.Symbol;
                actionDescription = $"op '{opToken.Symbol}' -> {produced}";
                break;
            }

            case Token.Assignment assignToken:
            {
                EvalResult<string> assignResult = ExecuteAssignment(assignToken, stack, environment);
                if (!assignResult.TryGetValue(out string? eventText, out RpnError? assignError))
                {
                    return EvalResult<Unit>.Fail(assignError);
                }

                producedStackOutput = false;
                events.Add(eventText);
                lexemeForTrace = "=";
                actionDescription = $"assign {eventText}";
                break;
            }

            case Token.BlockCommand blockToken:
            {
                EvalResult<string> blockResult = ExecuteBlockCommand(blockToken, environment);
                if (!blockResult.TryGetValue(out string? blockEvent, out RpnError? blockError))
                {
                    return EvalResult<Unit>.Fail(blockError);
                }

                events.Add(blockEvent);
                lexemeForTrace = blockToken.Kind == BlockKind.Begin ? "begin" : "end";
                actionDescription = blockEvent;
                break;
            }

            default:
                throw new UnreachableException();
        }

        if (trace is not null)
        {
            trace.Add(new TraceStep(
                Step: stepCounter,
                Lexeme: lexemeForTrace,
                Action: actionDescription,
                ScopeDepth: environment.CurrentDepth,
                StackSnapshot: stack.Items.Select(slot => slot.ToString()).ToArray()));
        }

        return EvalResult<Unit>.Ok(Unit.Value);
    }

    private EvalResult<RpnValue> ExecuteOperator(
        Token.Operator opToken,
        EvaluationStack stack,
        SymbolEnvironment environment)
    {
        if (!_operations.TryResolve(opToken.Op, out IRpnOperation? operation) || operation is null)
        {
            return EvalResult<RpnValue>.Fail(
                new RpnError.InvalidToken(opToken.Symbol, opToken.Span));
        }

        SourceSpan opSpan = opToken.Span;

        if (!TryPopTwoRValues(
            stack,
            environment,
            opToken.Symbol,
            in opSpan,
            out RpnValue? left,
            out RpnValue? right,
            out RpnError? popError))
        {
            return EvalResult<RpnValue>.Fail(popError);
        }

        EvalResult<RpnValue> applied = operation.Apply(left, right, in opSpan);
        if (!applied.TryGetValue(out RpnValue? resultValue, out RpnError? evalError))
        {
            return EvalResult<RpnValue>.Fail(evalError);
        }

        stack.Push(new StackSlot.ValueSlot(resultValue));
        return EvalResult<RpnValue>.Ok(resultValue);
    }

    private static EvalResult<string> ExecuteAssignment(
        Token.Assignment assignToken,
        EvaluationStack stack,
        SymbolEnvironment environment)
    {
        if (stack.Count < 2)
        {
            return EvalResult<string>.Fail(
                new RpnError.StackUnderflow("=", Required: 2, Available: stack.Count, assignToken.Span));
        }

        stack.TryPop(out StackSlot? rhsSlot);
        stack.TryPop(out StackSlot? lhsSlot);

        // El RHS debe poder resolverse a un R-value válido (puede ser otra variable ya definida)
        if (!TryResolveRValue(rhsSlot!, environment, out RpnValue? rhsValue, out RpnError? rhsError))
        {
            return EvalResult<string>.Fail(rhsError);
        }

        if (lhsSlot is not StackSlot.SymbolSlot targetSymbol)
        {
            string actualDesc = lhsSlot switch
            {
                StackSlot.ValueSlot(var v) => $"literal/valor ({v})",
                _ => "elemento desconocido"
            };
            return EvalResult<string>.Fail(
                new RpnError.InvalidAssignmentTarget(actualDesc, assignToken.Span));
        }

        environment.BindInCurrentScope(targetSymbol.Name, rhsValue);
        return EvalResult<string>.Ok(
            $"{targetSymbol.Name} = {rhsValue} [nivel {environment.CurrentDepth}]");
    }

    private static EvalResult<string> ExecuteBlockCommand(
        Token.BlockCommand blockToken,
        SymbolEnvironment environment)
    {
        if (blockToken.Kind == BlockKind.Begin)
        {
            int newDepth = environment.EnterScope();
            return EvalResult<string>.Ok($"→ Entró a bloque local (nivel {newDepth})");
        }

        if (!environment.TryExitScope(out int depthAfterExit))
        {
            return EvalResult<string>.Fail(new RpnError.UnmatchedEndBlock(blockToken.Span));
        }

        string label = depthAfterExit == 0 ? "global" : $"nivel {depthAfterExit}";
        return EvalResult<string>.Ok($"← Salió de bloque local ({label})");
    }

    private static bool TryPopTwoRValues(
        EvaluationStack stack,
        SymbolEnvironment environment,
        string operationName,
        in SourceSpan span,
        [NotNullWhen(true)] out RpnValue? left,
        [NotNullWhen(true)] out RpnValue? right,
        [NotNullWhen(false)] out RpnError? error)
    {
        if (stack.Count < 2)
        {
            left = null;
            right = null;
            error = new RpnError.StackUnderflow(operationName, Required: 2, Available: stack.Count, span);
            return false;
        }

        stack.TryPop(out StackSlot? rightSlot);
        stack.TryPop(out StackSlot? leftSlot);

        if (!TryResolveRValue(leftSlot!, environment, out left, out error))
        {
            right = null;
            return false;
        }

        if (!TryResolveRValue(rightSlot!, environment, out right, out error))
        {
            left = null;
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryResolveRValue(
        StackSlot slot,
        SymbolEnvironment environment,
        [NotNullWhen(true)] out RpnValue? value,
        [NotNullWhen(false)] out RpnError? error)
    {
        switch (slot)
        {
            case StackSlot.ValueSlot(var v):
                value = v;
                error = null;
                return true;

            case StackSlot.SymbolSlot(var name, var captured, _, var span):
                if (captured is not null)
                {
                    value = captured;
                    error = null;
                    return true;
                }

                // Reintento de búsqueda por si una asignación previa en la misma línea lo enlazó
                if (environment.TryLookup(name, out RpnValue? lateBound, out _))
                {
                    value = lateBound;
                    error = null;
                    return true;
                }

                value = null;
                error = new RpnError.UndefinedVariable(name, span);
                return false;

            default:
                throw new UnreachableException();
        }
    }

    private static bool FindDanglingUndefinedSymbol(
        EvaluationStack stack,
        [NotNullWhen(true)] out Token.Identifier? unresolved,
        out SourceSpan badSpan)
    {
        foreach (StackSlot slot in stack.Items)
        {
            if (slot is StackSlot.SymbolSlot(var name, null, _, var span))
            {
                unresolved = new Token.Identifier(name, span);
                badSpan = span;
                return true;
            }
        }

        unresolved = null;
        badSpan = SourceSpan.Empty;
        return false;
    }

    private static RpnValue? ExtractTopValue(EvaluationStack stack)
    {
        if (!stack.TryPeek(out StackSlot? top))
        {
            return null;
        }

        return top switch
        {
            StackSlot.ValueSlot(var v) => v,
            StackSlot.SymbolSlot(_, var captured, _, _) => captured,
            _ => null
        };
    }
}
