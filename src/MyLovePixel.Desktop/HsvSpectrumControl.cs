using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

/// <summary>A saturation/value plane with pointer capture and a visible draggable handle.</summary>
internal sealed class HsvSpectrumControl : Control
{
    private double _hue;
    private double _saturation = 1;
    private double _value = 1;
    private byte _alpha = 255;
    private bool _dragging;
    private Rgba32 _lastColor = new(255, 0, 0);
    private static readonly LinearGradientBrush WhiteOverlay = new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(Avalonia.Media.Color.FromArgb(0, 255, 255, 255), 1) },
    };
    private static readonly LinearGradientBrush BlackOverlay = new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Avalonia.Media.Color.FromArgb(0, 0, 0, 0), 0), new GradientStop(Colors.Black, 1) },
    };

    public HsvSpectrumControl()
    {
        Height = 190;
        MinWidth = 240;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Cross);
        AutomationProperties.SetAutomationId(this, "color-picker-spectrum");
        AutomationProperties.SetName(this, "Color saturation and brightness. Drag the circle or use arrow keys. Hold Shift for larger steps.");
        PointerCaptureLost += (_, _) => _dragging = false;
    }

    public event Action<Rgba32>? ColorChanged;
    public double Hue => _hue;

    public void SetColor(Rgba32 color)
    {
        var hsv = ColorPickerMath.ToHsv(color);
        if (hsv.Saturation > 0) _hue = hsv.Hue;
        _saturation = hsv.Saturation;
        _value = hsv.Value;
        _alpha = color.A;
        _lastColor = color;
        InvalidateVisual();
    }

    public void SetHue(double hue) { _hue = Math.Clamp(hue, 0, 359.999); Publish(); }
    public void SetAlpha(byte alpha) { _alpha = alpha; Publish(); }

    private Rect Plane => new(7, 7, Math.Max(1, Bounds.Width - 14), Math.Max(1, Bounds.Height - 14));

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 14 || Bounds.Height <= 14) return;
        var plane = Plane;
        var hueColor = ColorPickerMath.FromHsv(_hue, 1, 1);
        var background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(hueColor.R, hueColor.G, hueColor.B));
        context.DrawRectangle(background, null, plane);
        context.DrawRectangle(WhiteOverlay, null, plane);
        context.DrawRectangle(BlackOverlay, null, plane);
        var point = new Point(plane.X + _saturation * plane.Width, plane.Y + (1 - _value) * plane.Height);
        context.DrawEllipse(null, new Pen(Brushes.Black, 4), point, 6, 6);
        context.DrawEllipse(null, new Pen(Brushes.White, 2), point, 6, 6);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus();
        _dragging = true;
        e.Pointer.Capture(this);
        UpdatePointer(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        UpdatePointer(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        UpdatePointer(e.GetPosition(this));
        _dragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var step = (e.KeyModifiers & KeyModifiers.Shift) != 0 ? 0.1 : 0.01;
        switch (e.Key)
        {
            case Key.Left: _saturation = Math.Max(0, _saturation - step); break;
            case Key.Right: _saturation = Math.Min(1, _saturation + step); break;
            case Key.Up: _value = Math.Min(1, _value + step); break;
            case Key.Down: _value = Math.Max(0, _value - step); break;
            default: return;
        }
        Publish();
        e.Handled = true;
    }

    private void UpdatePointer(Point point)
    {
        var plane = Plane;
        _saturation = Math.Clamp((point.X - plane.X) / plane.Width, 0, 1);
        _value = 1 - Math.Clamp((point.Y - plane.Y) / plane.Height, 0, 1);
        Publish();
    }

    private void Publish()
    {
        InvalidateVisual();
        var color = ColorPickerMath.FromHsv(_hue, _saturation, _value, _alpha);
        if (color == _lastColor) return;
        _lastColor = color;
        ColorChanged?.Invoke(color);
    }
}
