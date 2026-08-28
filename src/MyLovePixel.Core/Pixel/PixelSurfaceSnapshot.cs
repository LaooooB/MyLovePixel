using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Pixel;

public sealed class PixelSurfaceSnapshot
{
    private readonly byte[] _bytes;

    internal PixelSurfaceSnapshot(
        IntSize size,
        PixelFormat format,
        PaletteId? paletteId,
        long revision,
        byte[] bytes)
    {
        Size = size;
        Format = format;
        PaletteId = paletteId;
        Revision = revision;
        _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        ValidateStorageInvariant();
    }

    public IntSize Size { get; }
    public PixelFormat Format { get; }
    public PaletteId? PaletteId { get; }
    public long Revision { get; }
    public ReadOnlyMemory<byte> Bytes => _bytes;

    public Rgba32 GetPixel(int x, int y)
    {
        EnsureFormat(PixelFormat.Rgba32);
        ValidateCoordinates(x, y);
        var index = checked(((y * Size.Width) + x) * 4);
        return new Rgba32(_bytes[index], _bytes[index + 1], _bytes[index + 2], _bytes[index + 3]);
    }

    public byte GetIndex(int x, int y)
    {
        EnsureFormat(PixelFormat.Indexed8);
        ValidateCoordinates(x, y);
        return _bytes[checked((y * Size.Width) + x)];
    }

    private void EnsureFormat(PixelFormat expected)
    {
        if (Format != expected)
            throw new InvalidOperationException($"Operation requires pixel format '{expected}', but snapshot format is '{Format}'.");
    }

    private void ValidateCoordinates(int x, int y)
    {
        if ((uint)x >= (uint)Size.Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Size.Height) throw new ArgumentOutOfRangeException(nameof(y));
    }

    private void ValidateStorageInvariant()
    {
        if (Revision < 0) throw new ArgumentOutOfRangeException(nameof(Revision));
        var expectedLength = Format switch
        {
            PixelFormat.Rgba32 => checked(Size.Width * Size.Height * 4),
            PixelFormat.Indexed8 => checked(Size.Width * Size.Height),
            _ => throw new ArgumentOutOfRangeException(nameof(Format), $"Unsupported pixel format '{Format}'."),
        };
        if (_bytes.Length != expectedLength)
            throw new ArgumentException("Pixel storage length does not match surface dimensions and format.", nameof(_bytes));
        if (Format == PixelFormat.Rgba32 && PaletteId is not null)
            throw new ArgumentException("RGBA32 snapshots cannot reference a palette.", nameof(PaletteId));
        if (Format == PixelFormat.Indexed8 && (PaletteId is null || PaletteId.Value.Value == Guid.Empty))
            throw new ArgumentException("Indexed8 snapshots must reference a non-empty palette.", nameof(PaletteId));
    }
}
