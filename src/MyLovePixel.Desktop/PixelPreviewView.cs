using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

/// <summary>A read-only view of the final composite with its own zoom and pan.</summary>
internal sealed class PixelPreviewView : Control
{
    private readonly PixelBitmapCache _bitmap = new();
    private CanvasPresentation? _presentation;
    private double _zoom = 1;
    private bool _fit = true;
    private Vector _pan;
    private IPointer? _pointer;
    private Point _lastPosition;
    private MouseButton _dragButton;

    public PixelPreviewView()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        SizeChanged += (_, _) => { ClampPan(); NotifyViewChanged(); };
        PointerCaptureLost += (_, _) => _pointer = null;
    }

    public CanvasPresentation? Presentation => _presentation;
    public bool IsFit => _fit;
    public Vector PanOffset => _pan;
    public double EffectiveZoom
    {
        get
        {
            if (!_fit || _presentation is null || Bounds.Width <= 0 || Bounds.Height <= 0) return _zoom;
            var fit = Math.Min(Bounds.Width / _presentation.Size.Width, Bounds.Height / _presentation.Size.Height);
            return fit >= 1 ? Math.Max(1, Math.Floor(fit)) : fit;
        }
    }
    public event Action? ViewChanged;
    public Action? EnlargeRequested { get; set; }

    public void SetPresentation(CanvasPresentation? presentation)
    {
        var sizeChanged = _presentation?.Size != presentation?.Size;
        _presentation = presentation;
        _bitmap.Update(presentation);
        if (sizeChanged) { _fit = true; _pan = default; }
        ClampPan();
        NotifyViewChanged();
    }

    public void Fit()
    {
        _fit = true;
        _pan = default;
        NotifyViewChanged();
    }

    public void ZoomBy(double factor) => ZoomAt(EffectiveZoom * factor, new Point(Bounds.Width / 2, Bounds.Height / 2));
    public void ActualSize() => ZoomAt(1, new Point(Bounds.Width / 2, Bounds.Height / 2));

    private void ZoomAt(double zoom, Point anchor)
    {
        if (_presentation is null || !double.IsFinite(zoom) || zoom <= 0) return;
        var previous = EffectiveZoom;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var relative = (anchor - center - _pan) / previous;
        _zoom = Math.Clamp(zoom, 1d / 16, 64);
        _fit = false;
        _pan = anchor - center - relative * _zoom;
        ClampPan();
        NotifyViewChanged();
    }

    private void ClampPan()
    {
        if (_fit || _presentation is null) { _pan = default; return; }
        var x = Math.Max(0, (_presentation.Size.Width * EffectiveZoom - Bounds.Width) / 2);
        var y = Math.Max(0, (_presentation.Size.Height * EffectiveZoom - Bounds.Height) / 2);
        _pan = new Vector(Math.Clamp(_pan.X, -x, x), Math.Clamp(_pan.Y, -y, y));
    }

    private void NotifyViewChanged() { InvalidateVisual(); ViewChanged?.Invoke(); }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_pointer is null && e.Delta.Y != 0)
            ZoomAt(EffectiveZoom * (e.Delta.Y > 0 ? 2 : .5), e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_pointer is not null || _presentation is null) return;
        var properties = e.GetCurrentPoint(this).Properties;
        if (!properties.IsLeftButtonPressed && !properties.IsMiddleButtonPressed) return;
        Focus();
        if (e.ClickCount == 2 && properties.IsLeftButtonPressed)
        {
            if (EnlargeRequested is { } enlarge) enlarge(); else Fit();
        }
        else
        {
            _dragButton = properties.IsMiddleButtonPressed ? MouseButton.Middle : MouseButton.Left;
            _lastPosition = e.GetPosition(this);
            _pointer = e.Pointer;
            _pointer.Capture(this);
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!ReferenceEquals(e.Pointer, _pointer)) return;
        var p = e.GetPosition(this);
        _pan += p - _lastPosition;
        _lastPosition = p;
        ClampPan();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!ReferenceEquals(e.Pointer, _pointer) || e.InitialPressMouseButton != _dragButton) return;
        _pointer = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyModifiers != KeyModifiers.None) return;
        switch (e.Key)
        {
            case Key.Add: case Key.OemPlus: ZoomBy(2); break;
            case Key.Subtract: case Key.OemMinus: ZoomBy(.5); break;
            case Key.F: Fit(); break;
            case Key.D1: case Key.NumPad1: ActualSize(); break;
            default: return;
        }
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        // Display-only white: the bitmap retains its original RGBA values.
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));
        if (_presentation is not { } p || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        _bitmap.Update(p);
        if (_bitmap.Image is not { } image) return;
        var scale = EffectiveZoom;
        var width = p.Size.Width * scale;
        var height = p.Size.Height * scale;
        var x = Math.Round((Bounds.Width - width) / 2 + _pan.X);
        var y = Math.Round((Bounds.Height - height) / 2 + _pan.Y);
        context.DrawImage(image, new Rect(0, 0, p.Size.Width, p.Size.Height), new Rect(x, y, width, height));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PreviewAutomationPeer(this);

    private sealed class PreviewAutomationPeer(PixelPreviewView owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var pointer = _pointer; _pointer = null; pointer?.Capture(null);
        _bitmap.Dispose();
        base.OnDetachedFromVisualTree(e);
    }
}
