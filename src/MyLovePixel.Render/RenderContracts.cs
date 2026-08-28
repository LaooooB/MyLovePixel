using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Render;

public readonly record struct SurfaceInvalidation
{
    public SurfaceInvalidation(
        ResourceId surfaceId,
        long fromRevision,
        long toRevision,
        IntRect surfaceRegion)
    {
        if (surfaceId.Value == Guid.Empty)
            throw new ArgumentException("ResourceId cannot be empty.", nameof(surfaceId));
        if (fromRevision < 0)
            throw new ArgumentOutOfRangeException(nameof(fromRevision));
        if (toRevision <= fromRevision)
            throw new ArgumentOutOfRangeException(nameof(toRevision), "ToRevision must be greater than FromRevision.");
        if (surfaceRegion.IsEmpty)
            throw new ArgumentException("Invalidation region cannot be empty.", nameof(surfaceRegion));

        SurfaceId = surfaceId;
        FromRevision = fromRevision;
        ToRevision = toRevision;
        SurfaceRegion = surfaceRegion;
    }

    public ResourceId SurfaceId { get; }
    public long FromRevision { get; }
    public long ToRevision { get; }
    public IntRect SurfaceRegion { get; }
}

public sealed class RenderNodeContext
{
    public RenderNodeContext(DocumentSnapshot snapshot, FrameId frameId)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        if (!snapshot.Frames.ContainsKey(frameId))
            throw new ArgumentException($"Frame '{frameId}' does not exist in the snapshot.", nameof(frameId));
        FrameId = frameId;
    }

    public DocumentSnapshot Snapshot { get; }
    public FrameId FrameId { get; }
}

public interface IRenderNode
{
    string Id { get; }
    long Revision { get; }
    void Execute(RenderNodeContext context, IRenderTarget target, IntRect region);
}

internal sealed record RenderNodeSignature(string Id, long Revision);

internal sealed class RenderGraphSignature : IEquatable<RenderGraphSignature>
{
    private readonly RenderNodeSignature[] _nodes;

    public RenderGraphSignature(long structureRevision, RenderNodeSignature[] nodes)
    {
        StructureRevision = structureRevision;
        _nodes = nodes;
    }

    public long StructureRevision { get; }

    public bool Equals(RenderGraphSignature? other) =>
        other is not null &&
        StructureRevision == other.StructureRevision &&
        _nodes.AsSpan().SequenceEqual(other._nodes);

    public override bool Equals(object? obj) =>
        obj is RenderGraphSignature other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(StructureRevision);
        foreach (var node in _nodes)
            hash.Add(node);
        return hash.ToHashCode();
    }
}

public sealed class RenderGraph
{
    private readonly List<IRenderNode> _nodes = [];

    public IReadOnlyList<IRenderNode> Nodes => _nodes.AsReadOnly();
    public long Revision { get; private set; }

    public void Add(IRenderNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (string.IsNullOrWhiteSpace(node.Id))
            throw new ArgumentException("Render node Id cannot be empty.", nameof(node));
        if (_nodes.Any(existing => string.Equals(existing.Id, node.Id, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Render node '{node.Id}' is already registered.");

        var nextRevision = checked(Revision + 1);
        _nodes.Add(node);
        Revision = nextRevision;
    }

    internal RenderGraphSignature CaptureSignature() =>
        new(
            Revision,
            _nodes
                .Select(node => new RenderNodeSignature(node.Id, node.Revision))
                .ToArray());

    internal void Execute(RenderNodeContext context, IRenderTarget target, IntRect region)
    {
        foreach (var node in _nodes)
            node.Execute(context, target, region);
    }

    public static RenderGraph CreateDefault()
    {
        var graph = new RenderGraph();
        graph.Add(new FrameCompositeRenderNode());
        return graph;
    }
}

public enum RenderCacheOutcome
{
    FullRecompose = 1,
    PartialRecompose = 2,
    CacheHit = 3,
}

public sealed record FrameRenderRequest(
    FrameId FrameId,
    IReadOnlyList<SurfaceInvalidation>? Invalidations = null,
    ViewTransform? View = null,
    ViewRect? Viewport = null,
    IReadOnlyList<IRenderOverlayPass>? OverlayPasses = null);

public sealed record FrameRenderResult(
    CpuRenderSurface Surface,
    RenderOverlayScene Overlays,
    TextureUploadPlan UploadPlan,
    RenderCacheOutcome CacheOutcome,
    RenderCacheDiagnosticsSnapshot Diagnostics)
{
    public bool UsedPartialRecompose => CacheOutcome == RenderCacheOutcome.PartialRecompose;
}
