namespace MyLovePixel.Commands.History;

internal sealed class UndoStack
{
    private readonly Stack<HistoryEntry> _undo = new();
    private readonly Stack<HistoryEntry> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;

    public void Push(HistoryEntry entry)
    {
        _undo.Push(entry);
        _redo.Clear();
    }

    public HistoryEntry PeekUndo() => _undo.Peek();
    public HistoryEntry PeekRedo() => _redo.Peek();

    public void CommitUndo(HistoryEntry expected)
    {
        if (!ReferenceEquals(_undo.Peek(), expected))
            throw new InvalidOperationException("Undo history changed unexpectedly.");
        _redo.Push(_undo.Pop());
    }

    public void CommitRedo(HistoryEntry expected)
    {
        if (!ReferenceEquals(_redo.Peek(), expected))
            throw new InvalidOperationException("Redo history changed unexpectedly.");
        _undo.Push(_redo.Pop());
    }
}
