using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Render;

public sealed class FrameRenderer
{
    private readonly RenderGraph _graph;
    private readonly Dictionary<FrameCacheKey, FrameCacheEntry> _cache = [];

    public FrameRenderer(RenderGraph? graph = null)
    {
        _graph = graph ?? RenderGraph.CreateDefault();
    }

    public RenderCacheDiagnostics Diagnostics { get; } = new();

    public FrameRenderResult Render(
        DocumentSnapshot snapshot,
        FrameRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);

        if (!snapshot.Frames.ContainsKey(request.FrameId))
            throw new ArgumentException(
                $"Frame '{request.FrameId}' does not exist in the snapshot.",
                nameof(request));

        var key = new FrameCacheKey(snapshot.Id, request.FrameId);
        var structure = FrameStructureSignature.Capture(snapshot, request.FrameId);
        var graphSignature = _graph.CaptureSignature();
        var revisions = FrameRevisionSignature.Capture(snapshot, request.FrameId);
        var context = new RenderNodeContext(snapshot, request.FrameId);
        IReadOnlyList<IntRect> dirtyRegions = Array.Empty<IntRect>();
        RenderCacheOutcome outcome;
        FrameCacheEntry entry;

        if (!_cache.TryGetValue(key, out entry!) ||
            !entry.Structure.Equals(structure) ||
            !entry.GraphSignature.Equals(graphSignature))
        {
            entry = CreateFullEntry(context, structure, graphSignature, revisions);
            _cache[key] = entry;
            outcome = RenderCacheOutcome.FullRecompose;
            Diagnostics.RecordFull(snapshot.Canvas.Size);
        }
        else if (!TryFindChangedSurfaces(
                     entry.Revisions,
                     revisions,
                     out var changedSurfaces))
        {
            RecomposeFull(context, entry.Target);
            entry.Structure = structure;
            entry.Revisions = revisions;
            outcome = RenderCacheOutcome.FullRecompose;
            Diagnostics.RecordFull(snapshot.Canvas.Size);
        }
        else if (changedSurfaces.Count == 0)
        {
            outcome = RenderCacheOutcome.CacheHit;
            Diagnostics.RecordHit();
        }
        else if (TryBuildCanvasDirtyRegions(
                     snapshot,
                     request.FrameId,
                     changedSurfaces,
                     request.Invalidations,
                     out dirtyRegions))
        {
            if (dirtyRegions.Count == 0)
            {
                entry.Revisions = revisions;
                outcome = RenderCacheOutcome.CacheHit;
                Diagnostics.RecordHit();
            }
            else
            {
                foreach (var region in dirtyRegions)
                    _graph.Execute(context, entry.Target, region);

                entry.Revisions = revisions;
                outcome = RenderCacheOutcome.PartialRecompose;
                Diagnostics.RecordPartial(dirtyRegions);
            }
        }
        else
        {
            RecomposeFull(context, entry.Target);
            entry.Revisions = revisions;
            outcome = RenderCacheOutcome.FullRecompose;
            Diagnostics.RecordFull(snapshot.Canvas.Size);
        }

        var overlays = BuildOverlays(snapshot, request);
        var uploadPlan = TextureUploadPlanner.Plan(
            outcome,
            snapshot.Canvas.Size,
            dirtyRegions);

