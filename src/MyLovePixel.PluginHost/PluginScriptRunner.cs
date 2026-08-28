using MyLovePixel.PluginSdk;

namespace MyLovePixel.PluginHost;

public static class PluginScriptRunner
{
    public static async ValueTask<PluginScriptExecutionResult> ExecuteAsync(
        IPluginScriptProgram program,
        ScriptSandboxPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        var effective = policy ?? new ScriptSandboxPolicy();
        effective.Validate();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(effective.EffectiveTimeBudget);
        var context = new BudgetContext(effective);

        try
        {
            var value = await program.ExecuteAsync(context, timeout.Token).ConfigureAwait(false);
            if (context.TryGetFailure(out var budgetFailure)) return budgetFailure;
            if (cancellationToken.IsCancellationRequested)
                return Failure("cancelled", "Script execution was cancelled.");
            if (timeout.IsCancellationRequested)
                return Failure("time-budget-exceeded", "Script time budget exceeded.");
            return new PluginScriptExecutionResult(true, value, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure("cancelled", "Script execution was cancelled.");
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return Failure("time-budget-exceeded", "Script time budget exceeded.");
        }
        catch (PluginScriptBudgetException ex)
        {
            return Failure(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return Failure("script-failed", ex.Message);
        }
    }

    private static PluginScriptExecutionResult Failure(string code, string message) =>
        new(false, null, code, message);

    private sealed class BudgetContext : IPluginScriptContext
    {
        private readonly object _gate = new();
        private readonly ScriptSandboxPolicy _policy;
        private long _operations;
        private long _memory;
        private string? _failureCode;
        private string? _failureMessage;

        public BudgetContext(ScriptSandboxPolicy policy) => _policy = policy;

        public long RemainingOperations
        {
            get
            {
                lock (_gate) return Math.Max(0, _policy.OperationBudget - _operations);
            }
        }

        public long RemainingMemoryBytes
        {
            get
            {
                lock (_gate) return Math.Max(0, _policy.MemoryBudgetBytes - _memory);
            }
        }

        public bool Deterministic => _policy.Deterministic;

        public void ConsumeOperations(long count = 1)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            lock (_gate)
            {
                ThrowIfFailed();
                if (count > _policy.OperationBudget - _operations)
                    Fail("operation-budget-exceeded", "Script operation budget exceeded.");
                _operations += count;
            }
        }

        public void ReserveMemory(long bytes)
        {
            if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
            lock (_gate)
            {
                ThrowIfFailed();
                if (bytes > _policy.MemoryBudgetBytes - _memory)
                    Fail("memory-budget-exceeded", "Script memory budget exceeded.");
                _memory += bytes;
            }
        }

        public void ReleaseMemory(long bytes)
        {
            if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
            lock (_gate)
            {
                ThrowIfFailed();
                if (bytes > _memory)
                    throw new InvalidOperationException("Script cannot release more accounted memory than it reserved.");
                _memory -= bytes;
            }
        }

        public bool TryGetFailure(out PluginScriptExecutionResult result)
        {
            lock (_gate)
            {
                if (_failureCode is null)
                {
                    result = null!;
                    return false;
                }
                result = Failure(_failureCode, _failureMessage!);
                return true;
            }
        }

        private void ThrowIfFailed()
        {
            if (_failureCode is not null)
                throw new PluginScriptBudgetException(_failureCode, _failureMessage!);
        }

        private void Fail(string code, string message)
        {
            _failureCode ??= code;
            _failureMessage ??= message;
            throw new PluginScriptBudgetException(_failureCode, _failureMessage);
        }
    }

    private sealed class PluginScriptBudgetException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }
}
