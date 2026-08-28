using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Desktop;

public sealed class PixelCanvasView : Control
{
    private readonly Dictionary<uint, IBrush> _brushes = [];
    private CanvasPresentation? _presentation;
    private double _zoom = 1d;

    public PixelCanvasView()
    {
        ClipToBounds = true;
        PointerCaptureLost += (_, _) => CancelPointerInput?.Invoke();
    }

    public Action<EditorPointerEvent>? PointerInput { get; set; }
    public Action? CancelPointerInput { get; set; }
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
        context.FillRectangle(EditorThemeTokens.CanvasBackground, new Rect(Bounds.Size));
        var presentation = _presentation;
        if (presentation is null) return;

        var bytes = presentation.Rgba.Span;
        for (var y = 0; y < presentation.Size.Height; y++)
        for (var x = 0; x < presentation.Size.Width; x++)
        {
            var offset = ((y * presentation.Size.Width) + x) * 4;
            DrawPixel(context, x, y, bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]);
        }

        foreach (var preview in presentation.PreviewPixels)
            DrawPixel(context, preview.Point.X, preview.Point.Y, preview.Color.R, preview.Color.G, preview.Color.B, preview.Color.A);

        if (presentation.DirtyRegions.Count != 0)
        {
            var pen = new Pen(EditorThemeTokens.DirtyRegionOutline, 1d);
            foreach (var region in presentation.DirtyRegions)
            {
                var rect = new Rect(
                    region.X * _zoom,
                    region.Y * _zoom,
                    region.Width * _zoom,
                    region.Height * _zoom);
                context.DrawRectangle(null, pen, rect);
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_presentation is null) return;
        e.Pointer.Capture(this);
        DispatchPointer(e, EditorPointerKind.Pressed);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!ReferenceEquals(e.Pointer.Captured, this) || _presentation is null) return;
        DispatchPointer(e, EditorPointerKind.Moved);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_presentation is null) return;
        DispatchPointer(e, EditorPointerKind.Released);
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void DispatchPointer(PointerEventArgs e, EditorPointerKind kind)
    {
        var point = e.GetCurrentPoint(this);
        var canvasPixel = new IntPoint(
            checked((int)Math.Floor(point.Position.X / _zoom)),
            checked((int)Math.Floor(point.Position.Y / _zoom)));
        var properties = point.Properties;
        var buttons = EditorPointerButtons.None;
        if (properties.IsLeftButtonPressed) buttons |= EditorPointerButtons.Primary;
        if (properties.IsRightButtonPressed) buttons |= EditorPointerButtons.Secondary;
        if (properties.IsMiddleButtonPressed) buttons |= EditorPointerButtons.Middle;
        if (properties.IsBarrelButtonPressed) buttons |= EditorPointerButtons.Barrel;
        if (properties.IsEraser) buttons |= EditorPointerButtons.Eraser;

        var modifiers = EditorInputModifiers.None;
        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Shift) != 0) modifiers |= EditorInputModifiers.Shift;
        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Control) != 0) modifiers |= EditorInputModifiers.Control;
        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Alt) != 0) modifiers |= EditorInputModifiers.Alt;
        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Meta) != 0) modifiers |= EditorInputModifiers.Meta;

        PointerInput?.Invoke(new EditorPointerEvent(
            e.Pointer.Id,
            e.Pointer.Type switch
            {
                PointerType.Mouse => EditorPointerDevice.Mouse,
                PointerType.Pen => EditorPointerDevice.Pen,
                PointerType.Touch => EditorPointerDevice.Touch,
                _ => EditorPointerDevice.Unknown,
            },
            kind,
            canvasPixel,
            properties.Pressure,
            buttons,
            modifiers,
            unchecked((long)e.Timestamp)));
    }

    private void DrawPixel(DrawingContext context, int x, int y, byte r, byte g, byte b, byte a)
    {
        var rect = new Rect(x * _zoom, y * _zoom, _zoom, _zoom);
        if (a == 0)
        {
            var checker = ((x + y) & 1) == 0 ? EditorThemeTokens.CheckerLight : EditorThemeTokens.CheckerDark;
            context.FillRectangle(checker, rect);
            return;
        }
        context.FillRectangle(GetBrush(r, g, b, a), rect);
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
