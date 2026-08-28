using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Raster;

namespace MyLovePixel.Tools;

public sealed record ToolDescriptor(
    string Id,
    string DisplayName,
    ToolOptionSchema Options)
{
    public ToolDescriptor(string id, string displayName)
        : this(id, displayName, new ToolOptionSchema())
    {
    }
}

public readonly record struct ToolTarget(
    CelId CelId,
    LayerId LayerId,
    FrameId FrameId,
    ResourceId SurfaceId,
    IntPoint SurfaceOriginInCanvas)
{
    public static ToolTarget FromCel(Cel cel)
    {
        ArgumentNullException.ThrowIfNull(cel);
        return new ToolTarget(cel.Id, cel.LayerId, cel.FrameId, cel.SurfaceId, cel.Position);
    }

    public IntPoint CanvasToSurface(IntPoint canvasPoint) =>
        new(
            checked(canvasPoint.X - SurfaceOriginInCanvas.X),
            checked(canvasPoint.Y - SurfaceOriginInCanvas.Y));
}

public interface IToolDocumentReader
{
    PixelSurfaceSnapshot CaptureSurface(ResourceId surfaceId);
    long GetSurfaceRevision(ResourceId surfaceId);
}

public sealed class PixelDocumentToolReader : IToolDocumentReader
{
    private readonly PixelDocument _document;

    public PixelDocumentToolReader(PixelDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public int SurfaceSnapshotCaptureCount { get; private set; }

    public PixelSurfaceSnapshot CaptureSurface(ResourceId surfaceId)
    {
        SurfaceSnapshotCaptureCount = checked(SurfaceSnapshotCaptureCount + 1);
        return _document.Resources.GetSurface(surfaceId).Snapshot();
    }

    public long GetSurfaceRevision(ResourceId surfaceId) =>
        _document.Resources.GetSurface(surfaceId).Revision;
}

public sealed class ToolInteractionConflictException : InvalidOperationException
{
    public ToolInteractionConflictException(ResourceId surfaceId, long expectedRevision, long actualRevision)
        : base($"Surface '{surfaceId}' changed during the tool interaction. Expected revision {expectedRevision}, actual revision {actualRevision}.")
    {
        SurfaceId = surfaceId;
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }

    public ResourceId SurfaceId { get; }
    public long ExpectedRevision { get; }
    public long ActualRevision { get; }
}

public sealed class ToolContext
{
    public ToolContext(
        IToolDocumentReader document,
        CommandBus commands,
        ToolTarget target,
        Rgba32 primaryColor,
        Rgba32 secondaryColor,
        RasterWorkBudget? workBudget = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        Target = target;
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
        WorkBudget = workBudget ?? RasterWorkBudget.Default;
    }

    public IToolDocumentReader Document { get; }
    public CommandBus Commands { get; }
    public ToolTarget Target { get; }
    public Rgba32 PrimaryColor { get; }
    public Rgba32 SecondaryColor { get; }
    public RasterWorkBudget WorkBudget { get; }

    public PixelSurfaceSnapshot CaptureTargetSurface() =>
        Document.CaptureSurface(Target.SurfaceId);

    public bool CommitPatch(RasterPatch patch, long expectedRevision, string commandName)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (patch.IsEmpty) return false;

        var actualRevision = Document.GetSurfaceRevision(Target.SurfaceId);
        if (actualRevision != expectedRevision)
            throw new ToolInteractionConflictException(Target.SurfaceId, expectedRevision, actualRevision);

        Commands.Execute(new PixelPatchCommand(Target.SurfaceId, patch.Writes, commandName));
        return true;
    }
}

public sealed record ToolPreview(ResourceId SurfaceId, RasterPatch Patch)
{
    public bool IsEmpty => Patch.IsEmpty;
}

public sealed record ToolDispatchResult(
    bool Consumed,
    ToolPreview? Preview,
    bool Committed)
{
    public static ToolDispatchResult Ignored { get; } = new(false, null, false);
    public static ToolDispatchResult Cleared { get; } = new(true, null, false);
}

public interface ITool
{
    ToolDescriptor Descriptor { get; }
    bool IsInteracting { get; }
    ToolDispatchResult HandlePointer(ToolContext context, ToolOptions options, PointerEvent pointerEvent);
    ToolDispatchResult Cancel(ToolContext context);
}
