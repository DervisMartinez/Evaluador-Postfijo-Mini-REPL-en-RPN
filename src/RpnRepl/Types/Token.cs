using MiniRepl.Rpn.Diagnostics;

namespace MiniRepl.Rpn.Types;

public enum BlockKind
{
    Begin,
    End
}

/// <summary>
/// Tipo algebraico cerrado que representa los tokens producidos por el analizador léxico.
/// </summary>
public abstract record Token
{
    private Token(SourceSpan span) => Span = span;

    public SourceSpan Span { get; }

    public sealed record IntLiteral(long Value, SourceSpan Span) : Token(Span);
    public sealed record FloatLiteral(double Value, SourceSpan Span) : Token(Span);
    public sealed record BoolLiteral(bool Value, SourceSpan Span) : Token(Span);
    public sealed record Operator(RpnOperator Op, string Symbol, SourceSpan Span) : Token(Span);
    public sealed record Assignment(SourceSpan Span) : Token(Span);
    public sealed record Identifier(string Name, SourceSpan Span) : Token(Span);
    public sealed record BlockCommand(BlockKind Kind, SourceSpan Span) : Token(Span);
}
