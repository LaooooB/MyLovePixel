using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Render;

namespace MyLovePixel.Export;

public sealed class ExportImage
{
    private readonly byte[] _rgba;

    public ExportImage(IntSize size, ReadOnlySpan<byte> rgba)
    {
        var expected = checked(size.Width * size.Height * 4);
        if (rgba.Length != expected) throw new ArgumentException($"RGBA byte length must be {expected}.", nameof(rgba));
        Size = size;
        _rgba = rgba.ToArray();
    }

    public IntSize Size { get; }
    public ReadOnlyMemory<byte> Bytes => _rgba;

    public Rgba32 GetPixel(int x, int y)
    {
        if ((uint)x >= (uint)Size.Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Size.Height) throw new ArgumentOutOfRangeException(nameof(y));
        var offset = ((y * Size.Width) + x) * 4;
        return new Rgba32(_rgba[offset], _rgba[offset + 1], _rgba[offset + 2], _rgba[offset + 3]);
    }

    public static ExportImage FromRenderSurface(CpuRenderSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        return new ExportImage(surface.Size, surface.Bytes.Span);
    }

    public ExportImage Crop(IntRect rect)
    {
        var bounds = new IntRect(0, 0, Size.Width, Size.Height);
        var clipped = Intersect(bounds, rect);
        if (clipped.IsEmpty) throw new ArgumentException("Crop does not intersect the image.", nameof(rect));
        var output = new byte[checked(clipped.Width * clipped.Height * 4)];
        for (var y = 0; y < clipped.Height; y++)
        {
            var sourceOffset = (((clipped.Y + y) * Size.Width) + clipped.X) * 4;
            var destinationOffset = y * clipped.Width * 4;
            _rgba.AsSpan(sourceOffset, clipped.Width * 4).CopyTo(output.AsSpan(destinationOffset));
        }
        return new ExportImage(new IntSize(clipped.Width, clipped.Height), output);
    }

    public TrimmedImage TrimAlpha()
    {
        var minX = Size.Width;
        var minY = Size.Height;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < Size.Height; y++)
        for (var x = 0; x < Size.Width; x++)
        {
            var alpha = _rgba[(((y * Size.Width) + x) * 4) + 3];
            if (alpha == 0) continue;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        if (maxX < minX || maxY < minY)
            return new TrimmedImage(
                new ExportImage(new IntSize(1, 1), new byte[4]),
                new IntRect(0, 0, 1, 1),
                true);

        var rect = new IntRect(minX, minY, checked(maxX - minX + 1), checked(maxY - minY + 1));
        return new TrimmedImage(Crop(rect), rect, false);
    }

    public ExportImage ScaleNearest(int scale)
    {
        if (scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
        if (scale == 1) return this;
        var width = checked(Size.Width * scale);
        var height = checked(Size.Height * scale);
        var output = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var sourceX = x / scale;
            var sourceY = y / scale;
            var sourceOffset = ((sourceY * Size.Width) + sourceX) * 4;
            var destinationOffset = ((y * width) + x) * 4;
            _rgba.AsSpan(sourceOffset, 4).CopyTo(output.AsSpan(destinationOffset, 4));
        }
        return new ExportImage(new IntSize(width, height), output);
    }

    public static ExportImage Compose(
        IntSize size,
        IEnumerable<ImagePlacement> placements,
        int extrude)
    {
        if (extrude < 0) throw new ArgumentOutOfRangeException(nameof(extrude));
        var output = new byte[checked(size.Width * size.Height * 4)];
        foreach (var placement in placements)
        {
            ArgumentNullException.ThrowIfNull(placement.Image);
            Blit(output, size, placement.Image, placement.X, placement.Y);
            if (extrude > 0) Extrude(output, size, placement.Image, placement.X, placement.Y, extrude);
        }
        return new ExportImage(size, output);
    }

    private static void Blit(byte[] target, IntSize targetSize, ExportImage image, int x, int y)
    {
        if (x < 0 || y < 0 || checked(x + image.Size.Width) > targetSize.Width || checked(y + image.Size.Height) > targetSize.Height)
            throw new ArgumentOutOfRangeException(nameof(x), "Image placement is outside the target.");
        for (var row = 0; row < image.Size.Height; row++)
        {
            var sourceOffset = row * image.Size.Width * 4;
            var targetOffset = (((y + row) * targetSize.Width) + x) * 4;
            image._rgba.AsSpan(sourceOffset, image.Size.Width * 4).CopyTo(target.AsSpan(targetOffset));
        }
    }

    private static void Extrude(byte[] target, IntSize targetSize, ExportImage image, int x, int y, int amount)
    {
        for (var distance = 1; distance <= amount; distance++)
        {
            var top = y - distance;
            var bottom = y + image.Size.Height - 1 + distance;
            for (var column = 0; column < image.Size.Width; column++)
            {
                if (top >= 0) CopyPixel(target, targetSize, x + column, y, x + column, top);
                if (bottom < targetSize.Height) CopyPixel(target, targetSize, x + column, y + image.Size.Height - 1, x + column, bottom);
            }

            var left = x - distance;
            var right = x + image.Size.Width - 1 + distance;
            for (var row = -amount; row < image.Size.Height + amount; row++)
            {
                var targetY = y + row;
                if ((uint)targetY >= (uint)targetSize.Height) continue;
                var sourceY = Math.Clamp(targetY, y, y + image.Size.Height - 1);
                if (left >= 0) CopyPixel(target, targetSize, x, sourceY, left, targetY);
                if (right < targetSize.Width) CopyPixel(target, targetSize, x + image.Size.Width - 1, sourceY, right, targetY);
            }
        }
    }

    private static void CopyPixel(byte[] data, IntSize size, int sourceX, int sourceY, int targetX, int targetY)
    {
        var sourceOffset = ((sourceY * size.Width) + sourceX) * 4;
        var targetOffset = ((targetY * size.Width) + targetX) * 4;
        data.AsSpan(sourceOffset, 4).CopyTo(data.AsSpan(targetOffset, 4));
    }

    private static IntRect Intersect(IntRect a, IntRect b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(checked(a.X + a.Width), checked(b.X + b.Width));
        var bottom = Math.Min(checked(a.Y + a.Height), checked(b.Y + b.Height));
        return right <= left || bottom <= top ? default : new IntRect(left, top, right - left, bottom - top);
    }
}

public sealed record TrimmedImage(ExportImage Image, IntRect ContentRect, bool IsEmpty);
public sealed record ImagePlacement(ExportImage Image, int X, int Y);
