using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed partial class PixelCanvasView
{
    private readonly CanvasBitmapCache _displayBitmap = new();
    private IBrush _displayBackdrop = CanvasBackdrop.Create(new CanvasDisplaySettings());

    public void SetDisplaySettings(CanvasDisplaySettings settings)
    {
        _displayBackdrop = CanvasBackdrop.Create(settings);
        InvalidateVisual();
    }

    private void UpdateDisplayBitmap()
    {
        _displayBitmap.Update(_presentation, _invert);
        RenderOptions.SetBitmapInterpolationMode(this, _zoom < 1d ? BitmapInterpolationMode.HighQuality : BitmapInterpolationMode.None);
    }

    private void DrawDisplayBitmap(DrawingContext context, CanvasPresentation presentation)
    {
        var rect = new Rect(0, 0, presentation.Size.Width * _zoom, presentation.Size.Height * _zoom);
        context.FillRectangle(_displayBackdrop, rect);
        _displayBitmap.Draw(context, rect);
    }

    public void ReleaseDisplayResources() => _displayBitmap.Dispose();
}

public sealed partial class PixelCanvasView
{
    public Func<bool>? IsColorPickActive { get; set; }
    public Action<int, int>? PickColorRequested { get; set; }
}
