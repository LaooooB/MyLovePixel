using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Document;

public static class IndexedDocumentFactory
{
    public static PixelDocument Create(
        IntSize size,
        IEnumerable<Rgba32> colors,
        byte? transparentIndex,
        ReadOnlySpan<byte> indices)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var expected = checked(size.Width * size.Height);
        if (indices.Length != expected)
            throw new ArgumentException($"Indexed byte length must be {expected}, received {indices.Length}.", nameof(indices));

        var document = PixelDocumentFactory.CreateBlank(size.Width, size.Height);
        var cel = document.Cels.Single();
        var oldSurfaceId = cel.SurfaceId;
        var palette = new Palette(colors, transparentIndex);
        var paletteId = document.Resources.AddPalette(palette);
        var surface = PixelSurface.CreateIndexed(size, paletteId);
        surface.ReplaceIndices(indices);
        var surfaceId = document.Resources.AddSurface(surface);
        cel.SurfaceId = surfaceId;
        document.Resources.RemoveSurface(oldSurfaceId);
        return document;
    }
}
