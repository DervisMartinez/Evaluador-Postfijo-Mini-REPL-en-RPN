using System.Diagnostics.CodeAnalysis;

namespace MiniRepl.Rpn.Diagnostics;

/// <summary>
/// Tipo de retorno idiomático orientado a resultados (<c>Result&lt;T, RpnError&gt;</c>)
/// implementado como <c>readonly record struct</c> para evitar asignaciones innecesarias en el heap.
/// Permite propagación funcional sin lanzar excepciones para errores esperados del dominio.
/// </summary>
public readonly record struct EvalResult<T> where T : notnull
{
    private readonly T? _value;
    private readonly RpnError? _error;

    private EvalResult(T value)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
    }

    private EvalResult(RpnError error)
    {
        IsSuccess = false;
        _value = default;
        _error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public static EvalResult<T> Ok(T value) => new(value);
    public static EvalResult<T> Fail(RpnError error) => new(error);

    public bool TryGetValue(
        [NotNullWhen(true)] out T? value,
        [NotNullWhen(false)] out RpnError? error)
    {
        if (IsSuccess)
        {
            value = _value!;
            error = null;
            return true;
        }

        value = default;
        error = _error!;
        return false;
    }

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<RpnError, TResult> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(_error!);

    public EvalResult<TNext> Map<TNext>(Func<T, TNext> mapper) where TNext : notnull =>
        IsSuccess ? EvalResult<TNext>.Ok(mapper(_value!)) : EvalResult<TNext>.Fail(_error!);

    public EvalResult<TNext> Bind<TNext>(Func<T, EvalResult<TNext>> binder) where TNext : notnull =>
        IsSuccess ? binder(_value!) : EvalResult<TNext>.Fail(_error!);

    public static implicit operator EvalResult<T>(T value) => Ok(value);
    public static implicit operator EvalResult<T>(RpnError error) => Fail(error);
}

/// <summary>
/// Tipo unidad sin estado (<c>Unit</c>) equivalente al tipo vacío funcional <c>()</c>,
/// útil para operaciones con efectos sobre pila/entorno que devuelven <see cref="EvalResult{Unit}"/>.
/// </summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
