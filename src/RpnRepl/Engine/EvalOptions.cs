using MiniRepl.Rpn.Types;

namespace MiniRepl.Rpn.Engine;

/// <summary>
/// Opciones inmutables por valor (<c>readonly record struct</c>) pasadas mediante <c>in</c>
/// para evitar copias y prohibir mutación dentro de las subrutinas de evaluación.
/// </summary>
public readonly record struct EvalOptions(
    bool CaptureTrace = false,
    bool TransactionalRollback = true);

public sealed record TraceStep(
    int Step,
    string Lexeme,
    string Action,
    int ScopeDepth,
    IReadOnlyList<string> StackSnapshot);

public sealed record EvaluationReport(
    RpnValue? TopValue,
    bool ProducedStackOutput,
    int StackCount,
    int ScopeDepth,
    IReadOnlyList<TraceStep> Trace,
    IReadOnlyList<string> Events);
