using System.Numerics;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Selection;

public enum SelectionMaskFormat
{
    Bit1,
    Alpha8,
}

public sealed class SelectionMask
{
    private readonly ISelectionStorage _storage;

    private SelectionMask(IntSize size, SelectionMaskFormat format, ISelectionStorage storage)
    {
        Size = size;
        Format = format;
        _storage = storage;
        SelectedPixelCount = CountSelected(storage, size);
        Bounds = CalculateBounds(storage, size);
    }

    public IntSize Size { get; }
    public SelectionMaskFormat Format { get; }
    public int SelectedPixelCount { get; }
    public IntRect Bounds { get; }
    public bool IsEmpty => SelectedPixelCount == 0;

    public static SelectionMask Empty(IntSize size, SelectionMaskFormat format = SelectionMaskFormat.Bit1) =>
        FromCoverage(size, format, new byte[checked(size.Width * size.Height)]);

    public static SelectionMask Full(IntSize size, SelectionMaskFormat format = SelectionMaskFormat.Bit1)
    {
        var coverage = new byte[checked(size.Width * size.Height)];
        Array.Fill(coverage, byte.MaxValue);
        return FromCoverage(size, format, coverage);
    }

    public static SelectionMask FromCoverage(IntSize size, SelectionMaskFormat format, ReadOnlySpan<byte> coverage)
    {
        var expectedLength = checked(size.Width * size.Height);
        if (coverage.Length != expectedLength)
            throw new ArgumentException($"Selection coverage length must be {expectedLength}.", nameof(coverage));

        ISelectionStorage storage = format switch
        {
            SelectionMaskFormat.Bit1 => new Bit1SelectionStorage(coverage),
            SelectionMaskFormat.Alpha8 => new Alpha8SelectionStorage(coverage),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        return new SelectionMask(size, format, storage);
    }

    public byte GetCoverage(int x, int y)
    {
        ValidateCoordinates(x, y);
        return _storage.Get((y * Size.Width) + x);
    }

    public bool IsSelected(int x, int y) => GetCoverage(x, y) != 0;

    public IEnumerable<IntPoint> EnumerateSelected()
    {
        for (var y = 0; y < Size.Height; y++)
        for (var x = 0; x < Size.Width; x++)
        {
            if (_storage.Get((y * Size.Width) + x) != 0)
                yield return new IntPoint(x, y);
        }
    }

    internal byte[] CopyCoverage()
    {
        var values = new byte[checked(Size.Width * Size.Height)];
        _storage.CopyTo(values);
        return values;
    }

    private void ValidateCoordinates(int x, int y)
    {
        if ((uint)x >= (uint)Size.Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Size.Height) throw new ArgumentOutOfRangeException(nameof(y));
    }

    private static int CountSelected(ISelectionStorage storage, IntSize size)
    {
        if (storage is Bit1SelectionStorage bitStorage) return bitStorage.CountSelected;

        var count = 0;
        var length = checked(size.Width * size.Height);
        for (var index = 0; index < length; index++)
        {
            if (storage.Get(index) != 0) count++;
        }
        return count;
    }

    private static IntRect CalculateBounds(ISelectionStorage storage, IntSize size)
    {
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        for (var y = 0; y < size.Height; y++)
        for (var x = 0; x < size.Width; x++)
        {
            if (storage.Get((y * size.Width) + x) == 0) continue;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        return minX == int.MaxValue
            ? default
            : new IntRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private interface ISelectionStorage
    {
        byte Get(int index);
        void CopyTo(Span<byte> destination);
    }

    private sealed class Alpha8SelectionStorage : ISelectionStorage
    {
        private readonly byte[] _values;

        public Alpha8SelectionStorage(ReadOnlySpan<byte> values) => _values = values.ToArray();

        public byte Get(int index) => _values[index];

        public void CopyTo(Span<byte> destination) => _values.CopyTo(destination);
    }

    private sealed class Bit1SelectionStorage : ISelectionStorage
    {
        private readonly ulong[] _words;
        private readonly int _length;

        public Bit1SelectionStorage(ReadOnlySpan<byte> values)
        {
            _length = values.Length;
            _words = new ulong[(values.Length + 63) / 64];
            for (var index = 0; index < values.Length; index++)
            {
                if (values[index] == 0) continue;
                _words[index >> 6] |= 1UL << (index & 63);
            }

            var count = 0;
            foreach (var word in _words) count = checked(count + BitOperations.PopCount(word));
            CountSelected = count;
        }

        public int CountSelected { get; }

        public byte Get(int index) => ((_words[index >> 6] >> (index & 63)) & 1UL) != 0 ? byte.MaxValue : (byte)0;

        public void CopyTo(Span<byte> destination)
        {
            if (destination.Length < _length) throw new ArgumentException("Destination is too small.", nameof(destination));
            for (var index = 0; index < _length; index++) destination[index] = Get(index);
        }
    }
}
