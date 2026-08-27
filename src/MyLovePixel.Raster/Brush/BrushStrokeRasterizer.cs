using MyLovePixel.Core.Primitives;
using MyLovePixel.Raster.Geometry;
using MyLovePixel.Raster.Strokes;

namespace MyLovePixel.Raster.Brush;

public static class BrushStrokeRasterizer
{
    public static IReadOnlyList<IntPoint> Rasterize(
        IReadOnlyList<IntPoint> samples,
        BrushMask brush,
        int spacingPixels = 1,
        IStrokeFilter? strokeFilter = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(brush);
        if (spacingPixels <= 0) throw new ArgumentOutOfRangeException(nameof(spacingPixels));
        if (samples.Count == 0) return Array.Empty<IntPoint>();

        var path = BuildConnectedPath(samples);
        var filteredPath = (strokeFilter ?? IdentityStrokeFilter.Instance).Filter(path);
        if (filteredPath.Count == 0) return Array.Empty<IntPoint>();

        var centers = ApplySpacing(filteredPath, spacingPixels);
        var seen = new HashSet<IntPoint>();
        var points = new List<IntPoint>();

        foreach (var center in centers)
        foreach (var point in brush.Stamp(center))
        {
            if (seen.Add(point)) points.Add(point);
        }

        return points;
    }

    internal static IReadOnlyList<IntPoint> BuildConnectedPath(IReadOnlyList<IntPoint> samples)
    {
        if (samples.Count == 0) return Array.Empty<IntPoint>();

        var result = new List<IntPoint> { samples[0] };
        for (var index = 1; index < samples.Count; index++)
        {
            var segment = LineRasterizer.Rasterize(samples[index - 1], samples[index]);
            for (var pointIndex = 1; pointIndex < segment.Count; pointIndex++)
            {
                var point = segment[pointIndex];
                if (result[^1] != point) result.Add(point);
            }
        }

        return result;
    }

    internal static IReadOnlyList<IntPoint> ApplySpacing(IReadOnlyList<IntPoint> path, int spacingPixels)
    {
        if (path.Count == 0) return Array.Empty<IntPoint>();
        if (spacingPixels <= 0) throw new ArgumentOutOfRangeException(nameof(spacingPixels));
        if (spacingPixels == 1) return path.ToArray();

        var centers = new List<IntPoint> { path[0] };
        var stepsSinceStamp = 0;
        for (var index = 1; index < path.Count; index++)
        {
            stepsSinceStamp++;
            if (stepsSinceStamp < spacingPixels) continue;
            centers.Add(path[index]);
            stepsSinceStamp = 0;
        }

        if (centers[^1] != path[^1]) centers.Add(path[^1]);
        return centers;
    }
}
