using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Color;

public sealed class QuantizedImage
{
    private readonly Rgba32[] _colors;
    private readonly byte[] _indices;

    internal QuantizedImage(
        IntSize size,
        Rgba32[] colors,
        byte? transparentIndex,
        byte[] indices)
    {
        if (colors.Length is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(colors));
        if (transparentIndex is { } index && index >= colors.Length)
            throw new ArgumentOutOfRangeException(nameof(transparentIndex));
        if (indices.Length != checked(size.Width * size.Height))
            throw new ArgumentException("Index count must match image dimensions.", nameof(indices));
        Size = size;
        _colors = colors;
        TransparentIndex = transparentIndex;
        _indices = indices;
    }

    public IntSize Size { get; }
    public IReadOnlyList<Rgba32> Colors => Array.AsReadOnly(_colors);
    public byte? TransparentIndex { get; }
    public ReadOnlyMemory<byte> Indices => _indices;

    public Palette CreatePalette() => new(_colors, TransparentIndex);
}

public interface IQuantizationStrategy
{
    QuantizedImage Quantize(
        PixelSurfaceSnapshot source,
        int maxColors = 256,
        bool reserveTransparentIndex = true);
}

public sealed class MedianCutQuantizationStrategy : IQuantizationStrategy
{
    public static MedianCutQuantizationStrategy Instance { get; } = new();

    private MedianCutQuantizationStrategy()
    {
    }

    public QuantizedImage Quantize(
        PixelSurfaceSnapshot source,
        int maxColors = 256,
        bool reserveTransparentIndex = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Format != PixelFormat.Rgba32)
            throw new ArgumentException("Quantization requires an RGBA32 source snapshot.", nameof(source));
        if (maxColors is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(maxColors));

        var counts = new Dictionary<Rgba32, int>();
        var hasTransparentPixels = false;
        var bytes = source.Bytes.Span;
        for (var offset = 0; offset < bytes.Length; offset += 4)
        {
            var color = new Rgba32(bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]);
            if (reserveTransparentIndex && color.A == 0)
            {
                hasTransparentPixels = true;
                continue;
            }

