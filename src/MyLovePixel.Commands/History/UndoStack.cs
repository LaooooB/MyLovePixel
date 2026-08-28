using MyLovePixel.Commands.Abstractions;

namespace MyLovePixel.Commands.History;

internal sealed class UndoStack
{
    private readonly List<HistoryEntry> _undo = [];
    private readonly List<HistoryEntry> _redo = [];
    private readonly UndoHistoryOptions _options;
    private long _estimatedBytes;
    private long _evictedUndoEntries;

    public UndoStack(UndoHistoryOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;

    public void Push(HistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ClearRedo();
        _undo.Add(entry);
        _estimatedBytes = checked(_estimatedBytes + Estimate(entry));
        TrimOldestUndoEntries();
    }

    public HistoryEntry PeekUndo() => _undo[^1];
    public HistoryEntry PeekRedo() => _redo[^1];

    public void CommitUndo(HistoryEntry expected)
    {
        if (!ReferenceEquals(_undo[^1], expected))
            throw new InvalidOperationException("Undo history changed unexpectedly.");
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(expected);
    }

    public void CommitRedo(HistoryEntry expected)
    {
        if (!ReferenceEquals(_redo[^1], expected))
            throw new InvalidOperationException("Redo history changed unexpectedly.");

        var before = Estimate(expected);
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(expected);
        var after = Estimate(expected);
        _estimatedBytes = checked(_estimatedBytes - before + after);
        TrimOldestUndoEntries();
    }

    public void RefreshEntryCost(HistoryEntry entry, long previousCost)
    {
        var nextCost = Estimate(entry);
        _estimatedBytes = checked(_estimatedBytes - previousCost + nextCost);
    }

    public long Estimate(HistoryEntry entry) => entry.EstimateMemoryBytes(_options.FallbackTokenBytes);

    public UndoHistoryDiagnostics Diagnostics() => new(
        _options.MemoryBudgetBytes,
        _estimatedBytes,
        _evictedUndoEntries,
        UndoCount,
        RedoCount);

    private void ClearRedo()
    {
        foreach (var entry in _redo)
            _estimatedBytes = checked(_estimatedBytes - Estimate(entry));
        _redo.Clear();
    }

    private void TrimOldestUndoEntries()
    {
        // Preserve the newest command even when one exceptionally large entry alone exceeds the soft budget.
        // This bounds history growth while keeping the latest user action undoable.
        while (_estimatedBytes > _options.MemoryBudgetBytes && _undo.Count > 1)
        {
            var oldest = _undo[0];
            _estimatedBytes = checked(_estimatedBytes - Estimate(oldest));
            _undo.RemoveAt(0);
            _evictedUndoEntries++;
        }
    }
}
