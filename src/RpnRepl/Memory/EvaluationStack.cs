using System.Diagnostics.CodeAnalysis;
using MiniRepl.Rpn.Types;

namespace MiniRepl.Rpn.Memory;

/// <summary>
/// Instantánea inmutable de la pila de evaluación para rollback transaccional ante errores.
/// </summary>
public readonly record struct StackSnapshot(StackSlot[] Slots);

/// <summary>
/// Pila de evaluación RPN encapsulada sobre memoria gestionada.
/// Se pasa explícitamente como parámetro a las subrutinas del evaluador para evidenciar
/// el paso de referencias por valor en C#.
/// </summary>
public sealed class EvaluationStack
{
    private readonly List<StackSlot> _slots = [];

    public int Count => _slots.Count;
    public bool IsEmpty => _slots.Count == 0;

    /// <summary>
    /// Vista de solo lectura desde el fondo (índice 0) hasta el tope (índice Count - 1).
    /// </summary>
    public IReadOnlyList<StackSlot> Items => _slots.AsReadOnly();

    public void Push(StackSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        _slots.Add(slot);
    }

    public bool TryPop([NotNullWhen(true)] out StackSlot? slot)
    {
        if (_slots.Count == 0)
        {
            slot = null;
            return false;
        }

        int lastIndex = _slots.Count - 1;
        slot = _slots[lastIndex];
        _slots.RemoveAt(lastIndex);
        return true;
    }

    public bool TryPeek([NotNullWhen(true)] out StackSlot? slot)
    {
        if (_slots.Count == 0)
        {
            slot = null;
            return false;
        }

        slot = _slots[^1];
        return true;
    }

    public void Clear() => _slots.Clear();

    public StackSnapshot CaptureSnapshot() => new([.. _slots]);

    public void RestoreSnapshot(in StackSnapshot snapshot)
    {
        _slots.Clear();
        _slots.AddRange(snapshot.Slots);
    }
}
