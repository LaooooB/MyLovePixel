using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Color;

public static class ColorDistance
{
    public static int SquaredRgba(Rgba32 left, Rgba32 right)
    {
        var dr = left.R - right.R;
        var dg = left.G - right.G;
        var db = left.B - right.B;
        var da = left.A - right.A;
        return (dr * dr) + (dg * dg) + (db * db) + (da * da);
    }
}

public static class PaletteMatcher
{
    public static byte FindNearestIndex(PaletteSnapshot palette, Rgba32 color)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (color.A == 0 && palette.TransparentIndex is { } transparentIndex)
            return transparentIndex;

        var bestIndex = -1;
        var bestDistance = int.MaxValue;
        for (var index = 0; index < palette.Count; index++)
        {
            var candidateIndex = checked((byte)index);
            if (palette.TransparentIndex == candidateIndex && color.A != 0)
                continue;

            var distance = ColorDistance.SquaredRgba(color, palette.ResolveColor(candidateIndex));
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            bestIndex = index;
        }

        if (bestIndex >= 0) return checked((byte)bestIndex);
        if (palette.TransparentIndex is { } fallback) return fallback;
        throw new InvalidOperationException("Palette does not contain a usable color entry.");
    }
}

public sealed class PaletteIndexRemap
{
    private readonly byte[] _map;

    internal PaletteIndexRemap(byte[] map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        if (_map.Length is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(map));
    }

    public int SourceColorCount => _map.Length;
    public IReadOnlyList<byte> Map => Array.AsReadOnly(_map);

    public byte Apply(byte sourceIndex)
    {
        if (sourceIndex >= _map.Length)
            throw new ArgumentOutOfRangeException(nameof(sourceIndex));
        return _map[sourceIndex];
    }

    public byte[] Apply(ReadOnlySpan<byte> sourceIndices)
    {
        var result = new byte[sourceIndices.Length];
        for (var index = 0; index < sourceIndices.Length; index++)
            result[index] = Apply(sourceIndices[index]);
        return result;
    }
}

public static class PaletteRemapper
{
    public static PaletteIndexRemap Build(PaletteSnapshot source, PaletteSnapshot target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var map = new byte[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            var sourceIndex = checked((byte)index);
            if (source.TransparentIndex == sourceIndex && target.TransparentIndex is { } transparentTarget)
            {
                map[index] = transparentTarget;
                continue;
            }

            map[index] = PaletteMatcher.FindNearestIndex(target, source.ResolveColor(sourceIndex));
        }

        return new PaletteIndexRemap(map);
    }
}
