using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Raster.Geometry;

public static class PolygonRasterizer
{
    public static IReadOnlyList<IntPoint> Rasterize(IReadOnlyList<IntPoint> vertices, bool filled)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        if (vertices.Count == 0) return Array.Empty<IntPoint>();
        if (vertices.Count == 1) return [vertices[0]];

        var points = new HashSet<IntPoint>();
        AddOutline(vertices, points);
        if (!filled || vertices.Count < 3) return Order(points);

        var minX = vertices.Min(point => point.X);
        var maxX = vertices.Max(point => point.X);
        var minY = vertices.Min(point => point.Y);
        var maxY = vertices.Max(point => point.Y);

        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var point = new IntPoint(x, y);
            if (points.Contains(point) || IsPixelCenterInside(vertices, x, y)) points.Add(point);
        }

        return Order(points);
    }

    private static void AddOutline(IReadOnlyList<IntPoint> vertices, HashSet<IntPoint> points)
    {
        for (var index = 0; index < vertices.Count; index++)
        {
            var start = vertices[index];
            var end = vertices[(index + 1) % vertices.Count];
            foreach (var point in LineRasterizer.Rasterize(start, end)) points.Add(point);
        }
    }

    private static bool IsPixelCenterInside(IReadOnlyList<IntPoint> vertices, int x, int y)
    {
        var px2 = (Int128)(2L * x + 1);
        var py2 = (Int128)(2L * y + 1);
        var inside = false;

        for (var index = 0; index < vertices.Count; index++)
        {
            var a = vertices[index];
            var b = vertices[(index + 1) % vertices.Count];
            if (a.Y == b.Y) continue;

            var ay2 = (Int128)2 * a.Y;
            var by2 = (Int128)2 * b.Y;
            var crossesY = ay2 <= py2 && py2 < by2 || by2 <= py2 && py2 < ay2;
            if (!crossesY) continue;

            var dx2 = px2 - ((Int128)2 * a.X);
            var dy2 = py2 - ay2;
            var edgeDx = (Int128)b.X - a.X;
            var edgeDy = (Int128)b.Y - a.Y;
            var left = dx2 * edgeDy;
            var right = dy2 * edgeDx;
            var intersectionIsRight = edgeDy > 0 ? right > left : right < left;
            if (intersectionIsRight) inside = !inside;
        }

        return inside;
    }

    private static IReadOnlyList<IntPoint> Order(IEnumerable<IntPoint> points) =>
        points.OrderBy(point => point.Y).ThenBy(point => point.X).ToArray();
}
