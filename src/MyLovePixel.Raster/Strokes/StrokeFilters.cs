using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Raster.Strokes;

public interface IStrokeFilter
{
    IReadOnlyList<IntPoint> Filter(IReadOnlyList<IntPoint> points);
}

public sealed class IdentityStrokeFilter : IStrokeFilter
{
    public static IdentityStrokeFilter Instance { get; } = new();

    private IdentityStrokeFilter()
    {
    }

    public IReadOnlyList<IntPoint> Filter(IReadOnlyList<IntPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        return points;
    }
}

public sealed class PixelPerfectStrokeFilter : IStrokeFilter
{
    public static PixelPerfectStrokeFilter Instance { get; } = new();

    private PixelPerfectStrokeFilter()
    {
    }

    public IReadOnlyList<IntPoint> Filter(IReadOnlyList<IntPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count <= 2) return points.ToArray();

        var result = new List<IntPoint>(points.Count);
        foreach (var point in points)
        {
            if (result.Count > 0 && result[^1] == point) continue;
            result.Add(point);

            while (result.Count >= 3)
            {
                var a = result[^3];
                var b = result[^2];
                var c = result[^1];
                if (!IsRedundantOrthogonalCorner(a, b, c)) break;
                result.RemoveAt(result.Count - 2);
            }
        }

        return result;
    }

    private static bool IsRedundantOrthogonalCorner(IntPoint a, IntPoint b, IntPoint c)
    {
        var abX = Math.Abs(a.X - b.X);
        var abY = Math.Abs(a.Y - b.Y);
        var bcX = Math.Abs(b.X - c.X);
        var bcY = Math.Abs(b.Y - c.Y);
        var acX = Math.Abs(a.X - c.X);
        var acY = Math.Abs(a.Y - c.Y);

        return abX + abY == 1 &&
               bcX + bcY == 1 &&
               acX == 1 && acY == 1 &&
               (abX != bcX || abY != bcY);
    }
}
