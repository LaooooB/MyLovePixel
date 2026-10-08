using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MyLovePixel.Desktop;

internal sealed partial class CanvasBitmapCache
{
    private readonly List<Bitmap> _resolutions = [];
    public long ResampleCount { get; private set; }

    private Bitmap SelectResolution(Rect destination, double renderScaling)
    {
        Bitmap image = _bitmap!;
        var targetWidth = destination.Width * Math.Max(1d, renderScaling);
        var targetHeight = destination.Height * Math.Max(1d, renderScaling);
        var level = 0;
        while (image.PixelSize.Width > 1 && image.PixelSize.Height > 1 &&
               image.PixelSize.Width * 0.5 >= targetWidth && image.PixelSize.Height * 0.5 >= targetHeight)
        {
            if (level == _resolutions.Count)
            {
                var size = new PixelSize(Math.Max(1, (image.PixelSize.Width + 1) / 2), Math.Max(1, (image.PixelSize.Height + 1) / 2));
                _resolutions.Add(ReduceImage(image, size));
                ResampleCount++;
            }
            image = _resolutions[level++];
        }
        return image;
    }

    private static Bitmap ReduceImage(Bitmap source, PixelSize size)
    {
        // The Skia backend cannot CreateScaledBitmap from a WriteableBitmap.
        // A new drawing target supports writable input and alpha-aware filtering.
        var result = new RenderTargetBitmap(size, new Vector(96, 96));
        try
        {
            using (var context = result.CreateDrawingContext())
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
                context.DrawImage(source, new Rect(source.Size), new Rect(0, 0, size.Width, size.Height));
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private void InvalidateResolutions()
    {
        foreach (var image in _resolutions) image.Dispose();
        _resolutions.Clear();
    }
}
