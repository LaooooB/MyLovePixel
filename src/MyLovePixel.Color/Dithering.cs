using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Color;

public sealed class OrderedDitherMatrix
{
    private readonly byte[] _thresholds;

    public OrderedDitherMatrix(int width, int height, IEnumerable<byte> thresholds)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        ArgumentNullException.ThrowIfNull(thresholds);
        _thresholds = thresholds.ToArray();
        if (_thresholds.Length != checked(width * height))
            throw new ArgumentException("Dither matrix threshold count must match width * height.", nameof(thresholds));
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<byte> Thresholds => Array.AsReadOnly(_thresholds);

    public byte GetThreshold(int x, int y)
    {
        var wrappedX = Mod(x, Width);
        var wrappedY = Mod(y, Height);
        return _thresholds[(wrappedY * Width) + wrappedX];
    }

    public static OrderedDitherMatrix Bayer2x2 { get; } = new(
        2,
        2,
        [0, 128, 192, 64]);

    public static OrderedDitherMatrix Bayer4x4 { get; } = new(
        4,
        4,
        [
            0, 128, 32, 160,
            192, 64, 224, 96,
            48, 176, 16, 144,
            240, 112, 208, 80,
        ]);

    private static int Mod(int value, int divisor)
    {
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }
}

public sealed record DitherOptions
{
    public DitherOptions(OrderedDitherMatrix matrix, int strength = 64)
    {
        Matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
        if (strength is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(strength));
        Strength = strength;
    }

    public OrderedDitherMatrix Matrix { get; }
    public int Strength { get; }
}

public sealed class DitheredImage
{
    private readonly byte[] _indices;

    internal DitheredImage(IntSize size, byte[] indices)
    {
        if (indices.Length != checked(size.Width * size.Height))
            throw new ArgumentException("Index count must match image dimensions.", nameof(indices));
        Size = size;
        _indices = indices;
    }

    public IntSize Size { get; }
    public ReadOnlyMemory<byte> Indices => _indices;
}

public interface IDitherStrategy
{
    DitheredImage Dither(
        PixelSurfaceSnapshot source,
        PaletteSnapshot palette,
        DitherOptions options);
}

public sealed class OrderedPaletteDitherStrategy : IDitherStrategy
{
    public static OrderedPaletteDitherStrategy Instance { get; } = new();

    private OrderedPaletteDitherStrategy()
    {
    }

    public DitheredImage Dither(
        PixelSurfaceSnapshot source,
        PaletteSnapshot palette,
        DitherOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(options);
        if (source.Format != PixelFormat.Rgba32)
            throw new ArgumentException("Dithering requires an RGBA32 source snapshot.", nameof(source));

        var result = new byte[checked(source.Size.Width * source.Size.Height)];
        var bytes = source.Bytes.Span;
        for (var y = 0; y < source.Size.Height; y++)
        for (var x = 0; x < source.Size.Width; x++)
        {
            var pixel = (y * source.Size.Width) + x;
            var offset = pixel * 4;
            var sourceColor = new Rgba32(
                bytes[offset],
                bytes[offset + 1],
                bytes[offset + 2],
                bytes[offset + 3]);

            if (sourceColor.A == 0 && palette.TransparentIndex is { } transparentIndex)
            {
                result[pixel] = transparentIndex;
                continue;
            }

            var threshold = options.Matrix.GetThreshold(x, y);
            var bias = ((threshold - 128) * options.Strength) / 255;
            var adjusted = new Rgba32(
                ClampToByte(sourceColor.R + bias),
                ClampToByte(sourceColor.G + bias),
                ClampToByte(sourceColor.B + bias),
                sourceColor.A);
            result[pixel] = PaletteMatcher.FindNearestIndex(palette, adjusted);
        }

        return new DitheredImage(source.Size, result);
    }

    private static byte ClampToByte(int value) =>
        checked((byte)Math.Clamp(value, byte.MinValue, byte.MaxValue));
}
