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
                WriteRgbaUnchecked(x, y, initial);
        }
        else
        {
            Array.Clear(_pixels, 0, _pixels.Length);
        }
    }

    private PixelSurface(
        IntSize size,
        PixelFormat format,
        PaletteId? paletteId,
        byte[] pixels,
        long revision)
    {
        Size = size;
        Format = format;
        PaletteId = paletteId;
        _pixels = pixels;
        Revision = revision;
        ValidateStorageInvariant();
    }

    public IntSize Size { get; }
    public PixelFormat Format { get; }
    public PaletteId? PaletteId { get; }
    public long Revision { get; private set; }

    public static PixelSurface CreateIndexed(
        IntSize size,
        PaletteId paletteId,
        byte fillIndex = 0)
    {
        if (paletteId.Value == Guid.Empty)
            throw new ArgumentException("PaletteId cannot be empty.", nameof(paletteId));
        var pixels = new byte[checked(size.Width * size.Height)];
        if (fillIndex != 0) Array.Fill(pixels, fillIndex);
        return new PixelSurface(size, PixelFormat.Indexed8, paletteId, pixels, revision: 0);
    }

    public Rgba32 GetPixel(int x, int y)
    {
        EnsureFormat(PixelFormat.Rgba32);
        ValidateCoordinates(x, y);
        var index = RgbaOffset(x, y);
        return new Rgba32(_pixels[index], _pixels[index + 1], _pixels[index + 2], _pixels[index + 3]);
    }

    public byte GetIndex(int x, int y)
    {
        EnsureFormat(PixelFormat.Indexed8);
        ValidateCoordinates(x, y);
        return _pixels[IndexOffset(x, y)];
    }

    public PixelSurfaceSnapshot Snapshot() =>
        new(Size, Format, PaletteId, Revision, (byte[])_pixels.Clone());

    public PixelSurface Clone() =>
        new(Size, Format, PaletteId, (byte[])_pixels.Clone(), Revision);

    internal static PixelSurface FromRgbaBytes(IntSize size, ReadOnlySpan<byte> bytes)
    {
        var expectedLength = checked(size.Width * size.Height * 4);
        if (bytes.Length != expectedLength)
            throw new ArgumentException($"RGBA byte length must be {expectedLength}, received {bytes.Length}.", nameof(bytes));

        return new PixelSurface(size, PixelFormat.Rgba32, null, bytes.ToArray(), revision: 0);
    }

    internal static PixelSurface FromIndexedBytes(
        IntSize size,
        PaletteId paletteId,
        ReadOnlySpan<byte> bytes)
    {
        if (paletteId.Value == Guid.Empty)
            throw new ArgumentException("PaletteId cannot be empty.", nameof(paletteId));
        var expectedLength = checked(size.Width * size.Height);
        if (bytes.Length != expectedLength)
            throw new ArgumentException($"Indexed8 byte length must be {expectedLength}, received {bytes.Length}.", nameof(bytes));

        return new PixelSurface(size, PixelFormat.Indexed8, paletteId, bytes.ToArray(), revision: 0);
    }

    internal void SetPixel(int x, int y, Rgba32 color)
    {
        EnsureFormat(PixelFormat.Rgba32);
        ValidateCoordinates(x, y);
        var nextRevision = checked(Revision + 1);
        WriteRgbaUnchecked(x, y, color);
        Revision = nextRevision;
    }

    internal void SetPixels(ReadOnlySpan<PixelWrite> writes)
    {
        EnsureFormat(PixelFormat.Rgba32);
        if (writes.IsEmpty) return;

        foreach (var write in writes) ValidateCoordinates(write.X, write.Y);
        var nextRevision = checked(Revision + 1);

        foreach (var write in writes) WriteRgbaUnchecked(write.X, write.Y, write.Color);
        Revision = nextRevision;
    }

    internal void SetIndex(int x, int y, byte index)
    {
        EnsureFormat(PixelFormat.Indexed8);
        ValidateCoordinates(x, y);
        var nextRevision = checked(Revision + 1);
        _pixels[IndexOffset(x, y)] = index;
        Revision = nextRevision;
    }

    internal void SetIndices(ReadOnlySpan<IndexedPixelWrite> writes)
    {
        EnsureFormat(PixelFormat.Indexed8);
        if (writes.IsEmpty) return;

        foreach (var write in writes) ValidateCoordinates(write.X, write.Y);
        var nextRevision = checked(Revision + 1);

        foreach (var write in writes)
            _pixels[IndexOffset(write.X, write.Y)] = write.Index;
        Revision = nextRevision;
    }

    internal void ReplaceIndices(ReadOnlySpan<byte> indices)
    {
        EnsureFormat(PixelFormat.Indexed8);
        if (indices.Length != _pixels.Length)
            throw new ArgumentException($"Indexed8 byte length must be {_pixels.Length}, received {indices.Length}.", nameof(indices));
        var nextRevision = checked(Revision + 1);
        indices.CopyTo(_pixels);
        Revision = nextRevision;
    }

    private void WriteRgbaUnchecked(int x, int y, Rgba32 color)
    {
        var index = RgbaOffset(x, y);
        _pixels[index] = color.R;
        _pixels[index + 1] = color.G;
        _pixels[index + 2] = color.B;
        _pixels[index + 3] = color.A;
    }

    private int RgbaOffset(int x, int y) => checked(((y * Size.Width) + x) * 4);
    private int IndexOffset(int x, int y) => checked((y * Size.Width) + x);

    private void EnsureFormat(PixelFormat expected)
    {
        if (Format != expected)
            throw new InvalidOperationException($"Operation requires pixel format '{expected}', but surface format is '{Format}'.");
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
        if (_pixels.Length != expectedLength)
            throw new ArgumentException("Pixel storage length does not match surface dimensions and format.", nameof(_pixels));
        if (Format == PixelFormat.Rgba32 && PaletteId is not null)
            throw new ArgumentException("RGBA32 surfaces cannot reference a palette.", nameof(PaletteId));
        if (Format == PixelFormat.Indexed8 && (PaletteId is null || PaletteId.Value.Value == Guid.Empty))
            throw new ArgumentException("Indexed8 surfaces must reference a non-empty palette.", nameof(PaletteId));
    }
}

public readonly record struct PixelWrite(int X, int Y, Rgba32 Color);
public readonly record struct IndexedPixelWrite(int X, int Y, byte Index);
