using System.Diagnostics;
using MiniRepl.Rpn.Diagnostics;
using MiniRepl.Rpn.Types;

namespace MiniRepl.Rpn.Engine;

/// <summary>
/// Contrato polimórfico (interfaz) para operaciones binarias sobre valores RPN.
/// Demuestra abstracción orientada a objetos mediante interfaces y despacho dinámico.
/// </summary>
public interface IRpnOperation
{
    RpnOperator Operator { get; }
    string Symbol { get; }
    EvalResult<RpnValue> Apply(RpnValue left, RpnValue right, in SourceSpan span);
}

public interface IOperationRegistry
{
    bool TryResolve(RpnOperator op, out IRpnOperation? operation);
}

public sealed class ArithmeticOperation(RpnOperator op, string symbol) : IRpnOperation
{
    public RpnOperator Operator { get; } = op;
    public string Symbol { get; } = symbol;

    public EvalResult<RpnValue> Apply(RpnValue left, RpnValue right, in SourceSpan span)
    {
        EvalResult<NumericOperands> promoted = TypeChecker.PromoteNumericPair(Symbol, left, right, in span);
        if (!promoted.TryGetValue(out NumericOperands? operands, out RpnError? typeError))
        {
            return EvalResult<RpnValue>.Fail(typeError);
        }

        return operands switch
        {
            NumericOperands.IntPair(var a, var b) => ApplyInt(a, b, in span),
            NumericOperands.FloatPair(var a, var b) => ApplyFloat(a, b, in span),
            _ => throw new UnreachableException()
        };
    }

    private EvalResult<RpnValue> ApplyInt(long a, long b, in SourceSpan span)
    {
        if (Operator is RpnOperator.Divide or RpnOperator.Modulo && b == 0)
        {
            return new RpnError.DivisionByZero(Symbol, span);
        }

        try
        {
            checked
            {
                long result = Operator switch
                {
                    RpnOperator.Add => a + b,
                    RpnOperator.Subtract => a - b,
                    RpnOperator.Multiply => a * b,
                    RpnOperator.Divide => a / b,
                    RpnOperator.Modulo => a % b,
                    _ => throw new InvalidOperationException($"Operador no aritmético: {Operator}")
                };

                return new RpnValue.IntValue(result);
            }
        }
        catch (OverflowException)
        {
            return new RpnError.ArithmeticOverflow(
                Symbol,
                $"el resultado entero de ({a} {Symbol} {b}) excede Int64.",
                span);
        }
    }

    private EvalResult<RpnValue> ApplyFloat(double a, double b, in SourceSpan span)
    {
        if (Operator is RpnOperator.Divide or RpnOperator.Modulo && b == 0.0)
        {
            return new RpnError.DivisionByZero(Symbol, span);
        }

        double result = Operator switch
        {
            RpnOperator.Add => a + b,
            RpnOperator.Subtract => a - b,
            RpnOperator.Multiply => a * b,
            RpnOperator.Divide => a / b,
            RpnOperator.Modulo => a % b,
            _ => throw new InvalidOperationException($"Operador no aritmético: {Operator}")
        };

        if (!double.IsFinite(result))
        {
            return new RpnError.ArithmeticOverflow(
                Symbol,
                $"el resultado flotante de ({a} {Symbol} {b}) no es finito (Infinity/NaN).",
                span);
        }

        return new RpnValue.FloatValue(result);
    }
}

public sealed class ComparisonOperation(RpnOperator op, string symbol) : IRpnOperation
{
    public RpnOperator Operator { get; } = op;
    public string Symbol { get; } = symbol;

    public EvalResult<RpnValue> Apply(RpnValue left, RpnValue right, in SourceSpan span)
    {
        if (Operator is RpnOperator.Equal or RpnOperator.NotEqual)
        {
            return ApplyEquality(left, right, in span);
        }

        EvalResult<NumericOperands> promoted = TypeChecker.PromoteNumericPair(Symbol, left, right, in span);
        if (!promoted.TryGetValue(out NumericOperands? operands, out RpnError? typeError))
        {
            return EvalResult<RpnValue>.Fail(typeError);
        }

        bool result = operands switch
        {
            NumericOperands.IntPair(var a, var b) => Operator switch
            {
                RpnOperator.LessThan => a < b,
                RpnOperator.GreaterThan => a > b,
                RpnOperator.LessOrEqual => a <= b,
                RpnOperator.GreaterOrEqual => a >= b,
                _ => throw new UnreachableException()
            },
            NumericOperands.FloatPair(var a, var b) => Operator switch
            {
                RpnOperator.LessThan => a < b,
                RpnOperator.GreaterThan => a > b,
                RpnOperator.LessOrEqual => a <= b,
                RpnOperator.GreaterOrEqual => a >= b,
                _ => throw new UnreachableException()
            },
            _ => throw new UnreachableException()
        };

        return new RpnValue.BoolValue(result);
    }

    private EvalResult<RpnValue> ApplyEquality(RpnValue left, RpnValue right, in SourceSpan span)
    {
        EvalResult<bool> compatible = TypeChecker.AreEqualityCompatible(Symbol, left, right, in span);
        if (!compatible.TryGetValue(out _, out RpnError? error))
        {
            return EvalResult<RpnValue>.Fail(error);
        }

        bool isEqual;
        if (left is RpnValue.BoolValue(var b1) && right is RpnValue.BoolValue(var b2))
        {
            isEqual = b1 == b2;
        }
        else
        {
            EvalResult<NumericOperands> promoted = TypeChecker.PromoteNumericPair(Symbol, left, right, in span);
            if (!promoted.TryGetValue(out NumericOperands? operands, out RpnError? numError))
            {
                return EvalResult<RpnValue>.Fail(numError);
            }

            isEqual = operands switch
            {
                NumericOperands.IntPair(var x, var y) => x == y,
                NumericOperands.FloatPair(var x, var y) => x == y,
                _ => throw new UnreachableException()
            };
        }

        return new RpnValue.BoolValue(Operator == RpnOperator.Equal ? isEqual : !isEqual);
    }
}

/// <summary>
/// Registro por composición de operaciones binarias soportadas.
/// </summary>
public sealed class OperationRegistry : IOperationRegistry
{
    private readonly Dictionary<RpnOperator, IRpnOperation> _operations;

    public OperationRegistry()
    {
        IRpnOperation[] builtIns =
        [
            new ArithmeticOperation(RpnOperator.Add, "+"),
            new ArithmeticOperation(RpnOperator.Subtract, "-"),
            new ArithmeticOperation(RpnOperator.Multiply, "*"),
            new ArithmeticOperation(RpnOperator.Divide, "/"),
            new ArithmeticOperation(RpnOperator.Modulo, "%"),
            new ComparisonOperation(RpnOperator.Equal, "=="),
            new ComparisonOperation(RpnOperator.NotEqual, "!="),
            new ComparisonOperation(RpnOperator.LessThan, "<"),
            new ComparisonOperation(RpnOperator.GreaterThan, ">"),
            new ComparisonOperation(RpnOperator.LessOrEqual, "<="),
            new ComparisonOperation(RpnOperator.GreaterOrEqual, ">=")
        ];

        _operations = builtIns.ToDictionary(op => op.Operator);
    }

    public bool TryResolve(RpnOperator op, out IRpnOperation? operation) =>
        _operations.TryGetValue(op, out operation);
}
