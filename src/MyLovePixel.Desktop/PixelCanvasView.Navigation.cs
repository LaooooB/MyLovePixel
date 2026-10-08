using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MyLovePixel.Desktop;

public sealed partial class PixelCanvasView
{
    private Rect? _visibleCanvasRect;
    private Rect _recordedViewport;

    internal void SetNavigationViewport(Rect visible) => UpdateVisibleViewport(visible);

    private void UpdateVisibleViewport(Rect visible)
    {
        _visibleCanvasRect = visible;
        var needed = new Rect(Bounds.Size).Intersect(visible);
        // Keep a screen-sized guard band in the retained drawing. Small pan
        // movements reuse commands rather than recording the scene every event.
        if (needed.Width > 0 && needed.Height > 0 &&
            (needed.Left < _recordedViewport.Left || needed.Top < _recordedViewport.Top ||
             needed.Right > _recordedViewport.Right || needed.Bottom > _recordedViewport.Bottom))
            InvalidateVisual();
    }
    public long DisplayResampleCount => _displayBitmap.ResampleCount;
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
        return _visibleCanvasRect is { } visible ? bounds.Intersect(visible.Inflate(256)) : bounds;
    }
}
