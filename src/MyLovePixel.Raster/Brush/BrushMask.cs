using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Raster.Brush;

public sealed class BrushMask
{
    private readonly byte[] _mask;

    public BrushMask(IntSize size, IntPoint anchor, ReadOnlySpan<byte> mask)
    {
        if ((uint)anchor.X >= (uint)size.Width || (uint)anchor.Y >= (uint)size.Height)
            throw new ArgumentOutOfRangeException(nameof(anchor), "Brush anchor must be inside the brush mask.");

        var expectedLength = checked(size.Width * size.Height);
        if (mask.Length != expectedLength)
            throw new ArgumentException($"Brush mask length must be {expectedLength}.", nameof(mask));

        Size = size;
        Anchor = anchor;
        _mask = mask.ToArray();
        for (var index = 0; index < _mask.Length; index++)
            _mask[index] = _mask[index] == 0 ? (byte)0 : (byte)1;

        if (!_mask.Any(value => value != 0))
            throw new ArgumentException("Brush mask must contain at least one active pixel.", nameof(mask));
    }

    public IntSize Size { get; }
    public IntPoint Anchor { get; }

    public static BrushMask SinglePixel { get; } = new(new IntSize(1, 1), default, [1]);

    public static BrushMask Square(int size)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        var mask = new byte[checked(size * size)];
        Array.Fill(mask, (byte)1);
        return new BrushMask(new IntSize(size, size), new IntPoint(size / 2, size / 2), mask);
    }

    public IEnumerable<IntPoint> Stamp(IntPoint center)
    {
        for (var y = 0; y < Size.Height; y++)
        for (var x = 0; x < Size.Width; x++)
        {
            if (_mask[(y * Size.Width) + x] == 0) continue;
            yield return new IntPoint(
                checked(center.X + x - Anchor.X),
                checked(center.Y + y - Anchor.Y));
        }
    }
}
