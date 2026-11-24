using System.Diagnostics.CodeAnalysis;
using MiniRepl.Rpn.Types;

namespace MiniRepl.Rpn.Memory;

/// <summary>
/// Marco de alcance (Scope Frame) enlazado a su entorno padre.
/// Permite implementar alcance local anidado (<c>begin ... end</c>) y
/// sombreado (<c>shadowing</c>) sin alterar los marcos externos.
/// </summary>
public sealed class ScopeFrame
{
    private readonly Dictionary<string, RpnValue> _bindings;

    public ScopeFrame(int depth, ScopeFrame? parent)
        : this(depth, parent, new Dictionary<string, RpnValue>(StringComparer.Ordinal))
    {
    }

    private ScopeFrame(int depth, ScopeFrame? parent, Dictionary<string, RpnValue> bindings)
    {
        Depth = depth;
        Parent = parent;
        _bindings = bindings;
    }

    public int Depth { get; }
    public ScopeFrame? Parent { get; }

    public IReadOnlyDictionary<string, RpnValue> Bindings => _bindings;

    public void BindLocal(string name, RpnValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _bindings[name] = value;
    }

    public bool TryGetLocal(string name, [NotNullWhen(true)] out RpnValue? value) =>
        _bindings.TryGetValue(name, out value);

    /// <summary>
    /// Crea una copia profunda de la cadena de marcos para garantizar atomicidad en caso de error.
    /// Como <see cref="RpnValue"/> es inmutable, los valores no necesitan clonarse.
    /// </summary>
    public ScopeFrame CloneChain()
    {
        ScopeFrame? clonedParent = Parent?.CloneChain();
        var clonedBindings = new Dictionary<string, RpnValue>(_bindings, StringComparer.Ordinal);
        return new ScopeFrame(Depth, clonedParent, clonedBindings);
    }
}
