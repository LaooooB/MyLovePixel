using MyLovePixel.PluginSdk;

namespace MyLovePixel.PluginHost;

public sealed record PluginCommandExecutionResult(
    bool Succeeded,
    bool Mutated,
    string? Message,
    PluginMutationReceipt? Mutation,
    PluginDiagnostic? Diagnostic);

public static class PluginCommandExecution
{
    public static PluginCommandExecutionResult Execute(
        PluginHost host,
        string commandId,
        PluginMutationGateway gateway,
        Guid? surfaceId = null,
        IReadOnlyDictionary<string, PluginValue>? arguments = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(gateway);
        if (!host.Commands.TryGet(commandId, out var command))
            throw new KeyNotFoundException($"Plugin command '{commandId}' is not registered.");
        var owner = host.Commands.GetOwner(commandId);

        PluginRasterTarget? target = null;
        if (surfaceId is { } id)
        {
            try
            {
                target = gateway.CaptureRgbaTarget(id);
            }
            catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or NotSupportedException)
            {
                var diagnostic = new PluginDiagnostic(
                    PluginDiagnosticCode.InvalidMutation,
                    owner,
                    $"Plugin command '{commandId}' target could not be captured.",
                    commandId,
                    ex);
                host.Record(diagnostic);
                return new PluginCommandExecutionResult(false, false, null, null, diagnostic);
            }
        }

        PluginCommandResult result;
        try
        {
            result = command.Execute(new PluginCommandRequest(
                commandId,
                target,
                arguments ?? new Dictionary<string, PluginValue>(StringComparer.Ordinal)))
                ?? throw new InvalidOperationException("Plugin command returned null.");
        }
        catch (Exception ex)
        {
            var diagnostic = new PluginDiagnostic(
                PluginDiagnosticCode.ExecutionFailed,
                owner,
                $"Plugin command '{commandId}' failed.",
                commandId,
                ex);
            host.Record(diagnostic);
            return new PluginCommandExecutionResult(false, false, null, null, diagnostic);
        }

        if (result.Mutation is null)
            return new PluginCommandExecutionResult(true, false, result.Message, null, null);
        if (target is null || result.Mutation.SurfaceId != target.SurfaceId || result.Mutation.ExpectedRevision != target.Revision)
        {
            var diagnostic = new PluginDiagnostic(
                PluginDiagnosticCode.InvalidMutation,
                owner,
                $"Plugin command '{commandId}' produced a patch for a missing, different, or stale target.",
                commandId);
            host.Record(diagnostic);
            return new PluginCommandExecutionResult(false, false, result.Message, null, diagnostic);
        }

        try
        {
            var mutation = gateway.Execute(result.Mutation);
            return new PluginCommandExecutionResult(true, true, result.Message, mutation, null);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or NotSupportedException)
        {
            var diagnostic = new PluginDiagnostic(
                PluginDiagnosticCode.InvalidMutation,
                owner,
                $"Plugin command '{commandId}' mutation was rejected.",
                commandId,
                ex);
            host.Record(diagnostic);
            return new PluginCommandExecutionResult(false, false, result.Message, null, diagnostic);
        }
    }
}
