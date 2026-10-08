using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MyLovePixel.Desktop;

public sealed partial class PixelCanvasView
{
    private Rect? _visibleCanvasRect;
    public long DisplayFullUploadCount => _displayBitmap.FullUploadCount;

    /// <summary>Camera-only change. No composition, snapshot capture, bitmap upload or inspector rebuild.</summary>
    public void SetViewZoom(double zoom)
    {
        if (!double.IsFinite(zoom) || zoom <= 0) throw new ArgumentOutOfRangeException(nameof(zoom));
        if (_zoom == zoom) return;
        _zoom = zoom;
        Width = _presentation is null ? 1 : _presentation.Size.Width * zoom;
        Height = _presentation is null ? 1 : _presentation.Size.Height * zoom;
        RenderOptions.SetBitmapInterpolationMode(this, zoom < 1 ? BitmapInterpolationMode.HighQuality : BitmapInterpolationMode.None);
        InvalidateVisual();
    }

    private Rect GetVisibleCanvasRect()
    {
        var bounds = new Rect(Bounds.Size);
        return _visibleCanvasRect is { } visible ? bounds.Intersect(visible) : bounds;
    }
}
