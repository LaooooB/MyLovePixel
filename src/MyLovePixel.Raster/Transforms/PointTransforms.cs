using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Raster.Transforms;

public interface IPointTransform
{
    IReadOnlyList<IntPoint> Transform(IntPoint point);
}

public sealed class IdentityPointTransform : IPointTransform
{
    public static IdentityPointTransform Instance { get; } = new();

    private IdentityPointTransform()
    {
    }

    public IReadOnlyList<IntPoint> Transform(IntPoint point) => [point];
}

public sealed class MirrorPointTransform : IPointTransform
{
    public MirrorPointTransform(int? verticalAxisXTwice = null, int? horizontalAxisYTwice = null)
    {
        if (verticalAxisXTwice is null && horizontalAxisYTwice is null)
            throw new ArgumentException("At least one mirror axis must be configured.");

        VerticalAxisXTwice = verticalAxisXTwice;
        HorizontalAxisYTwice = horizontalAxisYTwice;
    }

    public int? VerticalAxisXTwice { get; }
    public int? HorizontalAxisYTwice { get; }

    public IReadOnlyList<IntPoint> Transform(IntPoint point)
    {
        var result = new List<IntPoint>(4) { point };

        IntPoint? mirroredX = null;
        IntPoint? mirroredY = null;
        if (VerticalAxisXTwice is { } xAxis)
        {
            mirroredX = new IntPoint(checked(xAxis - point.X), point.Y);
            AddUnique(result, mirroredX.Value);
        }

        if (HorizontalAxisYTwice is { } yAxis)
        {
            mirroredY = new IntPoint(point.X, checked(yAxis - point.Y));
            AddUnique(result, mirroredY.Value);
        }

        if (mirroredX is { } x && mirroredY is not null)
            AddUnique(result, new IntPoint(x.X, mirroredY.Value.Y));

        return result;
    }

    private static void AddUnique(List<IntPoint> points, IntPoint candidate)
    {
        if (!points.Contains(candidate)) points.Add(candidate);
    }
}
