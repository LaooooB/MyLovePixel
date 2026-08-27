using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Raster.Color;
using MyLovePixel.Raster.Geometry;

namespace MyLovePixel.Selection;

public static class SelectionFactory
{
    public static SelectionMask Rectangle(
        IntSize canvasSize,
        IntRect bounds,
        SelectionMaskFormat format = SelectionMaskFormat.Bit1) =>
        FromPoints(canvasSize, RectangleRasterizer.Rasterize(bounds, filled: true), format);

    public static SelectionMask Ellipse(
        IntSize canvasSize,
        IntRect bounds,
        SelectionMaskFormat format = SelectionMaskFormat.Bit1) =>
        FromPoints(canvasSize, EllipseRasterizer.Rasterize(bounds, filled: true), format);

    public static SelectionMask Lasso(
        IntSize canvasSize,
        IReadOnlyList<IntPoint> vertices,
        SelectionMaskFormat format = SelectionMaskFormat.Bit1)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        return FromPoints(canvasSize, PolygonRasterizer.Rasterize(vertices, filled: true), format);
    }

    public static SelectionMask ByColor(
        PixelSurfaceSnapshot surface,
        Rgba32 reference,
        IColorToleranceStrategy? tolerance = null,
        SelectionMaskFormat format = SelectionMaskFormat.Bit1)
    {
        ArgumentNullException.ThrowIfNull(surface);
        tolerance ??= ExactColorTolerance.Instance;
        var coverage = new byte[checked(surface.Size.Width * surface.Size.Height)];

        for (var y = 0; y < surface.Size.Height; y++)
        for (var x = 0; x < surface.Size.Width; x++)
        {
            if (tolerance.Matches(reference, surface.GetPixel(x, y)))
                coverage[(y * surface.Size.Width) + x] = byte.MaxValue;
        }

        return SelectionMask.FromCoverage(surface.Size, format, coverage);
    }

    private static SelectionMask FromPoints(IntSize canvasSize, IEnumerable<IntPoint> points, SelectionMaskFormat format)
    {
        var coverage = new byte[checked(canvasSize.Width * canvasSize.Height)];
        foreach (var point in points)
        {
            if ((uint)point.X >= (uint)canvasSize.Width || (uint)point.Y >= (uint)canvasSize.Height) continue;
            coverage[(point.Y * canvasSize.Width) + point.X] = byte.MaxValue;
        }
        return SelectionMask.FromCoverage(canvasSize, format, coverage);
    }
}