            counts.TryGetValue(color, out var count);
            counts[color] = checked(count + 1);
        }

        var transparentSlots = reserveTransparentIndex && hasTransparentPixels ? 1 : 0;
        var availableColorSlots = maxColors - transparentSlots;
        if (counts.Count > 0 && availableColorSlots <= 0)
            throw new ArgumentException(
                "At least two colors are required when reserving a transparent index for an image that also contains visible pixels.",
                nameof(maxColors));

        var representatives = QuantizeOpaqueColors(counts, availableColorSlots);
        var colors = new List<Rgba32>(transparentSlots + representatives.Count);
        byte? transparentIndex = null;
        if (transparentSlots == 1)
        {
            transparentIndex = 0;
            colors.Add(Rgba32.Transparent);
        }
        colors.AddRange(representatives);

        if (colors.Count == 0)
        {
            colors.Add(Rgba32.Transparent);
            transparentIndex = reserveTransparentIndex ? (byte)0 : null;
        }

        var palette = new Palette(colors, transparentIndex).Snapshot();
        var indices = new byte[checked(source.Size.Width * source.Size.Height)];
        for (var pixel = 0; pixel < indices.Length; pixel++)
        {
            var offset = pixel * 4;
            var color = new Rgba32(bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]);
            indices[pixel] = transparentIndex is { } transparent && color.A == 0
                ? transparent
                : PaletteMatcher.FindNearestIndex(palette, color);
        }

        return new QuantizedImage(source.Size, colors.ToArray(), transparentIndex, indices);
    }

    private static IReadOnlyList<Rgba32> QuantizeOpaqueColors(
        IReadOnlyDictionary<Rgba32, int> counts,
        int maxColors)
    {
        if (counts.Count == 0 || maxColors <= 0) return Array.Empty<Rgba32>();

        var bins = counts
            .Select(pair => new ColorBin(pair.Key, pair.Value))
            .OrderBy(bin => Packed(bin.Color))
            .ToArray();
        var boxes = new List<ColorBox> { new(bins) };

        while (boxes.Count < maxColors)
        {
            var candidateIndex = SelectSplitCandidate(boxes);
            if (candidateIndex < 0) break;
            var candidate = boxes[candidateIndex];
            var (first, second) = candidate.Split();
            boxes[candidateIndex] = first;
            boxes.Insert(candidateIndex + 1, second);
        }

        return boxes.Select(box => box.GetRepresentative()).ToArray();
    }

    private static int SelectSplitCandidate(IReadOnlyList<ColorBox> boxes)
    {
        var bestIndex = -1;
        var bestRange = -1;
        var bestWeight = -1L;
        var bestCount = -1;

        for (var index = 0; index < boxes.Count; index++)
        {
            var box = boxes[index];
            if (box.Count <= 1) continue;
            var range = box.MaxChannelRange;
            if (range > bestRange ||
                (range == bestRange && box.TotalWeight > bestWeight) ||
                (range == bestRange && box.TotalWeight == bestWeight && box.Count > bestCount))
            {
                bestIndex = index;
                bestRange = range;
                bestWeight = box.TotalWeight;
                bestCount = box.Count;
            }
        }

        return bestIndex;
    }

    private readonly record struct ColorBin(Rgba32 Color, int Count);

    private sealed class ColorBox
    {
        private readonly ColorBin[] _bins;

        public ColorBox(IEnumerable<ColorBin> bins)
        {
            _bins = bins.ToArray();
            if (_bins.Length == 0) throw new ArgumentException("Color box cannot be empty.", nameof(bins));
            TotalWeight = _bins.Sum(bin => (long)bin.Count);
            (SplitChannel, MaxChannelRange) = FindSplitChannel(_bins);
        }

        public int Count => _bins.Length;
        public long TotalWeight { get; }
        public int MaxChannelRange { get; }
        private Channel SplitChannel { get; }

        public (ColorBox First, ColorBox Second) Split()
        {
            if (_bins.Length <= 1) throw new InvalidOperationException("A single-color box cannot be split.");

            var ordered = _bins
                .OrderBy(bin => ChannelValue(bin.Color, SplitChannel))
                .ThenBy(bin => Packed(bin.Color))
                .ToArray();
            var midpoint = Math.Max(1L, (TotalWeight + 1) / 2);
            long accumulated = 0;
            var splitIndex = 1;
            for (var index = 0; index < ordered.Length - 1; index++)
            {
                accumulated += ordered[index].Count;
                splitIndex = index + 1;
                if (accumulated >= midpoint) break;
            }

            return (
                new ColorBox(ordered[..splitIndex]),
                new ColorBox(ordered[splitIndex..]));
        }

        public Rgba32 GetRepresentative()
        {
            long red = 0;
            long green = 0;
            long blue = 0;
            long alpha = 0;
            foreach (var bin in _bins)
            {
                red += (long)bin.Color.R * bin.Count;
                green += (long)bin.Color.G * bin.Count;
                blue += (long)bin.Color.B * bin.Count;
                alpha += (long)bin.Color.A * bin.Count;
            }

            return new Rgba32(
                WeightedAverage(red, TotalWeight),
                WeightedAverage(green, TotalWeight),
                WeightedAverage(blue, TotalWeight),
                WeightedAverage(alpha, TotalWeight));
        }

        private static (Channel Channel, int Range) FindSplitChannel(IReadOnlyList<ColorBin> bins)
        {
            var minR = byte.MaxValue;
            var minG = byte.MaxValue;
            var minB = byte.MaxValue;
            var minA = byte.MaxValue;
            var maxR = byte.MinValue;
            var maxG = byte.MinValue;
            var maxB = byte.MinValue;
            var maxA = byte.MinValue;

            foreach (var bin in bins)
            {
                minR = Math.Min(minR, bin.Color.R);
                minG = Math.Min(minG, bin.Color.G);
                minB = Math.Min(minB, bin.Color.B);
                minA = Math.Min(minA, bin.Color.A);
                maxR = Math.Max(maxR, bin.Color.R);
                maxG = Math.Max(maxG, bin.Color.G);
                maxB = Math.Max(maxB, bin.Color.B);
                maxA = Math.Max(maxA, bin.Color.A);
            }

            var ranges = new[]
            {
                (Channel.Red, maxR - minR),
                (Channel.Green, maxG - minG),
                (Channel.Blue, maxB - minB),
                (Channel.Alpha, maxA - minA),
            };
            return ranges
                .OrderByDescending(item => item.Item2)
                .ThenBy(item => item.Item1)
                .First();
        }
    }

    private enum Channel
    {
        Red = 0,
        Green = 1,
        Blue = 2,
        Alpha = 3,
    }

    private static int ChannelValue(Rgba32 color, Channel channel) => channel switch
    {
        Channel.Red => color.R,
        Channel.Green => color.G,
        Channel.Blue => color.B,
        Channel.Alpha => color.A,
        _ => throw new ArgumentOutOfRangeException(nameof(channel)),
    };

    private static byte WeightedAverage(long sum, long weight) =>
        checked((byte)((sum + (weight / 2)) / weight));

    private static uint Packed(Rgba32 color) =>
        ((uint)color.R << 24) |
        ((uint)color.G << 16) |
        ((uint)color.B << 8) |
        color.A;
}
