using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Selection;

public sealed class FloatingContent
{
    private readonly Rgba32[] _pixels;

    internal FloatingContent(IntSize size, IntPoint position, Rgba32[] pixels, SelectionMask mask)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(mask);
        if (pixels.Length != checked(size.Width * size.Height))
            throw new ArgumentException("Floating pixel buffer size does not match dimensions.", nameof(pixels));
        if (mask.Size != size) throw new ArgumentException("Floating mask size must match content size.", nameof(mask));

        Size = size;
        Position = position;
        _pixels = (Rgba32[])pixels.Clone();
        Mask = SelectionMask.FromCoverage(mask.Size, mask.Format, mask.CopyCoverage());
    }

    public IntSize Size { get; }
    public IntPoint Position { get; }
    public SelectionMask Mask { get; }

    public static FloatingContent Capture(PixelSurfaceSnapshot surface, SelectionMask selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);
        if (surface.Size != selection.Size)
            throw new ArgumentException("Selection size must match source surface size.", nameof(selection));
        if (selection.IsEmpty) throw new InvalidOperationException("Cannot capture floating content from an empty selection.");

        var bounds = selection.Bounds;
        var size = new IntSize(bounds.Width, bounds.Height);
        var pixels = new Rgba32[checked(size.Width * size.Height)];
        var coverage = new byte[pixels.Length];

        for (var y = 0; y < size.Height; y++)
        for (var x = 0; x < size.Width; x++)
        {
            var sourceX = bounds.X + x;
            var sourceY = bounds.Y + y;
            var index = (y * size.Width) + x;
            pixels[index] = surface.GetPixel(sourceX, sourceY);
            coverage[index] = selection.GetCoverage(sourceX, sourceY);
        }

        var localMask = SelectionMask.FromCoverage(size, selection.Format, coverage);
        return new FloatingContent(size, new IntPoint(bounds.X, bounds.Y), pixels, localMask);
    }

    public Rgba32 GetPixel(int x, int y)
    {
        if ((uint)x >= (uint)Size.Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Size.Height) throw new ArgumentOutOfRangeException(nameof(y));
        return _pixels[(y * Size.Width) + x];
    }

    internal Rgba32[] CopyPixels() => (Rgba32[])_pixels.Clone();
}

public interface IArbitraryRotationStrategy
{
    FloatingContent Rotate(FloatingContent source, double degrees);
}

public static class FloatingContentTransforms
{
    public static FloatingContent Translate(FloatingContent source, IntPoint delta)
    {
        ArgumentNullException.ThrowIfNull(source);
        var position = new IntPoint(
            checked(source.Position.X + delta.X),
            checked(source.Position.Y + delta.Y));
        return new FloatingContent(source.Size, position, source.CopyPixels(), source.Mask);
    }

    public static FloatingContent FlipHorizontal(FloatingContent source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Remap(source, source.Size, (x, y) => new IntPoint(source.Size.Width - 1 - x, y));
    }

    public static FloatingContent FlipVertical(FloatingContent source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Remap(source, source.Size, (x, y) => new IntPoint(x, source.Size.Height - 1 - y));
    }

    public static FloatingContent Rotate90(FloatingContent source, QuarterTurn turn)
    {
        ArgumentNullException.ThrowIfNull(source);
        var targetSize = new IntSize(source.Size.Height, source.Size.Width);
        return Remap(
            source,
            targetSize,
            (x, y) => turn switch
            {
                QuarterTurn.Clockwise => new IntPoint(source.Size.Height - 1 - y, x),
                QuarterTurn.CounterClockwise => new IntPoint(y, source.Size.Width - 1 - x),
                _ => throw new ArgumentOutOfRangeException(nameof(turn)),
            });
    }

    public static FloatingContent ScaleNearest(FloatingContent source, IntSize targetSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        var pixels = new Rgba32[checked(targetSize.Width * targetSize.Height)];
        var coverage = new byte[pixels.Length];

        for (var y = 0; y < targetSize.Height; y++)
        for (var x = 0; x < targetSize.Width; x++)
        {
            var sourceX = (int)(((long)x * source.Size.Width) / targetSize.Width);
            var sourceY = (int)(((long)y * source.Size.Height) / targetSize.Height);
            var targetIndex = (y * targetSize.Width) + x;
            pixels[targetIndex] = source.GetPixel(sourceX, sourceY);
            coverage[targetIndex] = source.Mask.GetCoverage(sourceX, sourceY);
        }

        var mask = SelectionMask.FromCoverage(targetSize, source.Mask.Format, coverage);
        return new FloatingContent(targetSize, source.Position, pixels, mask);
    }

    private static FloatingContent Remap(
        FloatingContent source,
        IntSize targetSize,
        Func<int, int, IntPoint> map)
    {
        var pixels = new Rgba32[checked(targetSize.Width * targetSize.Height)];
        var coverage = new byte[pixels.Length];

        for (var y = 0; y < source.Size.Height; y++)
        for (var x = 0; x < source.Size.Width; x++)
        {
            var target = map(x, y);
            var targetIndex = (target.Y * targetSize.Width) + target.X;
            pixels[targetIndex] = source.GetPixel(x, y);
            coverage[targetIndex] = source.Mask.GetCoverage(x, y);
        }

        var mask = SelectionMask.FromCoverage(targetSize, source.Mask.Format, coverage);
        return new FloatingContent(targetSize, source.Position, pixels, mask);
    }
}
