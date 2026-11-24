using System.Globalization;

namespace MiniRepl.Rpn.Types;

/// <summary>
/// Tipo algebraico cerrado que representa los valores en tiempo de ejecución
/// soportados por el sistema de tipos del evaluador (<c>Int</c>, <c>Float</c>, <c>Bool</c>).
/// Las instancias son registros inmutables con igualdad estructural por valor.
/// </summary>
public abstract record RpnValue
{
    private RpnValue() { }

    public abstract string TypeName { get; }
    public abstract string FormatValue();

    public sealed record IntValue(long Value) : RpnValue
    {
        public override string TypeName => "Int";
        public override string FormatValue() => Value.ToString(CultureInfo.InvariantCulture);
    }

    public sealed record FloatValue(double Value) : RpnValue
    {
        public override string TypeName => "Float";

        public override string FormatValue()
        {
            string formatted = Value.ToString("G15", CultureInfo.InvariantCulture);
            if (!formatted.Contains('.') && !formatted.Contains('E') && !formatted.Contains('e'))
            {
                formatted += ".0";
            }
            return formatted;
        }
    }

    public sealed record BoolValue(bool Value) : RpnValue
    {
        public override string TypeName => "Bool";
        public override string FormatValue() => Value ? "true" : "false";
    }

    public sealed override string ToString() => $"{FormatValue()} : {TypeName}";
}
