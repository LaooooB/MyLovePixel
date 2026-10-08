using Avalonia;
using Avalonia.Media.Imaging;

namespace MyLovePixel.Desktop;

internal sealed partial class CanvasBitmapCache
{
    private readonly List<Bitmap> _resolutions = [];
    public long ResampleCount { get; private set; }

    private Bitmap SelectResolution(Rect destination, double renderScaling)
    {
        // Retain the original for 1:1 and magnification. Reduced levels are
        // immutable display caches and never replace the document pixels.
        Bitmap image = _bitmap!;
        var targetWidth = destination.Width * Math.Max(1d, renderScaling);
        var targetHeight = destination.Height * Math.Max(1d, renderScaling);
        var level = 0;
        while (image.PixelSize.Width > 1 && image.PixelSize.Height > 1 &&
               image.PixelSize.Width * 0.5 >= targetWidth && image.PixelSize.Height * 0.5 >= targetHeight)
        {
            if (level == _resolutions.Count)
            {
                _resolutions.Add(image.CreateScaledBitmap(
                    new PixelSize(Math.Max(1, (image.PixelSize.Width + 1) / 2), Math.Max(1, (image.PixelSize.Height + 1) / 2)),
                    BitmapInterpolationMode.HighQuality));
                ResampleCount++;
            }
            image = _resolutions[level++];
        }
        return image;
    }

    private void InvalidateResolutions()
    {
        foreach (var image in _resolutions) image.Dispose();
        _resolutions.Clear();
    }
}
