using MiniRepl.Rpn.Diagnostics;
using MiniRepl.Rpn.Lexing;
using MiniRepl.Rpn.Types;
using Xunit;

namespace RpnRepl.Tests;

public class LexerTests
{
    [Fact]
    public void Tokenize_ValidRpnExpression_ProducesExpectedTokensAndSpans()
    {
        EvalResult<IReadOnlyList<Token>> result = SpanLexer.Tokenize("x -5 3.5 true + =");

        Assert.True(result.TryGetValue(out IReadOnlyList<Token>? tokens, out _));
        Assert.Equal(6, tokens.Count);

        var t0 = Assert.IsType<Token.Identifier>(tokens[0]);
        Assert.Equal("x", t0.Name);
        Assert.Equal(new SourceSpan(0, 1), t0.Span);

        var t1 = Assert.IsType<Token.IntLiteral>(tokens[1]);
        Assert.Equal(-5L, t1.Value);

        var t2 = Assert.IsType<Token.FloatLiteral>(tokens[2]);
        Assert.Equal(3.5, t2.Value, precision: 10);

        var t3 = Assert.IsType<Token.BoolLiteral>(tokens[3]);
        Assert.True(t3.Value);

        var t4 = Assert.IsType<Token.Operator>(tokens[4]);
        Assert.Equal(RpnOperator.Add, t4.Op);

        Assert.IsType<Token.Assignment>(tokens[5]);
    }

    [Theory]
    [InlineData("12abc")]
    [InlineData("@var")]
    [InlineData("===")]
    [InlineData("1.2.3")]
    [InlineData("5+")]
    public void Tokenize_InvalidLexeme_ReturnsInvalidTokenError(string input)
    {
        EvalResult<IReadOnlyList<Token>> result = SpanLexer.Tokenize(input);

        Assert.False(result.IsSuccess);
        Assert.True(result.Match(_ => false, err => err is RpnError.InvalidToken));
    }

    [Fact]
    public void Tokenize_HugeIntegerLiteral_ReturnsArithmeticOverflow()
    {
        EvalResult<IReadOnlyList<Token>> result = SpanLexer.Tokenize("999999999999999999999999");

        Assert.True(result.IsFailure);
        Assert.True(result.Match(_ => false, err => err is RpnError.ArithmeticOverflow));
    }

    [Fact]
    public void Tokenize_LeadingUtf8Bom_IsIgnoredCleanly()
    {
        EvalResult<IReadOnlyList<Token>> result = SpanLexer.Tokenize("\uFEFFx 10 =");

        Assert.True(result.TryGetValue(out IReadOnlyList<Token>? tokens, out _));
        Assert.Equal(3, tokens.Count);
        var ident = Assert.IsType<Token.Identifier>(tokens[0]);
        Assert.Equal("x", ident.Name);
    }
}
