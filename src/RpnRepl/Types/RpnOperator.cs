namespace MiniRepl.Rpn.Types;

/// <summary>
/// Operadores binarios soportados por el evaluador RPN.
/// </summary>
public enum RpnOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    Equal,
    NotEqual,
    LessThan,
    GreaterThan,
    LessOrEqual,
    GreaterOrEqual
}

public enum OperatorCategory
{
    Arithmetic,
    Comparison
}

public readonly record struct OperatorDescriptor(
    RpnOperator Operator,
    string Symbol,
    OperatorCategory Category,
    int Arity = 2);
