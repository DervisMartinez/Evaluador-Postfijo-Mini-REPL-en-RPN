namespace MiniRepl.Rpn.Diagnostics;

/// <summary>
/// Tipo algebraico cerrado (Sum Type) que modela todos los errores controlados
/// del evaluador RPN sin recurrir a excepciones no capturadas para flujo de control.
/// El constructor <c>private</c> restringe la jerarquía exclusivamente a las variantes selladas internas.
/// </summary>
public abstract record RpnError
{
    private RpnError(SourceSpan span) => Span = span;

    public SourceSpan Span { get; }

    public sealed record InvalidToken(string RawToken, SourceSpan Span) : RpnError(Span)
    {
        public override string FormatMessage() =>
            $"Error léxico en {Span}: token inválido '{RawToken}'.";
    }

    public sealed record StackUnderflow(string Operation, int Required, int Available, SourceSpan Span) : RpnError(Span)
    {
        public override string FormatMessage() =>
            $"Error de pila (Stack Underflow) en {Span} para '{Operation}': se requieren {Required} operandos, hay {Available}.";
    }

    public sealed record DivisionByZero(string Operation, SourceSpan Span) : RpnError(Span)
    {
        public override string FormatMessage() =>
            $"Error aritmético en {Span}: división por cero en operación '{Operation}'.";
    }

    public sealed record ArithmeticOverflow(string Operation, string Detail, SourceSpan Span) : RpnError(Span)
    {
        public override string FormatMessage() =>
            $"Error de desbordamiento (checked overflow) en {Span} durante '{Operation}': {Detail}.";
    }

    public sealed record TypeMismatch(string Operation, string LeftType, string RightType, string Expected, SourceSpan Span) : RpnError(Span)
    {
        public override string FormatMessage() =>
            $"Error de tipos en {Span}: '{Operation}' no admite operandos ({LeftType}, {RightType}); se esperaba {Expected}.";
    }

    public sealed record UndefinedVariable(string Name, SourceSpan Span) : RpnError(Span)
    {
        public override string FormatMessage() =>
            $"Error de nombre en {Span}: la variable '{Name}' no está definida en el alcance actual ni en alcances padres.";
    }

    public sealed record InvalidAssignmentTarget(string ReceivedDescription, SourceSpan Span) : RpnError(Span)
    {
        public override string FormatMessage() =>
            $"Error de asignación '=' en {Span}: el destino izquierdo debe ser un identificador (L-value), se recibió {ReceivedDescription}.";
    }

    public sealed record UnmatchedEndBlock(SourceSpan Span) : RpnError(Span)
    {
        public override string FormatMessage() =>
            $"Error de alcance en {Span}: comando 'end' sin un bloque 'begin' abierto correspondiente.";
    }

    public abstract string FormatMessage();
}
