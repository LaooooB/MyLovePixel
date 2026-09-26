using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed partial class PixelCanvasView
{
    private readonly PixelBitmapCache _bitmap = new();
    private IPointer? _capturedPointer;
    private bool _sampling;
    private bool _panning;
    private bool _drawing;
    private Point _lastPan;
    private bool _samplerCursor;
    private bool _panCursor;
    private bool _selectionCursor;
    private int _brushDiameter = 1;

    public Func<bool>? SamplingRequested { get; set; }
    public Func<bool>? PanningRequested { get; set; }
    public Action<int, int>? PixelSampleRequested { get; set; }
    public Action<Vector>? PanDeltaRequested { get; set; }
    public Action<double, Point>? ZoomAtRequested { get; set; }
    public bool HasActivePointer => _capturedPointer is not null;

    public PixelCanvasView()
    {
        ClipToBounds = true;
        Focusable = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        PointerCaptureLost += (_, _) => { if (!_releasingCapture) CancelActivePointer(); };
        PointerExited += (_, _) => { _hoveredPixel = null; HoverPixelChanged?.Invoke(null); InvalidateVisual(); };
    }

    public void SetInteractionAppearance(bool sampler, bool pan, bool selection, int brushDiameter)
    {
        _samplerCursor = sampler;
        _panCursor = pan;
        _selectionCursor = selection;
        _brushDiameter = Math.Clamp(brushDiameter, 1, 1024);
        Cursor = new Cursor(pan || _panning ? StandardCursorType.Hand : StandardCursorType.Cross);
        InvalidateVisual();
    }

    public void CancelActivePointer()
    {
        var pointer = _capturedPointer;
        _capturedPointer = null;
        var wasDrawing = _drawing;
        _sampling = _panning = _drawing = false;
        if (_activeSelectionTransform is { } operation)
        {
            _activeSelectionTransform = null;
            SelectionTransformInput?.Invoke(new SelectionTransformPointerEvent(operation, SelectionTransformPhase.Canceled, 0, 0, KeyModifiers.None));
        }
        else if (wasDrawing) CancelPointerInput?.Invoke();
        if (pointer is not null) ReleaseCapture(pointer);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_presentation is null) return;
        Focus();
        UpdateHover(e);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsMiddleButtonPressed || point.Properties.IsLeftButtonPressed && PanningRequested?.Invoke() == true)
        {
            _panning = true;
            _lastPan = e.GetPosition(TopLevel.GetTopLevel(this));
            Capture(e.Pointer);
            Cursor = new Cursor(StandardCursorType.Hand);
            e.Handled = true;
            return;
        }
        if (SamplingRequested?.Invoke() == true && (point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed))
        {
            _sampling = true;
            Capture(e.Pointer);
            SampleHover();
            e.Handled = true;
            return;
        }
        if (point.Properties.IsRightButtonPressed && _hoveredPixel is { } hover)
        {
            SecondaryPickRequested?.Invoke(hover.X, hover.Y);
            e.Handled = true;
            return;
        }
        if (!point.Properties.IsLeftButtonPressed) return;
        if (TryBeginSelectionTransform(e)) { Capture(e.Pointer); e.Handled = true; return; }
        if (_hoveredPixel is null) return;
        _drawing = true;
        Capture(e.Pointer);
        DispatchPointer(e, EditorPointerKind.Pressed);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_presentation is null) return;
        var oldHover = _hoveredPixel;
        UpdateHover(e);
        if (!ReferenceEquals(e.Pointer.Captured, this)) return;
        if (_panning)
        {
            var position = e.GetPosition(TopLevel.GetTopLevel(this));
            PanDeltaRequested?.Invoke(position - _lastPan);
            _lastPan = position;
        }
        else if (_sampling) { if (oldHover != _hoveredPixel) SampleHover(); }
        else if (_activeSelectionTransform is { } operation) DispatchSelectionTransform(e, operation, SelectionTransformPhase.Moved);
        else if (_drawing) DispatchPointer(e, EditorPointerKind.Moved);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_presentation is null || !ReferenceEquals(e.Pointer.Captured, this)) return;
        UpdateHover(e);
        if (_sampling) SampleHover();
        else if (_activeSelectionTransform is { } operation)
        {
            _activeSelectionTransform = null;
            DispatchSelectionTransform(e, operation, SelectionTransformPhase.Released);
        }
        else if (_drawing) DispatchPointer(e, EditorPointerKind.Released);
        _drawing = _sampling = _panning = false;
        _capturedPointer = null;
        ReleaseCapture(e.Pointer);
        Cursor = new Cursor(_panCursor ? StandardCursorType.Hand : StandardCursorType.Cross);
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (HasActivePointer || e.Delta.Y == 0) { e.Handled = true; return; }
        if (ZoomAtRequested is { } zoom) zoom(e.Delta.Y > 0 ? 1.25 : 0.8, e.GetPosition(this));
        else ZoomFactorRequested?.Invoke(e.Delta.Y > 0 ? 1.25 : 0.8);
        e.Handled = true;
    }

    private void Capture(IPointer pointer) { _capturedPointer = pointer; pointer.Capture(this); }
    private void SampleHover() { if (_hoveredPixel is { } point) PixelSampleRequested?.Invoke(point.X, point.Y); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_presentation is not { } presentation) return;
        var bounds = new Rect(0, 0, presentation.Size.Width * _zoom, presentation.Size.Height * _zoom);
        context.FillRectangle(PixelBackdrop.Checker, bounds);
        _bitmap.Update(presentation, _invert);
        if (_bitmap.Image is { } image)
            context.DrawImage(image, new Rect(0, 0, presentation.Size.Width, presentation.Size.Height), bounds);
        foreach (var pixel in presentation.PreviewPixels)
        {
            var rect = new Rect(pixel.Point.X * _zoom, pixel.Point.Y * _zoom, _zoom, _zoom);
            context.FillRectangle(PixelBackdrop.Checker, rect);
            DrawPixel(context, pixel.Point.X, pixel.Point.Y, pixel.Color.R, pixel.Color.G, pixel.Color.B, pixel.Color.A);
        }
        if (_grid && _zoom >= 8)
        {
            var pen = new Pen(EditorThemeTokens.GridLine, 1);
            for (var x = 1; x < presentation.Size.Width; x++) context.DrawLine(pen, new Point(x * _zoom, 0), new Point(x * _zoom, bounds.Height));
            for (var y = 1; y < presentation.Size.Height; y++) context.DrawLine(pen, new Point(0, y * _zoom), new Point(bounds.Width, y * _zoom));
        }
        if (_selection is { } selection) DrawSelection(context, selection);
        foreach (var region in presentation.DirtyRegions)
            context.DrawRectangle(null, new Pen(EditorThemeTokens.DirtyRegionOutline, 1), new Rect(region.X * _zoom, region.Y * _zoom, region.Width * _zoom, region.Height * _zoom));
        if (_hoveredPixel is { } hover && !_panCursor && !_panning)
        {
            var diameter = _samplerCursor || _selectionCursor ? 1 : _brushDiameter;
            var offset = (diameter - 1) / 2;
            var rect = new Rect((hover.X - offset) * _zoom, (hover.Y - offset) * _zoom, diameter * _zoom, diameter * _zoom);
            context.DrawRectangle(null, new Pen(Brushes.Black, 3), rect);
            context.DrawRectangle(null, new Pen(Brushes.White, 1), rect);
        }
        context.DrawRectangle(null, new Pen(EditorThemeTokens.StrongBorder, 1), bounds);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelActivePointer();
        _bitmap.Dispose();
        base.OnDetachedFromVisualTree(e);
    }
}
