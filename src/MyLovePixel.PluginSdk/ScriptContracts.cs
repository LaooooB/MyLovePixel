namespace MyLovePixel.PluginSdk;

public sealed record ScriptSandboxPolicy(
    long OperationBudget = 1_000_000,
    long MemoryBudgetBytes = 16L * 1024 * 1024,
    TimeSpan? TimeBudget = null,
    bool Deterministic = true)
{
    public TimeSpan EffectiveTimeBudget => TimeBudget ?? TimeSpan.FromSeconds(2);

    public void Validate()
    {
        if (OperationBudget < 1) throw new ArgumentOutOfRangeException(nameof(OperationBudget));
        if (MemoryBudgetBytes < 1) throw new ArgumentOutOfRangeException(nameof(MemoryBudgetBytes));
        if (EffectiveTimeBudget <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(TimeBudget));
    }
}

public interface IPluginScriptContext
{
    long RemainingOperations { get; }
    long RemainingMemoryBytes { get; }
    bool Deterministic { get; }
    void ConsumeOperations(long count = 1);
    void ReserveMemory(long bytes);
    void ReleaseMemory(long bytes);
}

public interface IPluginScriptProgram
{
    string Id { get; }
    ValueTask<PluginValue?> ExecuteAsync(IPluginScriptContext context, CancellationToken cancellationToken);
}

public sealed record PluginScriptExecutionResult(
    bool Succeeded,
    PluginValue? Value,
    string? ErrorCode,
    string? Message);
