using HsvColor = MyLovePixel.Application.HsvColor;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

/// <summary>Direct-manipulation HSV picker. Its marker follows the pointer without hover smoothing.</summary>
internal sealed class ColorSpectrum : Control
{
    private IPointer? _pointer;
    private HsvColor _hsv;
    public bool HueStrip { get; init; }
    public HsvColor Hsv { get => _hsv; set { _hsv = value; InvalidateVisual(); } }
    public event Action<HsvColor>? ColorChanged;
    private Rect Field => new(8, 8, Math.Max(1, Bounds.Width - 16), Math.Max(1, Bounds.Height - 16));
    public ColorSpectrum()
    {
        Focusable = true; Cursor = new Cursor(StandardCursorType.Cross);
        PointerCaptureLost += (_, _) => _pointer = null;
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var field = Field;
        var horizontal = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative) };
        if (HueStrip)
        {
            for (var i = 0; i <= 6; i++)
            {
                var color = new HsvColor(i * 60, 1, 1).ToColor();
                horizontal.GradientStops.Add(new GradientStop(Color.FromRgb(color.R, color.G, color.B), i / 6d));
            }
        }
        else
        {
            var color = new HsvColor(_hsv.Hue, 1, 1).ToColor();
            horizontal.GradientStops.Add(new GradientStop(Colors.White, 0));
            horizontal.GradientStops.Add(new GradientStop(Color.FromRgb(color.R, color.G, color.B), 1));
        }
        context.FillRectangle(horizontal, field);
        if (!HueStrip)
        {
            var vertical = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops { new GradientStop(Color.FromArgb(0, 0, 0, 0), 0), new GradientStop(Colors.Black, 1) } };
            context.FillRectangle(vertical, field);
        }
        var point = HueStrip ? new Point(field.X + _hsv.Hue / 360 * field.Width, field.Center.Y)
            : new Point(field.X + _hsv.Saturation * field.Width, field.Y + (1 - _hsv.Value) * field.Height);
        context.DrawEllipse(null, new Pen(Brushes.Black, 4), point, 7, 7);
        context.DrawEllipse(null, new Pen(Brushes.White, 2), point, 7, 7);
        if (IsKeyboardFocusWithin) context.DrawRectangle(null, new Pen(EditorThemeTokens.Accent, 1), new Rect(Bounds.Size).Deflate(1));
    }
    private void SetAt(Point point)
    {
        var field = Field;
        var x = Math.Clamp((point.X - field.X) / field.Width, 0, 1);
        var y = Math.Clamp((point.Y - field.Y) / field.Height, 0, 1);
        _hsv = HueStrip ? _hsv with { Hue = x * 359.999 } : _hsv with { Saturation = x, Value = 1 - y };
        InvalidateVisual(); ColorChanged?.Invoke(_hsv);
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_pointer is not null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(); _pointer = e.Pointer; _pointer.Capture(this); SetAt(e.GetPosition(this)); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    { base.OnPointerMoved(e); if (ReferenceEquals(_pointer, e.Pointer)) { SetAt(e.GetPosition(this)); e.Handled = true; } }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!ReferenceEquals(_pointer, e.Pointer) || e.InitialPressMouseButton != MouseButton.Left) return;
        SetAt(e.GetPosition(this)); _pointer = null; e.Pointer.Capture(null); e.Handled = true;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var step = (e.KeyModifiers & KeyModifiers.Shift) != 0 ? .1 : .01;
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return;
        if (HueStrip) _hsv = _hsv with { Hue = Math.Clamp(_hsv.Hue + (e.Key is Key.Right or Key.Up ? 1 : -1) * step * 360, 0, 359.999) };
        else if (e.Key is Key.Left or Key.Right) _hsv = _hsv with { Saturation = Math.Clamp(_hsv.Saturation + (e.Key == Key.Right ? step : -step), 0, 1) };
        else _hsv = _hsv with { Value = Math.Clamp(_hsv.Value + (e.Key == Key.Up ? step : -step), 0, 1) };
        InvalidateVisual(); ColorChanged?.Invoke(_hsv); e.Handled = true;
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { var pointer = _pointer; _pointer = null; pointer?.Capture(null); base.OnDetachedFromVisualTree(e); }
    protected override AutomationPeer OnCreateAutomationPeer() => new SpectrumPeer(this);
    private sealed class SpectrumPeer(ColorSpectrum owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
    }
}
