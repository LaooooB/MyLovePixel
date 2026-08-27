using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Raster.Coordinates;
using MyLovePixel.Raster.Ink;

namespace MyLovePixel.Raster;

public sealed record RasterPatch(IReadOnlyList<PixelWrite> Writes, IntRect DirtyRegion)
{
    public static RasterPatch Empty { get; } = new(Array.Empty<PixelWrite>(), default);
    public bool IsEmpty => Writes.Count == 0;
}

public static class RasterPatchBuilder
{
    public static RasterPatch Build(
        PixelSurfaceSnapshot surface,
        IEnumerable<IntPoint> points,
        Rgba32 paint,
        IInkStrategy ink,
        ICoordinatePolicy? coordinatePolicy = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(ink);
        coordinatePolicy ??= ClipCoordinatePolicy.Instance;

        var seen = new HashSet<IntPoint>();
        var writes = new List<PixelWrite>();
        var dirty = default(IntRect);

        foreach (var input in points)
        {
            if (!coordinatePolicy.TryResolve(surface.Size, input, out var point) || !seen.Add(point)) continue;

            var destination = surface.GetPixel(point.X, point.Y);
            var result = ink.Apply(destination, paint);
            if (result == destination) continue;

            writes.Add(new PixelWrite(point.X, point.Y, result));
            dirty = IntRect.Union(dirty, IntRect.FromPoint(point.X, point.Y));
        }

        return writes.Count == 0 ? RasterPatch.Empty : new RasterPatch(writes.ToArray(), dirty);
    }
}
