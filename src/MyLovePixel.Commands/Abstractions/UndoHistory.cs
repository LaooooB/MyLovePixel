namespace MyLovePixel.Commands.Abstractions;

public interface IUndoMemoryCost
{
    long EstimatedMemoryBytes { get; }
}

public sealed record UndoHistoryOptions(
    long MemoryBudgetBytes = 64L * 1024 * 1024,
    long FallbackTokenBytes = 256)
{
    public void Validate()
    {
        if (MemoryBudgetBytes < 1) throw new ArgumentOutOfRangeException(nameof(MemoryBudgetBytes));
        if (FallbackTokenBytes < 1) throw new ArgumentOutOfRangeException(nameof(FallbackTokenBytes));
    }
}

public sealed record UndoHistoryDiagnostics(
    long MemoryBudgetBytes,
    long EstimatedHistoryBytes,
    long EvictedUndoEntryCount,
    int UndoCount,
    int RedoCount)
{
    public bool IsOverBudget => EstimatedHistoryBytes > MemoryBudgetBytes;
}
