using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Pixel;

public sealed class PixelSurfaceSnapshot
{
    private readonly byte[] _bytes;

    internal PixelSurfaceSnapshot(IntSize size, PixelFormat format, long revision, byte[] bytes)
    {
        Size = size;
        Format = format;
        Revision = revision;
        _bytes = bytes;
    }

    public IntSize Size { get; }
    public PixelFormat Format { get; }
    public long Revision { get; }
    public ReadOnlyMemory<byte> Bytes => _bytes;

    public Rgba32 GetPixel(int x, int y)
    {
        ValidateCoordinates(x, y);
        var index = ((y * Size.Width) + x) * 4;
        return new Rgba32(_bytes[index], _bytes[index + 1], _bytes[index + 2], _bytes[index + 3]);
    }

    private void ValidateCoordinates(int x, int y)
    {
        if ((uint)x >= (uint)Size.Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Size.Height) throw new ArgumentOutOfRangeException(nameof(y));
    }
}
