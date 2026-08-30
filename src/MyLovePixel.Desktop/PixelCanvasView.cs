using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Desktop;

public enum SelectionTransformOperation
{
    Move,
    ScaleTopLeft,
    ScaleTopRight,
    ScaleBottomLeft,
    ScaleBottomRight,
    Rotate,
}

public enum SelectionTransformPhase
{
    Pressed,
    Moved,
    Released,
    Canceled,
}

public readonly record struct SelectionTransformPointerEvent(
    SelectionTransformOperation Operation,
    SelectionTransformPhase Phase,
    double CanvasX,
    double CanvasY,
    KeyModifiers Modifiers);

public readonly record struct SelectionTransformPreview(
    double X,
    double Y,
    double Width,
    double Height,
    double RotationDegrees);

public sealed class PixelCanvasView : Control
{
    private const double TransformHandleRadius = 7d;
    private const double RotateHandleRadius = 8d;
    private const double RotateHandleOffset = 24d;

    private readonly Dictionary<uint, IBrush> _brushes = [];
    private CanvasPresentation? _presentation;
    private SelectionOverlayPresentation? _selection;
    private IntPoint? _hoveredPixel;
    private double _zoom = 1d;
    private bool _invert;
    private bool _grid = true;
    private bool _selectionTransformEnabled;
    private SelectionTransformOperation? _activeSelectionTransform;
    private SelectionTransformPreview? _selectionTransformPreview;
    private bool _releasingCapture;

    public PixelCanvasView()
    {
        ClipToBounds = true;
        Focusable = true;
        PointerCaptureLost += (_, _) =>
        {
            if (_releasingCapture) return;
            if (_activeSelectionTransform is { } operation)
            {
                _activeSelectionTransform = null;
                SelectionTransformInput?.Invoke(new SelectionTransformPointerEvent(
                    operation,
                    SelectionTransformPhase.Canceled,
                    0d,
                    0d,
                    KeyModifiers.None));
                return;
            }
            CancelPointerInput?.Invoke();
        };
        PointerExited += (_, _) =>
        {
            _hoveredPixel = null;
            HoverPixelChanged?.Invoke(null);
            InvalidateVisual();
        };
    }

    public Action<EditorPointerEvent>? PointerInput { get; set; }
    public Action<SelectionTransformPointerEvent>? SelectionTransformInput { get; set; }
    public Action? CancelPointerInput { get; set; }
    public Action<(int X, int Y)?>? HoverPixelChanged { get; set; }
    public Action<int, int>? SecondaryPickRequested { get; set; }
    public Action<double>? ZoomFactorRequested { get; set; }
    public CanvasPresentation? Presentation => _presentation;
    public double Zoom => _zoom;

    public void SetPresentation(CanvasPresentation? presentation, double zoom, SelectionOverlayPresentation? selection = null)
    {
        if (!double.IsFinite(zoom) || zoom <= 0d) throw new ArgumentOutOfRangeException(nameof(zoom));
        _presentation = presentation;
        _selection = selection;
        _zoom = zoom;
        Width = presentation is null ? 1d : presentation.Size.Width * zoom;
        Height = presentation is null ? 1d : presentation.Size.Height * zoom;
        InvalidateVisual();
    }

    public void SetSelectionTransformEnabled(bool enabled)
    {
        if (_selectionTransformEnabled == enabled) return;
        _selectionTransformEnabled = enabled;
        if (!enabled && _activeSelectionTransform is null)
            _selectionTransformPreview = null;
        InvalidateVisual();
    }

    public void SetSelectionTransformPreview(SelectionTransformPreview? preview)
    {
        _selectionTransformPreview = preview;
        InvalidateVisual();
    }

    public void SetInvert(bool invert)
    {
        if (_invert == invert) return;
        _invert = invert;
        _brushes.Clear();
        InvalidateVisual();
    }

