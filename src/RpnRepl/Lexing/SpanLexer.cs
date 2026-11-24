using System.Globalization;
using MiniRepl.Rpn.Diagnostics;
using MiniRepl.Rpn.Types;

namespace MiniRepl.Rpn.Lexing;

/// <summary>
/// Escáner léxico de cero asignaciones (<c>ref struct</c>) que recorre una línea
/// sobre <see cref="ReadOnlySpan{T}"/> sin generar arreglos ni subcadenas intermedias
/// durante la segmentación por espacios en blanco.
/// Al ser un <c>ref struct</c>, el compilador garantiza que resida estrictamente en el stack.
/// </summary>
public ref struct SpanTokenScanner
{
    private readonly ReadOnlySpan<char> _source;
    private int _cursor;

    public SpanTokenScanner(ReadOnlySpan<char> source)
    {
        _source = source;
        _cursor = 0;
    }

    private static bool IsDelimiter(char c) => char.IsWhiteSpace(c) || c == '\uFEFF';

    public bool TryNext(out ReadOnlySpan<char> lexeme, out SourceSpan span)
    {
        while (_cursor < _source.Length && IsDelimiter(_source[_cursor]))
        {
            _cursor++;
        }

        if (_cursor >= _source.Length)
        {
            lexeme = ReadOnlySpan<char>.Empty;
            span = SourceSpan.Empty;
            return false;
        }

        int start = _cursor;
        while (_cursor < _source.Length && !IsDelimiter(_source[_cursor]))
        {
            _cursor++;
        }

        int length = _cursor - start;
        lexeme = _source.Slice(start, length);
        span = new SourceSpan(start, length);
        return true;
    }
}

/// <summary>
/// Analizador léxico para expresiones estrictas en Notación Polaca Inversa (RPN).
/// </summary>
public static class SpanLexer
{
    public static EvalResult<IReadOnlyList<Token>> Tokenize(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var tokens = new List<Token>();
        var scanner = new SpanTokenScanner(input.AsSpan());

        while (scanner.TryNext(out ReadOnlySpan<char> lexeme, out SourceSpan span))
        {
            EvalResult<Token> parsed = ClassifyLexeme(lexeme, in span);
            if (!parsed.TryGetValue(out Token? token, out RpnError? error))
            {
                return EvalResult<IReadOnlyList<Token>>.Fail(error);
            }

            tokens.Add(token);
        }

        return EvalResult<IReadOnlyList<Token>>.Ok(tokens);
    }

    private static EvalResult<Token> ClassifyLexeme(ReadOnlySpan<char> lexeme, in SourceSpan span)
    {
        // Palabras reservadas y símbolos de un solo o dos caracteres mediante pattern matching sobre spans
        switch (lexeme)
        {
            case "+":
                return new Token.Operator(RpnOperator.Add, "+", span);
            case "-":
                return new Token.Operator(RpnOperator.Subtract, "-", span);
            case "*":
                return new Token.Operator(RpnOperator.Multiply, "*", span);
            case "/":
                return new Token.Operator(RpnOperator.Divide, "/", span);
            case "%":
                return new Token.Operator(RpnOperator.Modulo, "%", span);
            case "==":
                return new Token.Operator(RpnOperator.Equal, "==", span);
            case "!=":
                return new Token.Operator(RpnOperator.NotEqual, "!=", span);
            case "<":
                return new Token.Operator(RpnOperator.LessThan, "<", span);
            case ">":
                return new Token.Operator(RpnOperator.GreaterThan, ">", span);
            case "<=":
                return new Token.Operator(RpnOperator.LessOrEqual, "<=", span);
            case ">=":
                return new Token.Operator(RpnOperator.GreaterOrEqual, ">=", span);
            case "=":
                return new Token.Assignment(span);
            case "true":
                return new Token.BoolLiteral(true, span);
            case "false":
                return new Token.BoolLiteral(false, span);
            case "begin":
                return new Token.BlockCommand(BlockKind.Begin, span);
            case "end":
                return new Token.BlockCommand(BlockKind.End, span);
        }

        if (LooksLikeNumber(lexeme))
        {
            return ParseNumber(lexeme, in span);
        }

        if (IsValidIdentifier(lexeme))
        {
            return new Token.Identifier(lexeme.ToString(), span);
        }

        return new RpnError.InvalidToken(lexeme.ToString(), span);
    }

    private static bool LooksLikeNumber(ReadOnlySpan<char> lexeme)
    {
        if (lexeme.IsEmpty)
        {
            return false;
        }

        if (char.IsAsciiDigit(lexeme[0]))
        {
            return true;
        }

        return lexeme[0] == '-' && lexeme.Length > 1 && char.IsAsciiDigit(lexeme[1]);
    }

    private static EvalResult<Token> ParseNumber(ReadOnlySpan<char> lexeme, in SourceSpan span)
    {
        bool hasDecimalOrExponent = lexeme.Contains('.') || lexeme.Contains('e') || lexeme.Contains('E');

        if (hasDecimalOrExponent)
        {
            if (double.TryParse(
                lexeme,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture,
                out double floatValue))
            {
                if (!double.IsFinite(floatValue))
                {
                    return new RpnError.ArithmeticOverflow("literal", $"El literal flotante '{lexeme}' desborda IEEE-754 double.", span);
                }

                return new Token.FloatLiteral(floatValue, span);
            }

            return new RpnError.InvalidToken(lexeme.ToString(), span);
        }

        if (long.TryParse(lexeme, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long intValue))
        {
            return new Token.IntLiteral(intValue, span);
        }

        if (AllDigitsAfterOptionalSign(lexeme))
        {
            return new RpnError.ArithmeticOverflow("literal", $"El literal entero '{lexeme}' excede el rango de Int64.", span);
        }

        return new RpnError.InvalidToken(lexeme.ToString(), span);
    }

    private static bool AllDigitsAfterOptionalSign(ReadOnlySpan<char> lexeme)
    {
        int start = lexeme[0] == '-' ? 1 : 0;
        for (int i = start; i < lexeme.Length; i++)
        {
            if (!char.IsAsciiDigit(lexeme[i]))
            {
                return false;
            }
        }
        return lexeme.Length > start;
    }

    private static bool IsValidIdentifier(ReadOnlySpan<char> lexeme)
    {
        if (lexeme.IsEmpty || !(char.IsLetter(lexeme[0]) || lexeme[0] == '_'))
        {
            return false;
        }

        for (int i = 1; i < lexeme.Length; i++)
        {
            char c = lexeme[i];
            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }

        return true;
    }
}
