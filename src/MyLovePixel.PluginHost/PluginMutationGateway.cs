using MyLovePixel.Commands;
using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.PluginSdk;

namespace MyLovePixel.PluginHost;

public sealed record PluginMutationReceipt(DocumentChange Change, long SurfaceRevision);

public sealed record PluginToolExecutionResult(
    bool Succeeded,
    bool Consumed,
    bool Committed,
    IReadOnlyList<PluginPixelWrite> PreviewWrites,
    PluginMutationReceipt? Mutation,
    PluginDiagnostic? Diagnostic);

public sealed class PluginMutationGateway
{
    private readonly PixelDocument _document;
    private readonly CommandBus _commands;

    public PluginMutationGateway(PixelDocument document, CommandBus commands)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public PluginRasterTarget CaptureRgbaTarget(Guid surfaceId)
    {
        if (surfaceId == Guid.Empty) throw new ArgumentException("Surface id cannot be empty.", nameof(surfaceId));
        var id = new ResourceId(surfaceId);
        var snapshot = _document.Resources.GetSurface(id).Snapshot();
        if (snapshot.Format != PixelFormat.Rgba32)
            throw new NotSupportedException("Plugin raster mutation currently requires an RGBA32 surface.");
        return new PluginRasterTarget(
            surfaceId,
            snapshot.Revision,
            new PluginIntSize(snapshot.Size.Width, snapshot.Size.Height),
            snapshot.Bytes);
    }

    public PluginMutationReceipt Execute(PluginPixelPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var id = new ResourceId(patch.SurfaceId);
        var surface = _document.Resources.GetSurface(id);
        if (surface.Format != PixelFormat.Rgba32)
            throw new NotSupportedException("Plugin raster mutation currently requires an RGBA32 surface.");
        if (surface.Revision != patch.ExpectedRevision)
            throw new InvalidOperationException(
                $"Plugin patch is stale. Expected revision {patch.ExpectedRevision}, actual {surface.Revision}.");

        var writes = new PixelWrite[patch.Writes.Count];
        for (var index = 0; index < patch.Writes.Count; index++)
        {
            var write = patch.Writes[index];
            if ((uint)write.X >= (uint)surface.Size.Width || (uint)write.Y >= (uint)surface.Size.Height)
                throw new ArgumentOutOfRangeException(nameof(patch), $"Plugin pixel ({write.X},{write.Y}) is outside the target surface.");
            writes[index] = new PixelWrite(
                write.X,
                write.Y,
                new Rgba32(write.Color.R, write.Color.G, write.Color.B, write.Color.A));
        }

        var change = _commands.Execute(new PixelPatchCommand(id, writes, patch.Name));
        return new PluginMutationReceipt(change, surface.Revision);
    }
}

public static class PluginToolExecution
{
    public static PluginToolExecutionResult Execute(
        PluginHost host,
        string toolId,
        PluginMutationGateway gateway,
        Guid surfaceId,
        PluginPointerEvent pointerEvent)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        ArgumentNullException.ThrowIfNull(gateway);
        if (!host.Tools.TryGet(toolId, out var tool))
            throw new KeyNotFoundException($"Plugin tool '{toolId}' is not registered.");
        var owner = host.Tools.GetOwner(toolId);

        PluginRasterTarget target;
        try
        {
            target = gateway.CaptureRgbaTarget(surfaceId);
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or NotSupportedException)
        {
            var diagnostic = new PluginDiagnostic(
                PluginDiagnosticCode.InvalidMutation,
                owner,
                "Plugin tool target could not be captured.",
                toolId,
                ex);
            host.Record(diagnostic);
            return new PluginToolExecutionResult(false, false, false, Array.Empty<PluginPixelWrite>(), null, diagnostic);
        }

        PluginToolResult result;
        try
        {
            result = tool.Handle(pointerEvent, target) ?? throw new InvalidOperationException("Plugin tool returned null.");
        }
        catch (Exception ex)
        {
            var diagnostic = new PluginDiagnostic(
                PluginDiagnosticCode.ExecutionFailed,
                owner,
                $"Plugin tool '{toolId}' failed.",
                toolId,
                ex);
            host.Record(diagnostic);
            return new PluginToolExecutionResult(false, false, false, Array.Empty<PluginPixelWrite>(), null, diagnostic);
        }

        var preview = ValidatePreview(result.PreviewWrites, target);
        if (result.Commit is null)
            return new PluginToolExecutionResult(true, result.Consumed, false, preview, null, null);

        if (result.Commit.SurfaceId != target.SurfaceId || result.Commit.ExpectedRevision != target.Revision)
        {
            var diagnostic = new PluginDiagnostic(
                PluginDiagnosticCode.InvalidMutation,
                owner,
                $"Plugin tool '{toolId}' produced a patch for a different or stale target.",
                toolId);
            host.Record(diagnostic);
            return new PluginToolExecutionResult(false, result.Consumed, false, preview, null, diagnostic);
        }

        try
        {
            var receipt = gateway.Execute(result.Commit);
            return new PluginToolExecutionResult(true, result.Consumed, true, preview, receipt, null);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or NotSupportedException)
        {
            var diagnostic = new PluginDiagnostic(
                PluginDiagnosticCode.InvalidMutation,
                owner,
                $"Plugin tool '{toolId}' mutation was rejected.",
                toolId,
                ex);
            host.Record(diagnostic);
            return new PluginToolExecutionResult(false, result.Consumed, false, preview, null, diagnostic);
        }
    }

    private static IReadOnlyList<PluginPixelWrite> ValidatePreview(IReadOnlyList<PluginPixelWrite>? writes, PluginRasterTarget target)
    {
        if (writes is null || writes.Count == 0) return Array.Empty<PluginPixelWrite>();
        var values = new List<PluginPixelWrite>(writes.Count);
        foreach (var write in writes)
        {
            if ((uint)write.X >= (uint)target.Size.Width || (uint)write.Y >= (uint)target.Size.Height) continue;
            values.Add(write);
        }
        return values.AsReadOnly();
    }
}
