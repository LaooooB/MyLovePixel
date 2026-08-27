using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Raster.Geometry;

public static class LineRasterizer
{
    public static IReadOnlyList<IntPoint> Rasterize(IntPoint start, IntPoint end)
    {
        var points = new List<IntPoint>();
        var x0 = start.X;
        var y0 = start.Y;
        var x1 = end.X;
        var y1 = end.Y;
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;

        while (true)
        {
            points.Add(new IntPoint(x0, y0));
            if (x0 == x1 && y0 == y1) break;

            var twiceError = error * 2;
            if (twiceError >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (twiceError <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }

        return points;
    }
}
