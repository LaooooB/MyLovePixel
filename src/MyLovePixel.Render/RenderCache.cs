using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Render;

internal sealed record LayerRenderState(
    LayerId LayerId,
    LayerSnapshotKind Kind,
    bool Visible,
    byte Opacity,
    CelRenderState? Cel);

internal sealed record CelRenderState(
    CelId CelId,
    ResourceId SurfaceId,
    IntPoint Position,
    byte Opacity,
    IntSize SurfaceSize,
    PixelFormat SurfaceFormat);

internal sealed record ResourceRevisionState(ResourceId SurfaceId, long Revision);

internal sealed class FrameStructureSignature : IEquatable<FrameStructureSignature>
{
    private readonly LayerRenderState[] _layers;

    private FrameStructureSignature(
        IntSize canvasSize,
        PixelFormat canvasFormat,
        LayerRenderState[] layers)
    {
        CanvasSize = canvasSize;
        CanvasFormat = canvasFormat;
        _layers = layers;
    }

    public IntSize CanvasSize { get; }
    public PixelFormat CanvasFormat { get; }

    public static FrameStructureSignature Capture(DocumentSnapshot snapshot, FrameId frameId)
    {
        var cels = snapshot.Cels
            .Where(cel => cel.FrameId == frameId)
            .ToDictionary(cel => cel.LayerId);

        var layers = new LayerRenderState[snapshot.LayerOrder.Count];
        for (var index = 0; index < snapshot.LayerOrder.Count; index++)
        {
            var layerId = snapshot.LayerOrder[index];
            var layer = snapshot.GetLayer(layerId);

            CelRenderState? celState = null;
            if (cels.TryGetValue(layerId, out var cel))
            {
                var surface = snapshot.GetSurface(cel.SurfaceId);
                celState = new CelRenderState(
                    cel.Id,
                    cel.SurfaceId,
                    cel.Position,
                    cel.Opacity,
                    surface.Size,
                    surface.Format);
            }

            layers[index] = new LayerRenderState(
                layer.Id,
                layer.Kind,
                layer.Visible,
                layer.Opacity,
                celState);
        }

        return new FrameStructureSignature(
            snapshot.Canvas.Size,
            snapshot.Canvas.PixelFormat,
            layers);
    }

    public bool Equals(FrameStructureSignature? other) =>
        other is not null &&
        CanvasSize == other.CanvasSize &&
        CanvasFormat == other.CanvasFormat &&
        _layers.AsSpan().SequenceEqual(other._layers);

    public override bool Equals(object? obj) =>
        obj is FrameStructureSignature other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CanvasSize);
        hash.Add(CanvasFormat);
        foreach (var layer in _layers)
            hash.Add(layer);
        return hash.ToHashCode();
    }
}

internal static class FrameRevisionSignature
{
    public static ResourceRevisionState[] Capture(DocumentSnapshot snapshot, FrameId frameId)
    {
        var visibleLayers = snapshot.LayerOrder
            .Select(snapshot.GetLayer)
            .Where(layer => layer.Visible && layer.Opacity != 0)
            .Select(layer => layer.Id)
            .ToHashSet();

        return snapshot.Cels
            .Where(cel =>
                cel.FrameId == frameId &&
                cel.Opacity != 0 &&
                visibleLayers.Contains(cel.LayerId))
            .Select(cel => cel.SurfaceId)
            .Distinct()
            .OrderBy(id => id.Value)
            .Select(id => new ResourceRevisionState(id, snapshot.GetSurface(id).Revision))
            .ToArray();
    }
}

public sealed class RenderCacheDiagnostics
{
    public long FullRecomposeCount { get; private set; }
    public long PartialRecomposeCount { get; private set; }
    public long CacheHitCount { get; private set; }
    public long RecomposedPixelCount { get; private set; }
    public int LastRecomposedPixelCount { get; private set; }

    internal void RecordFull(IntSize size)
    {
        var pixels = checked(size.Width * size.Height);
        FullRecomposeCount++;
        RecomposedPixelCount = checked(RecomposedPixelCount + pixels);
        LastRecomposedPixelCount = pixels;
    }

    internal void RecordPartial(IReadOnlyList<IntRect> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);
        var pixels = 0;
        foreach (var region in regions)
            pixels = checked(pixels + checked(region.Width * region.Height));

        PartialRecomposeCount++;
        RecomposedPixelCount = checked(RecomposedPixelCount + pixels);
        LastRecomposedPixelCount = pixels;
    }

    internal void RecordHit()
    {
        CacheHitCount++;
        LastRecomposedPixelCount = 0;
    }

    public RenderCacheDiagnosticsSnapshot Snapshot() => new(
        FullRecomposeCount,
        PartialRecomposeCount,
        CacheHitCount,
        RecomposedPixelCount,
        LastRecomposedPixelCount);
}

public sealed record RenderCacheDiagnosticsSnapshot(
    long FullRecomposeCount,
    long PartialRecomposeCount,
    long CacheHitCount,
    long RecomposedPixelCount,
    int LastRecomposedPixelCount);

internal static class RenderRegionSet
{
    public static IReadOnlyList<IntRect> Normalize(
        IEnumerable<IntRect> regions,
        IntRect bounds)
    {
        ArgumentNullException.ThrowIfNull(regions);

        var normalized = new List<IntRect>();
        foreach (var input in regions)
        {
            var current = RenderMath.Intersect(input, bounds);
            if (current.IsEmpty) continue;

            var merged = true;
            while (merged)
            {
                merged = false;
                for (var index = normalized.Count - 1; index >= 0; index--)
                {
                    if (!TouchesOrOverlaps(normalized[index], current)) continue;

                    current = IntRect.Union(normalized[index], current);
                    normalized.RemoveAt(index);
                    merged = true;
                }
            }

            normalized.Add(current);
        }

        normalized.Sort(static (a, b) =>
        {
            var byY = a.Y.CompareTo(b.Y);
            return byY != 0 ? byY : a.X.CompareTo(b.X);
        });

        return normalized.AsReadOnly();
    }

    private static bool TouchesOrOverlaps(IntRect a, IntRect b) =>
        (long)a.X <= (long)b.X + b.Width &&
        (long)b.X <= (long)a.X + a.Width &&
        (long)a.Y <= (long)b.Y + b.Height &&
        (long)b.Y <= (long)a.Y + a.Height;
}
