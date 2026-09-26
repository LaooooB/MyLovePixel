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

public sealed partial class PixelCanvasView : Control
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
        foreach (var rawCorner in corners)
        {
            var corner = HandlePoint(rawCorner);
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
        else if (Distance(pointer, HandlePoint(corners[0])) <= TransformHandleRadius + 4d)
            operation = SelectionTransformOperation.ScaleTopLeft;
        else if (Distance(pointer, HandlePoint(corners[1])) <= TransformHandleRadius + 4d)
            operation = SelectionTransformOperation.ScaleTopRight;
        else if (Distance(pointer, HandlePoint(corners[2])) <= TransformHandleRadius + 4d)
            operation = SelectionTransformOperation.ScaleBottomRight;
        else if (Distance(pointer, HandlePoint(corners[3])) <= TransformHandleRadius + 4d)
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

    private Point HandlePoint(Point point)
    {
        // Keep hit testing and the visible handle on the same point without
        // changing the selection's artwork coordinates.
        const double inset = RotateHandleRadius + 1d;
        var xInset = Math.Min(inset, Bounds.Width / 2d);
        var yInset = Math.Min(inset, Bounds.Height / 2d);
        return new Point(Math.Clamp(point.X, xInset, Math.Max(xInset, Bounds.Width - xInset)),
            Math.Clamp(point.Y, yInset, Math.Max(yInset, Bounds.Height - yInset)));
    }

    private Point GetRotateHandle(IReadOnlyList<Point> corners)
    {
        var topMiddle = Midpoint(corners[0], corners[1]);
        var edgeX = corners[1].X - corners[0].X;
        var edgeY = corners[1].Y - corners[0].Y;
        var length = Math.Sqrt(edgeX * edgeX + edgeY * edgeY);
        if (length < 0.000001d) return HandlePoint(topMiddle);
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
        return HandlePoint(candidate);
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
