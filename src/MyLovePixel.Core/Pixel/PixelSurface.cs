using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Pixel;

public sealed class PixelSurface
{
    private readonly byte[] _pixels;

    public PixelSurface(IntSize size, Rgba32? fill = null)
    {
        Size = size;
        Format = PixelFormat.Rgba32;
        _pixels = GC.AllocateUninitializedArray<byte>(checked(size.Width * size.Height * 4));

        var initial = fill ?? Rgba32.Transparent;
        if (initial != Rgba32.Transparent)
        {
            for (var y = 0; y < size.Height; y++)
            for (var x = 0; x < size.Width; x++)
                WriteUnchecked(x, y, initial);
        }
        else
        {
            Array.Clear(_pixels, 0, _pixels.Length);
        }
    }

    private PixelSurface(IntSize size, PixelFormat format, byte[] pixels, long revision)
    {
        Size = size;
        Format = format;
        _pixels = pixels;
        Revision = revision;
    }

    public IntSize Size { get; }
    public PixelFormat Format { get; }
    public long Revision { get; private set; }

    public Rgba32 GetPixel(int x, int y)
    {
        ValidateCoordinates(x, y);
        var index = Offset(x, y);
        return new Rgba32(_pixels[index], _pixels[index + 1], _pixels[index + 2], _pixels[index + 3]);
    }

    public PixelSurfaceSnapshot Snapshot() => new(Size, Format, Revision, (byte[])_pixels.Clone());

    public PixelSurface Clone() => new(Size, Format, (byte[])_pixels.Clone(), Revision);

    internal static PixelSurface FromRgbaBytes(IntSize size, ReadOnlySpan<byte> bytes)
    {
        var expectedLength = checked(size.Width * size.Height * 4);
        if (bytes.Length != expectedLength)
            throw new ArgumentException($"RGBA byte length must be {expectedLength}, received {bytes.Length}.", nameof(bytes));

        return new PixelSurface(size, PixelFormat.Rgba32, bytes.ToArray(), revision: 0);
    }

    internal void SetPixel(int x, int y, Rgba32 color)
    {
        ValidateCoordinates(x, y);
        WriteUnchecked(x, y, color);
        Revision = checked(Revision + 1);
    }

    internal void SetPixels(ReadOnlySpan<PixelWrite> writes)
    {
        if (writes.IsEmpty) return;

        foreach (var write in writes)
        {
            ValidateCoordinates(write.X, write.Y);
            WriteUnchecked(write.X, write.Y, write.Color);
        }

        Revision = checked(Revision + 1);
    }

    private void WriteUnchecked(int x, int y, Rgba32 color)
    {
        var index = Offset(x, y);
        _pixels[index] = color.R;
        _pixels[index + 1] = color.G;
        _pixels[index + 2] = color.B;
        _pixels[index + 3] = color.A;
    }

    private int Offset(int x, int y) => ((y * Size.Width) + x) * 4;

    private void ValidateCoordinates(int x, int y)
    {
        if ((uint)x >= (uint)Size.Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Size.Height) throw new ArgumentOutOfRangeException(nameof(y));
    }
}

public readonly record struct PixelWrite(int X, int Y, Rgba32 Color);
