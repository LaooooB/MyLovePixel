using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Document;

public static class RgbaDocumentFactory
{
    public static PixelDocument Create(IntSize size, ReadOnlySpan<byte> rgba)
    {
        var expected = checked(size.Width * size.Height * 4);
        if (rgba.Length != expected)
            throw new ArgumentException($"RGBA byte length must be {expected}, received {rgba.Length}.", nameof(rgba));

        var document = PixelDocumentFactory.CreateBlank(size.Width, size.Height);
        var cel = document.Cels.Single();
        var oldSurfaceId = cel.SurfaceId;
        var surface = PixelSurface.FromRgbaBytes(size, rgba);
        var surfaceId = document.Resources.AddSurface(surface);
        cel.SurfaceId = surfaceId;
        document.Resources.RemoveSurface(oldSurfaceId);
        return document;
    }
}
