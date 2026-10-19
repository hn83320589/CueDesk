namespace CueDesk.Core.History;

/// <summary>
/// Snapshot-based undo/redo. Each <see cref="Push"/> is one step, so a batch operation that
/// produces a single new state is undone in one step. Pushing clears the redo stack.
/// </summary>
public sealed class UndoStack<T>
{
    public const int DefaultCapacity = 100;

    private sealed record Entry(T State, string Description);

    private readonly List<Entry> _undo = [];
    private readonly Stack<Entry> _redo = new();
    private Entry _current;
    private Entry? _saved;

    public UndoStack(T initial, int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
        _current = new Entry(initial, string.Empty);
        _saved = _current;
    }

    public int Capacity { get; }

    public T Current => _current.State;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public int UndoCount => _undo.Count;

    public int RedoCount => _redo.Count;

    /// <summary>Description of the step that <see cref="Undo"/> would revert.</summary>
    public string? UndoDescription => CanUndo ? _current.Description : null;

    public string? RedoDescription => CanRedo ? _redo.Peek().Description : null;

    /// <summary>True when the current state differs from the last state marked as saved.</summary>
    public bool IsModified => !ReferenceEquals(_current, _saved);

    public void Push(T state, string description)
    {
        _undo.Add(_current);
        if (_undo.Count > Capacity)
        {
            _undo.RemoveAt(0);
        }
        _current = new Entry(state, description);
        _redo.Clear();
    }

    public T Undo()
    {
        if (!CanUndo)
        {
            throw new InvalidOperationException("沒有可以復原的步驟");
        }
        _redo.Push(_current);
        _current = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        return _current.State;
    }

    public T Redo()
    {
        if (!CanRedo)
        {
            throw new InvalidOperationException("沒有可以重做的步驟");
        }
        _undo.Add(_current);
        _current = _redo.Pop();
        return _current.State;
    }

    public void MarkSaved() => _saved = _current;

    /// <summary>Starts a new history (e.g. after opening a file); the new state counts as saved.</summary>
    public void Reset(T state)
    {
        _undo.Clear();
        _redo.Clear();
        _current = new Entry(state, string.Empty);
        _saved = _current;
    }
}