        return new FrameRenderResult(
            entry.Target.Snapshot(),
            overlays,
            uploadPlan,
            outcome,
            Diagnostics.Snapshot());
    }

    public void ClearCaches()
    {
        _cache.Clear();
        foreach (var node in _graph.Nodes.OfType<FrameCompositeRenderNode>())
            node.Effects.ClearCaches();
    }

    private FrameCacheEntry CreateFullEntry(
        RenderNodeContext context,
        FrameStructureSignature structure,
        RenderGraphSignature graphSignature,
        ResourceRevisionState[] revisions)
    {
        var target = new CpuRenderTarget(context.Snapshot.Canvas.Size);
        RecomposeFull(context, target);
        return new FrameCacheEntry(target, structure, graphSignature, revisions);
    }

    private void RecomposeFull(
        RenderNodeContext context,
        CpuRenderTarget target) =>
        _graph.Execute(
            context,
            target,
            RenderMath.Bounds(context.Snapshot.Canvas.Size));

    private static bool TryFindChangedSurfaces(
        ResourceRevisionState[] previous,
        ResourceRevisionState[] current,
        out IReadOnlyList<ChangedSurface> changed)
    {
        if (previous.Length != current.Length)
        {
            changed = Array.Empty<ChangedSurface>();
            return false;
        }

        var values = new List<ChangedSurface>();
        for (var index = 0; index < previous.Length; index++)
        {
            var before = previous[index];
            var after = current[index];
            if (before.SurfaceId != after.SurfaceId ||
                after.Revision < before.Revision)
            {
                changed = Array.Empty<ChangedSurface>();
                return false;
            }

            if (before.Revision != after.Revision)
            {
                values.Add(new ChangedSurface(
                    after.SurfaceId,
                    before.Revision,
                    after.Revision));
            }
        }

        changed = values.AsReadOnly();
        return true;
    }

    private static bool TryBuildCanvasDirtyRegions(
        DocumentSnapshot snapshot,
        FrameId frameId,
        IReadOnlyList<ChangedSurface> changedSurfaces,
        IReadOnlyList<SurfaceInvalidation>? invalidations,
        out IReadOnlyList<IntRect> dirtyRegions)
    {
        if (invalidations is null || invalidations.Count == 0)
        {
            dirtyRegions = Array.Empty<IntRect>();
            return false;
        }

        var canvasBounds = RenderMath.Bounds(snapshot.Canvas.Size);
        var mappedRegions = new List<IntRect>();

        foreach (var changed in changedSurfaces)
        {
            if (!TryCollectCoveredSurfaceRegions(
                    changed,
                    invalidations,
                    out var declaredRegions))
            {
                dirtyRegions = Array.Empty<IntRect>();
                return false;
            }

            var surface = snapshot.GetSurface(changed.SurfaceId);
            var surfaceBounds = RenderMath.Bounds(surface.Size);
            var surfaceRegions = RenderRegionSet.Normalize(
                declaredRegions,
                surfaceBounds);

            if (surfaceRegions.Count == 0)
            {
                dirtyRegions = Array.Empty<IntRect>();
                return false;
            }

            var cels = GetVisibleCelsForSurface(
                snapshot,
                frameId,
                changed.SurfaceId);

            if (cels.Any(cel => cel.Effects.EffectOrder.Count != 0))
            {
                dirtyRegions = Array.Empty<IntRect>();
                return false;
            }

            foreach (var cel in cels)
            foreach (var surfaceRegion in surfaceRegions)
            {
                var canvasRegion = RenderMath.TranslateAndClip(
                    surfaceRegion,
                    cel.Position,
                    canvasBounds);
                if (!canvasRegion.IsEmpty)
                    mappedRegions.Add(canvasRegion);
            }
        }

        dirtyRegions = RenderRegionSet.Normalize(
            mappedRegions,
            canvasBounds);
        return true;
    }

    private static bool TryCollectCoveredSurfaceRegions(
        ChangedSurface changed,
        IReadOnlyList<SurfaceInvalidation> invalidations,
        out IReadOnlyList<IntRect> regions)
    {
        var candidates = invalidations
            .Where(item =>
                item.SurfaceId == changed.SurfaceId &&
                item.ToRevision > changed.FromRevision &&
                item.FromRevision < changed.ToRevision)
            .OrderBy(item => item.FromRevision)
            .ThenByDescending(item => item.ToRevision)
            .ToArray();

        if (candidates.Length == 0)
        {
            regions = Array.Empty<IntRect>();
            return false;
        }

        var usedRegions = new List<IntRect>();
        var cursor = changed.FromRevision;

        while (cursor < changed.ToRevision)
        {
            var covering = candidates
                .Where(item =>
                    item.FromRevision <= cursor &&
                    item.ToRevision > cursor)
                .ToArray();

            if (covering.Length == 0)
            {
                regions = Array.Empty<IntRect>();
                return false;
            }

            var farthestRevision = covering.Max(item => item.ToRevision);
            foreach (var item in covering)
                usedRegions.Add(item.SurfaceRegion);

            cursor = farthestRevision;
        }

        regions = usedRegions.AsReadOnly();
        return true;
    }

    private static IReadOnlyList<CelSnapshot> GetVisibleCelsForSurface(
        DocumentSnapshot snapshot,
        FrameId frameId,
        ResourceId surfaceId)
    {
        var result = new List<CelSnapshot>();

        foreach (var cel in snapshot.Cels)
        {
            if (cel.FrameId != frameId ||
                cel.SurfaceId != surfaceId ||
                cel.Opacity == 0)
                continue;

            var layer = snapshot.GetLayer(cel.LayerId);
            if (!layer.Visible || layer.Opacity == 0) continue;
            result.Add(cel);
        }

        return result.AsReadOnly();
    }

    private static RenderOverlayScene BuildOverlays(
        DocumentSnapshot snapshot,
        FrameRenderRequest request)
    {
        var passes = request.OverlayPasses;
        if (passes is null || passes.Count == 0)
            return RenderOverlayScene.Empty;

        var view = request.View ?? new ViewTransform(1);
        var context = new RenderOverlayContext(
            snapshot,
            request.FrameId,
            view,
            request.Viewport);
        var builder = new RenderOverlayBuilder();

        foreach (var pass in passes)
        {
            ArgumentNullException.ThrowIfNull(pass);
            if (string.IsNullOrWhiteSpace(pass.Id))
                throw new InvalidOperationException("Overlay pass Id cannot be empty.");
            pass.Build(context, builder);
        }

        return builder.Build();
    }

    private readonly record struct FrameCacheKey(
        DocumentId DocumentId,
        FrameId FrameId);

    private sealed class FrameCacheEntry(
        CpuRenderTarget target,
        FrameStructureSignature structure,
        RenderGraphSignature graphSignature,
        ResourceRevisionState[] revisions)
    {
        public CpuRenderTarget Target { get; } = target;
        public FrameStructureSignature Structure { get; set; } = structure;
        public RenderGraphSignature GraphSignature { get; set; } = graphSignature;
        public ResourceRevisionState[] Revisions { get; set; } = revisions;
    }

    private readonly record struct ChangedSurface(
        ResourceId SurfaceId,
        long FromRevision,
        long ToRevision);
}
