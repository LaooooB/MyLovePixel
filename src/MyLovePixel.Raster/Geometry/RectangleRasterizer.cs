using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Raster.Geometry;

public static class RectangleRasterizer
{
    public static IReadOnlyList<IntPoint> Rasterize(IntRect bounds, bool filled)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return Array.Empty<IntPoint>();

        var points = new List<IntPoint>(filled
            ? checked(bounds.Width * bounds.Height)
            : checked(Math.Max(1, (bounds.Width * 2) + (bounds.Height * 2) - 4)));

        if (filled)
        {
            for (var y = bounds.Y; y < bounds.Bottom; y++)
            for (var x = bounds.X; x < bounds.Right; x++)
                points.Add(new IntPoint(x, y));
            return points;
        }

        for (var x = bounds.X; x < bounds.Right; x++)
            points.Add(new IntPoint(x, bounds.Y));

        if (bounds.Height > 1)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
                points.Add(new IntPoint(x, bounds.Bottom - 1));
        }

        for (var y = bounds.Y + 1; y < bounds.Bottom - 1; y++)
        {
            points.Add(new IntPoint(bounds.X, y));
            if (bounds.Width > 1) points.Add(new IntPoint(bounds.Right - 1, y));
        }

        return points;
    }
}
