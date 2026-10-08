using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Render;

public sealed class CpuRenderSurface
{
    private readonly byte[] _pixels;

    internal CpuRenderSurface(IntSize size, byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        var expected = checked(size.Width * size.Height * 4);
        if (pixels.Length != expected)
            throw new ArgumentException($"Render surface byte length must be {expected}.", nameof(pixels));

        Size = size;
        _pixels = pixels;
    }

    public IntSize Size { get; }
    public ReadOnlyMemory<byte> Bytes => _pixels;

    public Rgba32 GetPixel(int x, int y)
    {
        ValidateCoordinates(x, y);
        var offset = ((y * Size.Width) + x) * 4;
        return new Rgba32(
            _pixels[offset],
            _pixels[offset + 1],
            _pixels[offset + 2],
            _pixels[offset + 3]);
    }

    private void ValidateCoordinates(int x, int y)
    {
        if ((uint)x >= (uint)Size.Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Size.Height) throw new ArgumentOutOfRangeException(nameof(y));
    }
}

public interface IRenderTarget
{
    IntSize Size { get; }
    Rgba32 GetPixel(int x, int y);
    void SetPixel(int x, int y, Rgba32 color);
    void Clear(IntRect region);
}

internal sealed class CpuRenderTarget : IRenderTarget
{
    private readonly byte[] _pixels;

    public CpuRenderTarget(IntSize size)
    {
        Size = size;
        _pixels = new byte[checked(size.Width * size.Height * 4)];
    }

    public IntSize Size { get; }

    public Rgba32 GetPixel(int x, int y)
    {
        ValidateCoordinates(x, y);
        var offset = Offset(x, y);
        return new Rgba32(
            _pixels[offset],
            _pixels[offset + 1],
            _pixels[offset + 2],
            _pixels[offset + 3]);
    }

    public void SetPixel(int x, int y, Rgba32 color)
    {
        ValidateCoordinates(x, y);
        var offset = Offset(x, y);
        _pixels[offset] = color.R;
        _pixels[offset + 1] = color.G;
        _pixels[offset + 2] = color.B;
        _pixels[offset + 3] = color.A;
    }

    public void Clear(IntRect region)
    {
        var clipped = RenderMath.Intersect(region, RenderMath.Bounds(Size));
        if (clipped.IsEmpty) return;

        for (var y = clipped.Y; y < clipped.Bottom; y++)
        {
            var start = ((y * Size.Width) + clipped.X) * 4;
            Array.Clear(_pixels, start, clipped.Width * 4);
        }
    }

    public CpuRenderSurface Snapshot() => new(Size, (byte[])_pixels.Clone());

    private int Offset(int x, int y) => ((y * Size.Width) + x) * 4;

    private void ValidateCoordinates(int x, int y)
    {
        if ((uint)x >= (uint)Size.Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Size.Height) throw new ArgumentOutOfRangeException(nameof(y));
    }
}

internal static class RenderMath
{
    public static IntRect Bounds(IntSize size) => new(0, 0, size.Width, size.Height);

    public static IntRect Intersect(IntRect a, IntRect b)
    {
        var left = Math.Max((long)a.X, b.X);
        var top = Math.Max((long)a.Y, b.Y);
        var right = Math.Min((long)a.X + a.Width, (long)b.X + b.Width);
        var bottom = Math.Min((long)a.Y + a.Height, (long)b.Y + b.Height);

        if (right <= left || bottom <= top) return default;

        return new IntRect(
            checked((int)left),
            checked((int)top),
            checked((int)(right - left)),
            checked((int)(bottom - top)));
    }

    public static IntRect TranslateAndClip(IntRect region, IntPoint offset, IntRect bounds)
    {
        var translatedLeft = (long)region.X + offset.X;
        var translatedTop = (long)region.Y + offset.Y;
        var translatedRight = translatedLeft + region.Width;
        var translatedBottom = translatedTop + region.Height;

        var left = Math.Max(translatedLeft, bounds.X);
        var top = Math.Max(translatedTop, bounds.Y);
        var right = Math.Min(translatedRight, (long)bounds.X + bounds.Width);
        var bottom = Math.Min(translatedBottom, (long)bounds.Y + bounds.Height);

        if (right <= left || bottom <= top) return default;

        return new IntRect(
            checked((int)left),
            checked((int)top),
            checked((int)(right - left)),
            checked((int)(bottom - top)));
    }

    public static byte ScaleByte(int value, int factor) =>
        (byte)(((value * factor) + 127) / 255);

    public static Rgba32 SourceOver(Rgba32 destination, Rgba32 source)
    {
        if (source.A == 0) return destination;
        if (source.A == byte.MaxValue || destination.A == 0) return source;

        var inverseSourceAlpha = byte.MaxValue - source.A;
        var alphaNumerator = (source.A * 255) + (destination.A * inverseSourceAlpha);
        if (alphaNumerator == 0) return Rgba32.Transparent;

        var alpha = DivideRounded(alphaNumerator, 255);
        return new Rgba32(
            BlendStraightChannel(destination.R, destination.A, source.R, source.A, inverseSourceAlpha, alphaNumerator),
            BlendStraightChannel(destination.G, destination.A, source.G, source.A, inverseSourceAlpha, alphaNumerator),
            BlendStraightChannel(destination.B, destination.A, source.B, source.A, inverseSourceAlpha, alphaNumerator),
            (byte)alpha);
    }

    private static byte BlendStraightChannel(
        byte destinationChannel,
        byte destinationAlpha,
        byte sourceChannel,
        byte sourceAlpha,
        int inverseSourceAlpha,
        int alphaNumerator)
    {
        var numerator =
            ((long)sourceChannel * sourceAlpha * 255) +
            ((long)destinationChannel * destinationAlpha * inverseSourceAlpha);
        return (byte)DivideRounded(numerator, alphaNumerator);
    }

    private static int DivideRounded(long numerator, long denominator) =>
        checked((int)((numerator + (denominator / 2)) / denominator));
}
