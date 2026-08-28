using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Render;

public readonly record struct ViewPoint(double X, double Y);

public readonly record struct ViewRect
{
    public ViewRect(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x));
        if (!double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(y));
        if (!double.IsFinite(width) || width < 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (!double.IsFinite(height) || height < 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (!double.IsFinite(x + width)) throw new ArgumentOutOfRangeException(nameof(width));
        if (!double.IsFinite(y + height)) throw new ArgumentOutOfRangeException(nameof(height));

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }
    public bool IsEmpty => Width == 0 || Height == 0;
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

public sealed class ViewTransform
{
    public ViewTransform(double scale, double offsetX = 0, double offsetY = 0)
    {
        if (!double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale));
        if (!double.IsFinite(offsetX))
            throw new ArgumentOutOfRangeException(nameof(offsetX));
        if (!double.IsFinite(offsetY))
            throw new ArgumentOutOfRangeException(nameof(offsetY));

        Scale = scale;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    public double Scale { get; }
    public double OffsetX { get; }
    public double OffsetY { get; }

    public ViewPoint CanvasToView(double canvasX, double canvasY) =>
        new(
            (canvasX * Scale) + OffsetX,
            (canvasY * Scale) + OffsetY);

    public ViewPoint ViewToCanvas(double viewX, double viewY) =>
        new(
            (viewX - OffsetX) / Scale,
            (viewY - OffsetY) / Scale);

    public ViewRect CanvasRectToView(IntRect rectangle)
    {
        var topLeft = CanvasToView(rectangle.X, rectangle.Y);
        return new ViewRect(
            topLeft.X,
            topLeft.Y,
            rectangle.Width * Scale,
            rectangle.Height * Scale);
    }

    public IntPoint ViewToCanvasPixel(double viewX, double viewY)
    {
        var canvas = ViewToCanvas(viewX, viewY);
        var x = Math.Floor(canvas.X);
        var y = Math.Floor(canvas.Y);
        if (x < int.MinValue || x > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(viewX));
        if (y < int.MinValue || y > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(viewY));

        return new IntPoint((int)x, (int)y);
    }

    public IntRect GetVisibleCanvasRegion(
        IntSize canvasSize,
        ViewRect? viewport = null)
    {
        if (viewport is null) return RenderMath.Bounds(canvasSize);
        if (viewport.Value.IsEmpty) return default;

        var a = ViewToCanvas(viewport.Value.X, viewport.Value.Y);
        var b = ViewToCanvas(viewport.Value.Right, viewport.Value.Bottom);

        var left = Math.Max(0, Math.Floor(Math.Min(a.X, b.X)));
        var top = Math.Max(0, Math.Floor(Math.Min(a.Y, b.Y)));
        var right = Math.Min(canvasSize.Width, Math.Ceiling(Math.Max(a.X, b.X)));
        var bottom = Math.Min(canvasSize.Height, Math.Ceiling(Math.Max(a.Y, b.Y)));

        if (right <= left || bottom <= top) return default;

        return new IntRect(
            (int)left,
            (int)top,
            (int)(right - left),
            (int)(bottom - top));
    }
}
