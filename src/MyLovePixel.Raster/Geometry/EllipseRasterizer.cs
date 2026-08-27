using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Raster.Geometry;

public static class EllipseRasterizer
{
    public static IReadOnlyList<IntPoint> Rasterize(IntRect bounds, bool filled)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return Array.Empty<IntPoint>();

        var points = new List<IntPoint>();
        for (var y = bounds.Y; y < bounds.Bottom; y++)
        for (var x = bounds.X; x < bounds.Right; x++)
        {
            if (!IsInside(bounds, x, y)) continue;
            if (filled || IsBoundary(bounds, x, y)) points.Add(new IntPoint(x, y));
        }

        return points;
    }

    private static bool IsBoundary(IntRect bounds, int x, int y) =>
        !IsInside(bounds, x - 1, y) ||
        !IsInside(bounds, x + 1, y) ||
        !IsInside(bounds, x, y - 1) ||
        !IsInside(bounds, x, y + 1);

    private static bool IsInside(IntRect bounds, int x, int y)
    {
        if (x < bounds.X || x >= bounds.Right || y < bounds.Y || y >= bounds.Bottom) return false;

        var dx2 = (Int128)(2L * x) - ((Int128)(2L * bounds.X) + bounds.Width - 1);
        var dy2 = (Int128)(2L * y) - ((Int128)(2L * bounds.Y) + bounds.Height - 1);
        var width = (Int128)bounds.Width;
        var height = (Int128)bounds.Height;
        var widthSquared = width * width;
        var heightSquared = height * height;

        return (dx2 * dx2 * heightSquared) + (dy2 * dy2 * widthSquared)
               <= widthSquared * heightSquared;
    }
}
