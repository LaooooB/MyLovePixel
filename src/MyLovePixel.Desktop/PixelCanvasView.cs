using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed class PixelCanvasView : Control
{
    private readonly Dictionary<uint, IBrush> _brushes = [];
    private CanvasPresentation? _presentation;
    private double _zoom = 1d;

    public PixelCanvasView()
    {
        ClipToBounds = true;
    }

    public CanvasPresentation? Presentation => _presentation;
    public double Zoom => _zoom;

    public void SetPresentation(CanvasPresentation? presentation, double zoom)
    {
        if (!double.IsFinite(zoom) || zoom <= 0d) throw new ArgumentOutOfRangeException(nameof(zoom));
        _presentation = presentation;
        _zoom = zoom;
        Width = presentation is null ? 1d : presentation.Size.Width * zoom;
        Height = presentation is null ? 1d : presentation.Size.Height * zoom;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.DimGray, new Rect(Bounds.Size));
        var presentation = _presentation;
        if (presentation is null) return;

        var bytes = presentation.Rgba.Span;
        for (var y = 0; y < presentation.Size.Height; y++)
        for (var x = 0; x < presentation.Size.Width; x++)
        {
            var offset = ((y * presentation.Size.Width) + x) * 4;
            var r = bytes[offset];
            var g = bytes[offset + 1];
            var b = bytes[offset + 2];
            var a = bytes[offset + 3];
            var rect = new Rect(x * _zoom, y * _zoom, _zoom, _zoom);
            if (a == 0)
            {
                var checker = ((x + y) & 1) == 0 ? Brushes.Gray : Brushes.DarkGray;
                context.FillRectangle(checker, rect);
                continue;
            }

            context.FillRectangle(GetBrush(r, g, b, a), rect);
        }
    }

    private IBrush GetBrush(byte r, byte g, byte b, byte a)
    {
        var key = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
        if (_brushes.TryGetValue(key, out var brush)) return brush;
        brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        _brushes.Add(key, brush);
        return brush;
    }
}
