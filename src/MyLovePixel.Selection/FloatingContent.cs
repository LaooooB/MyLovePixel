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
    public const int Direction16Count = 16;
    public const double Direction16StepDegrees = 360d / Direction16Count;

    public static FloatingContent Place(FloatingContent source, IntPoint position)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new FloatingContent(source.Size, position, source.CopyPixels(), source.Mask);
    }

    public static FloatingContent Translate(FloatingContent source, IntPoint delta)
    {
        ArgumentNullException.ThrowIfNull(source);
        var position = new IntPoint(
            checked(source.Position.X + delta.X),
            checked(source.Position.Y + delta.Y));
        return Place(source, position);
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

    public static int QuantizeDirection16(double degrees)
    {
        if (!double.IsFinite(degrees)) throw new ArgumentOutOfRangeException(nameof(degrees));
        var normalized = NormalizeDegrees(degrees);
        var steps = checked((int)Math.Round(
            normalized / Direction16StepDegrees,
            MidpointRounding.AwayFromZero));
        return NormalizeDirection16Index(steps);
    }

    public static double Direction16Degrees(int directionIndex)
    {
        var normalized = NormalizeDirection16Index(directionIndex);
        var degrees = normalized * Direction16StepDegrees;
        return degrees > 180d ? degrees - 360d : degrees;
    }

    public static FloatingContent RotateDirection16(FloatingContent source, int directionIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        var normalized = NormalizeDirection16Index(directionIndex);
        if (normalized == 0) return Place(source, source.Position);

        var radians = normalized * (2d * Math.PI / Direction16Count);
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        // Remove floating-point residue on the four cardinal axes. The other twelve
        // directions intentionally use the full 22.5-degree trigonometric values.
        switch (normalized)
        {
            case 4:
                cos = 0d;
                sin = 1d;
                break;
            case 8:
                cos = -1d;
                sin = 0d;
                break;
            case 12:
                cos = 0d;
                sin = -1d;
                break;
        }

        return RotateNearestCore(source, cos, sin);
    }

    public static FloatingContent RotateNearest(FloatingContent source, double degrees)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!double.IsFinite(degrees)) throw new ArgumentOutOfRangeException(nameof(degrees));

        var normalized = NormalizeDegrees(degrees);
        if (Math.Abs(normalized) < 0.000001d)
            return Place(source, source.Position);

        var radians = normalized * Math.PI / 180d;
        return RotateNearestCore(source, Math.Cos(radians), Math.Sin(radians));
    }

    private static FloatingContent RotateNearestCore(FloatingContent source, double cos, double sin)
    {
        var width = Math.Max(1, (int)Math.Ceiling(
            Math.Abs(source.Size.Width * cos) + Math.Abs(source.Size.Height * sin) - 0.000000001d));
        var height = Math.Max(1, (int)Math.Ceiling(
            Math.Abs(source.Size.Width * sin) + Math.Abs(source.Size.Height * cos) - 0.000000001d));
        var targetSize = new IntSize(width, height);
        var pixels = new Rgba32[checked(width * height)];
        var coverage = new byte[pixels.Length];

        var sourceCenterX = (source.Size.Width - 1) * 0.5d;
        var sourceCenterY = (source.Size.Height - 1) * 0.5d;
        var targetCenterX = (width - 1) * 0.5d;
        var targetCenterY = (height - 1) * 0.5d;

        // Inverse-map every target pixel to its nearest source pixel. Forward mapping
        // creates holes at 22.5/45/67.5 degrees, which is especially visible in pixel art.
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var dx = x - targetCenterX;
            var dy = y - targetCenterY;
            var sourceX = (cos * dx) + (sin * dy) + sourceCenterX;
            var sourceY = (-sin * dx) + (cos * dy) + sourceCenterY;
            var nearestX = (int)Math.Round(sourceX, MidpointRounding.AwayFromZero);
            var nearestY = (int)Math.Round(sourceY, MidpointRounding.AwayFromZero);
            if ((uint)nearestX >= (uint)source.Size.Width || (uint)nearestY >= (uint)source.Size.Height) continue;

            var targetIndex = (y * width) + x;
            pixels[targetIndex] = source.GetPixel(nearestX, nearestY);
            coverage[targetIndex] = source.Mask.GetCoverage(nearestX, nearestY);
        }

        var worldCenterX = source.Position.X + sourceCenterX;
        var worldCenterY = source.Position.Y + sourceCenterY;
        var position = new IntPoint(
            checked((int)Math.Round(worldCenterX - targetCenterX, MidpointRounding.AwayFromZero)),
            checked((int)Math.Round(worldCenterY - targetCenterY, MidpointRounding.AwayFromZero)));
        var mask = SelectionMask.FromCoverage(targetSize, source.Mask.Format, coverage);
        return new FloatingContent(targetSize, position, pixels, mask);
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

    private static int NormalizeDirection16Index(int directionIndex)
    {
        var result = directionIndex % Direction16Count;
        return result < 0 ? result + Direction16Count : result;
    }

    private static double NormalizeDegrees(double degrees)
    {
        var result = degrees % 360d;
        if (result <= -180d) result += 360d;
        if (result > 180d) result -= 360d;
        return result;
    }
}
