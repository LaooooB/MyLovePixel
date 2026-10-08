using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

internal sealed class PixelPreviewView : Control
{
    private readonly CanvasBitmapCache _bitmap = new();
    private CanvasPresentation? _presentation;
    private IBrush _backdrop = CanvasBackdrop.Create(new CanvasDisplaySettings());
    private IBrush _frame = CanvasBackdrop.Solid(new CanvasDisplaySettings().Frame);

    public void SetDisplaySettings(CanvasDisplaySettings settings)
    {
        _backdrop = CanvasBackdrop.Create(settings);
        _frame = CanvasBackdrop.Solid(settings.Frame);
        InvalidateVisual();
    }

    public void SetPresentation(CanvasPresentation? presentation)
    {
        if (ReferenceEquals(_presentation, presentation)) return;
        _presentation = presentation;
        _bitmap.Update(presentation);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 1 || bounds.Height <= 1) return;
        context.FillRectangle(_frame, bounds);
        if (_presentation is not { } p) return;
        var scale = Math.Min(Math.Max(1, bounds.Width - 8) / p.Size.Width, Math.Max(1, bounds.Height - 8) / p.Size.Height);
        var rect = new Rect((bounds.Width - p.Size.Width * scale) / 2,
            (bounds.Height - p.Size.Height * scale) / 2, p.Size.Width * scale, p.Size.Height * scale);
        context.FillRectangle(_backdrop, rect);
        _bitmap.Draw(context, rect);
    }

    public PixelPreviewView() => RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
    public void ReleaseDisplayResources() => _bitmap.Dispose();
}