    public void SetGrid(bool grid)
    {
        if (_grid == grid) return;
        _grid = grid;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
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

        if (_grid && _zoom >= 8d)
        {
            var pen = new Pen(EditorThemeTokens.GridLine, 1d);
            for (var x = 1; x < presentation.Size.Width; x++)
                context.DrawLine(pen, new Point(x * _zoom, 0), new Point(x * _zoom, presentation.Size.Height * _zoom));
            for (var y = 1; y < presentation.Size.Height; y++)
                context.DrawLine(pen, new Point(0, y * _zoom), new Point(presentation.Size.Width * _zoom, y * _zoom));
        }

        if (_selection is { } selection)
            DrawSelection(context, selection);

        if (presentation.DirtyRegions.Count != 0)
        {
            var pen = new Pen(EditorThemeTokens.DirtyRegionOutline, 1d);
            foreach (var region in presentation.DirtyRegions)
            {
                var rect = new Rect(region.X * _zoom, region.Y * _zoom, region.Width * _zoom, region.Height * _zoom);
                context.DrawRectangle(null, pen, rect);
            }
        }

        if (_hoveredPixel is { } hover &&
            (uint)hover.X < (uint)presentation.Size.Width &&
            (uint)hover.Y < (uint)presentation.Size.Height)
        {
            var rect = new Rect(hover.X * _zoom, hover.Y * _zoom, _zoom, _zoom);
            context.FillRectangle(EditorThemeTokens.HoverCell, rect);
            context.DrawRectangle(null, new Pen(EditorThemeTokens.HoverCellOutline, Math.Min(2d, Math.Max(1d, _zoom / 8d))), rect);
        }

        context.DrawRectangle(null, new Pen(EditorThemeTokens.StrongBorder, 1d), new Rect(0, 0, presentation.Size.Width * _zoom, presentation.Size.Height * _zoom));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_presentation is null) return;
        Focus();
        UpdateHover(e);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed && _hoveredPixel is { } hover)
        {
            SecondaryPickRequested?.Invoke(hover.X, hover.Y);
            e.Handled = true;
            return;
        }

        if (point.Properties.IsLeftButtonPressed && TryBeginSelectionTransform(e))
        {
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        e.Pointer.Capture(this);
        DispatchPointer(e, EditorPointerKind.Pressed);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_presentation is null) return;
        UpdateHover(e);
        if (!ReferenceEquals(e.Pointer.Captured, this)) return;

        if (_activeSelectionTransform is { } operation)
        {
            DispatchSelectionTransform(e, operation, SelectionTransformPhase.Moved);
            e.Handled = true;
            return;
        }

        DispatchPointer(e, EditorPointerKind.Moved);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_presentation is null) return;
        UpdateHover(e);
        if (!ReferenceEquals(e.Pointer.Captured, this)) return;

        if (_activeSelectionTransform is { } operation)
        {
            DispatchSelectionTransform(e, operation, SelectionTransformPhase.Released);
            _activeSelectionTransform = null;
            ReleaseCapture(e.Pointer);
            e.Handled = true;
            return;
        }

        DispatchPointer(e, EditorPointerKind.Released);
        ReleaseCapture(e.Pointer);
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
        ZoomFactorRequested?.Invoke(e.Delta.Y > 0 ? 1.25d : 0.8d);
        e.Handled = true;
    }

    private void DrawSelection(DrawingContext context, SelectionOverlayPresentation selection)
    {
        if (_selectionTransformPreview is { } preview)
        {
            DrawTransformPreviewPixels(context, selection, preview);
            DrawTransformFrame(context, preview, drawHandles: _selectionTransformEnabled);
            return;
        }

        if (_zoom >= 2d && selection.Pixels.Count <= 100_000)
        {
            foreach (var point in selection.Pixels)
                context.FillRectangle(EditorThemeTokens.SelectionFill, new Rect(point.X * _zoom, point.Y * _zoom, _zoom, _zoom));
        }

        var b = selection.Bounds;
        var basePreview = new SelectionTransformPreview(b.X, b.Y, b.Width, b.Height, 0d);
        DrawTransformFrame(context, basePreview, drawHandles: _selectionTransformEnabled);
    }

    private void DrawTransformPreviewPixels(
        DrawingContext context,
        SelectionOverlayPresentation selection,
        SelectionTransformPreview preview)
    {
        if (_zoom < 2d || selection.Pixels.Count > 100_000) return;
        var bounds = selection.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var pixelWidth = Math.Max(1d, Math.Abs(preview.Width / bounds.Width) * _zoom);
        var pixelHeight = Math.Max(1d, Math.Abs(preview.Height / bounds.Height) * _zoom);
        var centerX = preview.X + preview.Width * 0.5d;
        var centerY = preview.Y + preview.Height * 0.5d;
        var radians = preview.RotationDegrees * Math.PI / 180d;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        foreach (var point in selection.Pixels)
        {
            var u = ((point.X + 0.5d) - bounds.X) / bounds.Width;
            var v = ((point.Y + 0.5d) - bounds.Y) / bounds.Height;
            var unrotatedX = preview.X + u * preview.Width;
            var unrotatedY = preview.Y + v * preview.Height;
            var dx = unrotatedX - centerX;
            var dy = unrotatedY - centerY;
            var rotatedX = centerX + (cos * dx) - (sin * dy);
            var rotatedY = centerY + (sin * dx) + (cos * dy);
            context.FillRectangle(
                EditorThemeTokens.SelectionFill,
                new Rect(
                    rotatedX * _zoom - pixelWidth * 0.5d,
                    rotatedY * _zoom - pixelHeight * 0.5d,
                    pixelWidth,
                    pixelHeight));
        }
    }

    private void DrawTransformFrame(DrawingContext context, SelectionTransformPreview preview, bool drawHandles)
    {
        var corners = GetTransformCorners(preview);
        var pen = new Pen(EditorThemeTokens.SelectionOutline, 1.5d);
        for (var index = 0; index < corners.Length; index++)
            context.DrawLine(pen, corners[index], corners[(index + 1) % corners.Length]);

        if (!drawHandles) return;
        foreach (var corner in corners)
        {
            context.FillRectangle(
                EditorThemeTokens.SurfaceRaised,
                new Rect(
                    corner.X - TransformHandleRadius,
                    corner.Y - TransformHandleRadius,
                    TransformHandleRadius * 2d,
                    TransformHandleRadius * 2d));
            context.DrawRectangle(
                null,
                new Pen(EditorThemeTokens.SelectionOutline, 1.5d),
                new Rect(
                    corner.X - TransformHandleRadius,
                    corner.Y - TransformHandleRadius,
                    TransformHandleRadius * 2d,
                    TransformHandleRadius * 2d));
        }

        var topMiddle = Midpoint(corners[0], corners[1]);
        var rotateHandle = GetRotateHandle(corners);
        context.DrawLine(pen, topMiddle, rotateHandle);
        context.DrawEllipse(
            EditorThemeTokens.SurfaceRaised,
            new Pen(EditorThemeTokens.SelectionOutline, 1.5d),
            rotateHandle,
            RotateHandleRadius,
            RotateHandleRadius);
    }

    private bool TryBeginSelectionTransform(PointerPressedEventArgs e)
    {
        if (!_selectionTransformEnabled || _selection is not { } selection || SelectionTransformInput is null)
            return false;

        var pointer = e.GetPosition(this);
        var b = selection.Bounds;
        var preview = new SelectionTransformPreview(b.X, b.Y, b.Width, b.Height, 0d);
        var corners = GetTransformCorners(preview);
        var rotateHandle = GetRotateHandle(corners);
        SelectionTransformOperation? operation = null;

        if (Distance(pointer, rotateHandle) <= RotateHandleRadius + 4d)
            operation = SelectionTransformOperation.Rotate;
        else if (Distance(pointer, corners[0]) <= TransformHandleRadius + 4d)
            operation = SelectionTransformOperation.ScaleTopLeft;
        else if (Distance(pointer, corners[1]) <= TransformHandleRadius + 4d)
            operation = SelectionTransformOperation.ScaleTopRight;
        else if (Distance(pointer, corners[2]) <= TransformHandleRadius + 4d)
            operation = SelectionTransformOperation.ScaleBottomRight;
        else if (Distance(pointer, corners[3]) <= TransformHandleRadius + 4d)
            operation = SelectionTransformOperation.ScaleBottomLeft;
        else
        {
            var rect = new Rect(b.X * _zoom, b.Y * _zoom, b.Width * _zoom, b.Height * _zoom);
            if (rect.Contains(pointer)) operation = SelectionTransformOperation.Move;
        }

        if (operation is not { } value) return false;
        _activeSelectionTransform = value;
        DispatchSelectionTransform(e, value, SelectionTransformPhase.Pressed);
        return true;
    }

    private void DispatchSelectionTransform(
        PointerEventArgs e,
        SelectionTransformOperation operation,
        SelectionTransformPhase phase)
    {
        var p = e.GetPosition(this);
        SelectionTransformInput?.Invoke(new SelectionTransformPointerEvent(
            operation,
            phase,
            p.X / _zoom,
            p.Y / _zoom,
            e.KeyModifiers));
    }

    private Point[] GetTransformCorners(SelectionTransformPreview preview)
    {
        var centerX = (preview.X + preview.Width * 0.5d) * _zoom;
        var centerY = (preview.Y + preview.Height * 0.5d) * _zoom;
        var halfWidth = preview.Width * _zoom * 0.5d;
        var halfHeight = preview.Height * _zoom * 0.5d;
        var radians = preview.RotationDegrees * Math.PI / 180d;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        Point Rotate(double x, double y) => new(
            centerX + (cos * x) - (sin * y),
            centerY + (sin * x) + (cos * y));

        return
        [
            Rotate(-halfWidth, -halfHeight),
            Rotate(halfWidth, -halfHeight),
            Rotate(halfWidth, halfHeight),
            Rotate(-halfWidth, halfHeight),
        ];
    }

    private Point GetRotateHandle(IReadOnlyList<Point> corners)
    {
        var topMiddle = Midpoint(corners[0], corners[1]);
        var edgeX = corners[1].X - corners[0].X;
        var edgeY = corners[1].Y - corners[0].Y;
        var length = Math.Sqrt(edgeX * edgeX + edgeY * edgeY);
        if (length < 0.000001d) return topMiddle;
        var normalX = edgeY / length;
        var normalY = -edgeX / length;
        var candidate = new Point(
            topMiddle.X + normalX * RotateHandleOffset,
            topMiddle.Y + normalY * RotateHandleOffset);

        if (candidate.X < RotateHandleRadius || candidate.Y < RotateHandleRadius ||
            candidate.X > Bounds.Width - RotateHandleRadius || candidate.Y > Bounds.Height - RotateHandleRadius)
        {
            candidate = new Point(
                topMiddle.X - normalX * RotateHandleOffset,
                topMiddle.Y - normalY * RotateHandleOffset);
        }
        return candidate;
    }

    private void UpdateHover(PointerEventArgs e)
    {
        var presentation = _presentation;
        if (presentation is null) return;
        var p = e.GetPosition(this);
        var x = (int)Math.Floor(p.X / _zoom);
        var y = (int)Math.Floor(p.Y / _zoom);
        IntPoint? next = (uint)x < (uint)presentation.Size.Width && (uint)y < (uint)presentation.Size.Height
            ? new IntPoint(x, y)
            : null;
        if (_hoveredPixel == next) return;
        _hoveredPixel = next;
        HoverPixelChanged?.Invoke(next is { } value ? (value.X, value.Y) : null);
        InvalidateVisual();
    }

    private void DispatchPointer(PointerEventArgs e, EditorPointerKind kind)
    {
        var presentation = _presentation;
        if (presentation is null) return;
        var point = e.GetCurrentPoint(this);
        var x = Math.Clamp((int)Math.Floor(point.Position.X / _zoom), 0, presentation.Size.Width - 1);
        var y = Math.Clamp((int)Math.Floor(point.Position.Y / _zoom), 0, presentation.Size.Height - 1);
        var properties = point.Properties;
        var buttons = EditorPointerButtons.None;
        if (properties.IsLeftButtonPressed) buttons |= EditorPointerButtons.Primary;
        if (properties.IsRightButtonPressed) buttons |= EditorPointerButtons.Secondary;
        if (properties.IsMiddleButtonPressed) buttons |= EditorPointerButtons.Middle;
        if (properties.IsBarrelButtonPressed) buttons |= EditorPointerButtons.Barrel;
        if (properties.IsEraser) buttons |= EditorPointerButtons.Eraser;

        var modifiers = EditorInputModifiers.None;
        if ((e.KeyModifiers & KeyModifiers.Shift) != 0) modifiers |= EditorInputModifiers.Shift;
        if ((e.KeyModifiers & KeyModifiers.Control) != 0) modifiers |= EditorInputModifiers.Control;
        if ((e.KeyModifiers & KeyModifiers.Alt) != 0) modifiers |= EditorInputModifiers.Alt;
        if ((e.KeyModifiers & KeyModifiers.Meta) != 0) modifiers |= EditorInputModifiers.Meta;

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
            new IntPoint(x, y),
            properties.Pressure,
            buttons,
            modifiers,
            unchecked((long)e.Timestamp)));
    }

    private void ReleaseCapture(IPointer pointer)
    {
        _releasingCapture = true;
        try { pointer.Capture(null); }
        finally { _releasingCapture = false; }
    }

    private void DrawPixel(DrawingContext context, int x, int y, byte r, byte g, byte b, byte a)
    {
        var rect = new Rect(x * _zoom, y * _zoom, _zoom, _zoom);
        if (a == 0)
        {
            context.FillRectangle(((x + y) & 1) == 0 ? EditorThemeTokens.CheckerLight : EditorThemeTokens.CheckerDark, rect);
            return;
        }
        if (_invert)
        {
            r = (byte)(255 - r);
            g = (byte)(255 - g);
            b = (byte)(255 - b);
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

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Point Midpoint(Point a, Point b) => new((a.X + b.X) * 0.5d, (a.Y + b.Y) * 0.5d);
}
