using System.Diagnostics.CodeAnalysis;
using MiniRepl.Rpn.Types;

namespace MiniRepl.Rpn.Memory;

/// <summary>
/// Información diagnóstica de una variable visible en el entorno actual,
/// indicando en qué nivel fue definida y si sombrea (<c>shadowing</c>) a otra externa.
/// </summary>
public readonly record struct ActiveVariableInfo(
    string Name,
    RpnValue Value,
    int DefinedAtDepth,
    int? ShadowedDepth,
    RpnValue? ShadowedValue);

public readonly record struct EnvironmentSnapshot(ScopeFrame RootHead);

/// <summary>
/// Abstracción orientada a interfaces para consultar símbolos sin exponer mutación directa.
/// </summary>
public interface IScopeLookup
{
    int CurrentDepth { get; }
    bool TryLookup(string name, [NotNullWhen(true)] out RpnValue? value, out int foundAtDepth);
    IReadOnlyList<ActiveVariableInfo> GetActiveVariables();
}

/// <summary>
/// Tabla de símbolos jerárquica con pila de alcances (<c>begin ... end</c>).
/// Demuestra el paso de referencias por valor (mutación de tabla) frente al paso
/// por referencia explícita (<c>ref ScopeFrame</c>) al abrir o cerrar bloques.
/// </summary>
public sealed class SymbolEnvironment : IScopeLookup
{
    private ScopeFrame _currentScope = new(depth: 0, parent: null);

    public int CurrentDepth => _currentScope.Depth;

    /// <summary>
    /// Declara o actualiza la variable exclusivamente en el marco de alcance más interno,
    /// produciendo shadowing si existe una variable homónima en un alcance padre.
    /// </summary>
    public void BindInCurrentScope(string name, RpnValue value) =>
        _currentScope.BindLocal(name, value);

    /// <summary>
    /// Busca un símbolo desde el alcance más interno hacia el alcance global mediante parámetros <c>out</c>.
    /// </summary>
    public bool TryLookup(string name, [NotNullWhen(true)] out RpnValue? value, out int foundAtDepth)
    {
        for (ScopeFrame? cursor = _currentScope; cursor is not null; cursor = cursor.Parent)
        {
            if (cursor.TryGetLocal(name, out value))
            {
                foundAtDepth = cursor.Depth;
                return true;
            }
        }

        value = null;
        foundAtDepth = -1;
        return false;
    }

    public int EnterScope()
    {
        PushLocalFrame(ref _currentScope);
        return _currentScope.Depth;
    }

    public bool TryExitScope(out int newDepth)
    {
        bool exited = TryPopLocalFrame(ref _currentScope);
        newDepth = _currentScope.Depth;
        return exited;
    }

    /// <summary>
    /// Demuestra paso por referencia (<c>ref</c>): recibe un puntero administrado a la variable
    /// del llamador y reasigna directamente su referencia al nuevo marco local.
    /// </summary>
    private static void PushLocalFrame(ref ScopeFrame activeFrame)
    {
        activeFrame = new ScopeFrame(activeFrame.Depth + 1, activeFrame);
    }

    /// <summary>
    /// Demuestra paso por referencia (<c>ref</c>) al retroceder hacia el marco padre.
    /// Retorna <c>false</c> si ya se encuentra en el ámbito global (profundidad 0).
    /// </summary>
    private static bool TryPopLocalFrame(ref ScopeFrame activeFrame)
    {
        if (activeFrame.Parent is null)
        {
            return false;
        }

        activeFrame = activeFrame.Parent;
        return true;
    }

    public IReadOnlyList<ActiveVariableInfo> GetActiveVariables()
    {
        var seen = new Dictionary<string, ActiveVariableInfo>(StringComparer.Ordinal);

        for (ScopeFrame? cursor = _currentScope; cursor is not null; cursor = cursor.Parent)
        {
            foreach (KeyValuePair<string, RpnValue> entry in cursor.Bindings)
            {
                if (!seen.TryGetValue(entry.Key, out ActiveVariableInfo existing))
                {
                    seen[entry.Key] = new ActiveVariableInfo(
                        Name: entry.Key,
                        Value: entry.Value,
                        DefinedAtDepth: cursor.Depth,
                        ShadowedDepth: null,
                        ShadowedValue: null);
                }
                else if (existing.ShadowedDepth is null)
                {
                    // Registra el alcance padre inmediato que está siendo sombreado
                    seen[entry.Key] = existing with
                    {
                        ShadowedDepth = cursor.Depth,
                        ShadowedValue = entry.Value
                    };
                }
            }
        }

        return seen.Values.OrderBy(v => v.Name, StringComparer.Ordinal).ToList();
    }

    public EnvironmentSnapshot CaptureSnapshot() => new(_currentScope.CloneChain());

    public void RestoreSnapshot(in EnvironmentSnapshot snapshot)
    {
        _currentScope = snapshot.RootHead.CloneChain();
    }
}
