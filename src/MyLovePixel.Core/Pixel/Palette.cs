using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Pixel;

public sealed class Palette
{
    private readonly Rgba32[] _colors;

    public Palette(IEnumerable<Rgba32> colors, byte? transparentIndex = null)
    {
        ArgumentNullException.ThrowIfNull(colors);
        _colors = colors.ToArray();
        ValidateColorCount(_colors.Length);
        ValidateTransparentIndex(transparentIndex, _colors.Length);
        TransparentIndex = transparentIndex;
    }

    private Palette(Rgba32[] colors, byte? transparentIndex, long revision)
    {
        ValidateColorCount(colors.Length);
        ValidateTransparentIndex(transparentIndex, colors.Length);
        _colors = colors;
        TransparentIndex = transparentIndex;
        Revision = revision;
    }

    public int Count => _colors.Length;
    public byte? TransparentIndex { get; private set; }
    public long Revision { get; private set; }

    public Rgba32 GetColor(byte index)
    {
        ValidateIndex(index);
        return _colors[index];
    }

    public Rgba32 ResolveColor(byte index)
    {
        ValidateIndex(index);
        return TransparentIndex == index ? Rgba32.Transparent : _colors[index];
    }

    public PaletteSnapshot Snapshot() =>
        new((Rgba32[])_colors.Clone(), TransparentIndex, Revision);

    public Palette Clone() =>
        new((Rgba32[])_colors.Clone(), TransparentIndex, Revision);

    internal void SetColor(byte index, Rgba32 color)
    {
        ValidateIndex(index);
        var nextRevision = checked(Revision + 1);
        _colors[index] = color;
        Revision = nextRevision;
    }

    internal void ReplaceState(ReadOnlySpan<Rgba32> colors, byte? transparentIndex)
    {
        if (colors.Length != _colors.Length)
            throw new ArgumentException(
                "Palette entry count cannot change through ReplaceState; use a remapping command for palette resize.",
                nameof(colors));
        ValidateTransparentIndex(transparentIndex, colors.Length);
        var nextRevision = checked(Revision + 1);
        colors.CopyTo(_colors);
        TransparentIndex = transparentIndex;
        Revision = nextRevision;
    }

    internal static Palette FromState(
        ReadOnlySpan<Rgba32> colors,
        byte? transparentIndex,
        long revision = 0)
    {
        if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        var copy = colors.ToArray();
        return new Palette(copy, transparentIndex, revision);
    }

    private void ValidateIndex(byte index)
    {
        if (index >= _colors.Length)
            throw new ArgumentOutOfRangeException(nameof(index), $"Palette index {index} is outside 0..{_colors.Length - 1}.");
    }

    private static void ValidateColorCount(int count)
    {
        if (count is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(count), "Palette must contain between 1 and 256 colors.");
    }

    private static void ValidateTransparentIndex(byte? transparentIndex, int count)
    {
        if (transparentIndex is { } index && index >= count)
            throw new ArgumentOutOfRangeException(nameof(transparentIndex), "Transparent index must reference an existing palette entry.");
    }
}

public sealed class PaletteSnapshot
{
    private readonly Rgba32[] _colors;

    internal PaletteSnapshot(Rgba32[] colors, byte? transparentIndex, long revision)
    {
        _colors = colors ?? throw new ArgumentNullException(nameof(colors));
        if (_colors.Length is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(colors));
        if (transparentIndex is { } index && index >= _colors.Length)
            throw new ArgumentOutOfRangeException(nameof(transparentIndex));
        if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        TransparentIndex = transparentIndex;
        Revision = revision;
    }

    public int Count => _colors.Length;
    public byte? TransparentIndex { get; }
    public long Revision { get; }
    public IReadOnlyList<Rgba32> Colors => Array.AsReadOnly(_colors);

    public Rgba32 GetColor(byte index)
    {
        ValidateIndex(index);
        return _colors[index];
    }

    public Rgba32 ResolveColor(byte index)
    {
        ValidateIndex(index);
        return TransparentIndex == index ? Rgba32.Transparent : _colors[index];
    }

    private void ValidateIndex(byte index)
    {
        if (index >= _colors.Length)
            throw new ArgumentOutOfRangeException(nameof(index), $"Palette index {index} is outside 0..{_colors.Length - 1}.");
    }
}
