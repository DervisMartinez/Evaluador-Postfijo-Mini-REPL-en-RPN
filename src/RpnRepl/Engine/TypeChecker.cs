using MiniRepl.Rpn.Diagnostics;
using MiniRepl.Rpn.Types;

namespace MiniRepl.Rpn.Engine;

/// <summary>
/// Resultado normalizado de la promoción numérica entre operandos (<c>Int</c> + <c>Float</c> -> <c>Float</c>).
/// </summary>
public abstract record NumericOperands
{
    private NumericOperands() { }

    public sealed record IntPair(long Left, long Right) : NumericOperands;
    public sealed record FloatPair(double Left, double Right) : NumericOperands;
}

/// <summary>
/// Sistema de chequeo y coerción controlada de tipos del evaluador.
/// Replica las conversiones numéricas implícitas de C# (<c>Int64 -> Double</c>)
/// mientras prohíbe conversiones implícitas inseguras entre <c>Bool</c> y números.
/// </summary>
public static class TypeChecker
{
    public static EvalResult<NumericOperands> PromoteNumericPair(
        string operatorSymbol,
        RpnValue left,
        RpnValue right,
        in SourceSpan span)
    {
        return (left, right) switch
        {
            (RpnValue.IntValue a, RpnValue.IntValue b) =>
                EvalResult<NumericOperands>.Ok(new NumericOperands.IntPair(a.Value, b.Value)),

            (RpnValue.FloatValue a, RpnValue.FloatValue b) =>
                EvalResult<NumericOperands>.Ok(new NumericOperands.FloatPair(a.Value, b.Value)),

            (RpnValue.IntValue a, RpnValue.FloatValue b) =>
                EvalResult<NumericOperands>.Ok(new NumericOperands.FloatPair(a.Value, b.Value)),

            (RpnValue.FloatValue a, RpnValue.IntValue b) =>
                EvalResult<NumericOperands>.Ok(new NumericOperands.FloatPair(a.Value, b.Value)),

            _ => EvalResult<NumericOperands>.Fail(
                new RpnError.TypeMismatch(
                    operatorSymbol,
                    left.TypeName,
                    right.TypeName,
                    "(Int|Float, Int|Float)",
                    span))
        };
    }

    public static EvalResult<bool> AreEqualityCompatible(
        string operatorSymbol,
        RpnValue left,
        RpnValue right,
        in SourceSpan span)
    {
        return (left, right) switch
        {
            (RpnValue.BoolValue, RpnValue.BoolValue) => EvalResult<bool>.Ok(true),
            (RpnValue.IntValue or RpnValue.FloatValue, RpnValue.IntValue or RpnValue.FloatValue) => EvalResult<bool>.Ok(true),
            _ => EvalResult<bool>.Fail(
                new RpnError.TypeMismatch(
                    operatorSymbol,
                    left.TypeName,
                    right.TypeName,
                    "tipos compatibles (ambos numéricos o ambos Bool)",
                    span))
        };
    }
}
