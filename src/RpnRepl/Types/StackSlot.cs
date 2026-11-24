namespace MiniRepl.Rpn.Types;

/// <summary>
/// Representa un elemento almacenado en la pila de evaluación postfija.
/// Distingue explícitamente entre un R-value puro (<see cref="ValueSlot"/>)
/// y un símbolo identificador (<see cref="SymbolSlot"/>) que preserva su nombre
/// como L-value potencial para <c>=</c> y su valor capturado como R-value.
/// </summary>
public abstract record StackSlot
{
    private StackSlot() { }

    public sealed record ValueSlot(RpnValue Value) : StackSlot
    {
        public override string ToString() => Value.ToString();
    }

    public sealed record SymbolSlot(string Name, RpnValue? CapturedValue, int? ScopeDepth, Diagnostics.SourceSpan Span) : StackSlot
    {
        public override string ToString() => CapturedValue is not null
            ? $"{Name} ({CapturedValue})"
            : $"{Name} (L-value sin enlazar)";
    }
}
