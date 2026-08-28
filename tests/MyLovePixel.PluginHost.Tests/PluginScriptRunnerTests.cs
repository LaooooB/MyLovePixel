using MyLovePixel.PluginHost;
using MyLovePixel.PluginSdk;
using Xunit;

namespace MyLovePixel.PluginHost.Tests;

public sealed class PluginScriptRunnerTests
{
    [Fact]
    public async Task OperationBudgetExceeded_RemainsFailureWhenProgramCatchesException()
    {
        var result = await PluginScriptRunner.ExecuteAsync(
            new CatchingOperationProgram(),
            new ScriptSandboxPolicy(OperationBudget: 5, MemoryBudgetBytes: 1024, TimeBudget: TimeSpan.FromSeconds(1)));

        Assert.False(result.Succeeded);
        Assert.Equal("operation-budget-exceeded", result.ErrorCode);
    }

    [Fact]
    public async Task MemoryBudgetExceeded_IsReported()
    {
        var result = await PluginScriptRunner.ExecuteAsync(
            new MemoryProgram(),
            new ScriptSandboxPolicy(OperationBudget: 100, MemoryBudgetBytes: 8, TimeBudget: TimeSpan.FromSeconds(1)));

        Assert.False(result.Succeeded);
        Assert.Equal("memory-budget-exceeded", result.ErrorCode);
    }

    [Fact]
    public async Task TimeBudgetExceeded_IsReportedThroughCooperativeCancellation()
    {
        var result = await PluginScriptRunner.ExecuteAsync(
            new WaitingProgram(),
            new ScriptSandboxPolicy(OperationBudget: 100, MemoryBudgetBytes: 1024, TimeBudget: TimeSpan.FromMilliseconds(50)));

        Assert.False(result.Succeeded);
        Assert.Equal("time-budget-exceeded", result.ErrorCode);
    }

    [Fact]
    public async Task ExternalCancellation_IsDistinctFromBudgetTimeout()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await PluginScriptRunner.ExecuteAsync(
            new CancellationProgram(),
            cancellationToken: cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.Equal("cancelled", result.ErrorCode);
    }

    [Fact]
    public async Task DeterminismFlagAndAccounting_AreVisibleToRuntimeProgram()
    {
        var result = await PluginScriptRunner.ExecuteAsync(
            new AccountingProgram(),
            new ScriptSandboxPolicy(
                OperationBudget: 10,
                MemoryBudgetBytes: 16,
                TimeBudget: TimeSpan.FromSeconds(1),
                Deterministic: false));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.Equal(PluginValueKind.Text, result.Value.Kind);
        Assert.Equal("False:8:12", result.Value.TextValue);
    }

    [Fact]
    public void ScriptValueCodec_RoundTripsEveryPublicValueKindDeterministically()
    {
        var values = new PluginValue?[]
        {
            null,
            PluginValue.Integer(42),
            PluginValue.Number(1.25),
            PluginValue.Boolean(true),
            PluginValue.Color(new PluginRgba32(1, 2, 3, 4)),
            PluginValue.Point(new PluginIntPoint(-3, 9)),
            PluginValue.Identifier(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            PluginValue.Text("pixel"),
        };

        foreach (var value in values)
        {
            var first = PluginScriptValueCodec.Serialize(value);
            var second = PluginScriptValueCodec.Serialize(value);
            Assert.Equal(first, second);
            var roundTrip = PluginScriptValueCodec.Deserialize(first);
            Assert.Equal(value, roundTrip);
        }
    }

    private sealed class CatchingOperationProgram : IPluginScriptProgram
    {
        public string Id => "test.script.operation";

        public ValueTask<PluginValue?> ExecuteAsync(IPluginScriptContext context, CancellationToken cancellationToken)
        {
            try
            {
                context.ConsumeOperations(6);
            }
            catch (Exception)
            {
                // A script/runtime cannot erase the host's latched budget violation by catching it.
            }
            return ValueTask.FromResult<PluginValue?>(PluginValue.Text("caught"));
        }
    }

    private sealed class MemoryProgram : IPluginScriptProgram
    {
        public string Id => "test.script.memory";

        public ValueTask<PluginValue?> ExecuteAsync(IPluginScriptContext context, CancellationToken cancellationToken)
        {
            context.ReserveMemory(9);
            return ValueTask.FromResult<PluginValue?>(null);
        }
    }

    private sealed class WaitingProgram : IPluginScriptProgram
    {
        public string Id => "test.script.timeout";

        public async ValueTask<PluginValue?> ExecuteAsync(IPluginScriptContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return null;
        }
    }

    private sealed class CancellationProgram : IPluginScriptProgram
    {
        public string Id => "test.script.cancel";

        public ValueTask<PluginValue?> ExecuteAsync(IPluginScriptContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<PluginValue?>(null);
        }
    }

    private sealed class AccountingProgram : IPluginScriptProgram
    {
        public string Id => "test.script.accounting";

        public ValueTask<PluginValue?> ExecuteAsync(IPluginScriptContext context, CancellationToken cancellationToken)
        {
            context.ConsumeOperations(2);
            context.ReserveMemory(7);
            context.ReleaseMemory(3);
            return ValueTask.FromResult<PluginValue?>(PluginValue.Text(
                $"{context.Deterministic}:{context.RemainingOperations}:{context.RemainingMemoryBytes}"));
        }
    }
}
