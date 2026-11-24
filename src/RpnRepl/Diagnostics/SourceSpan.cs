namespace MiniRepl.Rpn.Diagnostics;

/// <summary>
/// Tipo por valor inmutable (<c>readonly record struct</c>) que representa
/// la posición léxica de un token dentro de la línea de entrada.
/// Se aloja en el stack y se pasa por valor o mediante <c>in</c>.
/// </summary>
public readonly record struct SourceSpan(int Start, int Length)
{
    public static SourceSpan Empty => new(0, 0);

    public int End => Start + Length;

    public override string ToString() => Length > 0 ? $"[{Start}..{End})" : "[REPL]";
}
