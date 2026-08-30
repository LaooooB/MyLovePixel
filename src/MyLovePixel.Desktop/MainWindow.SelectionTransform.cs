using Avalonia.Input;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private sealed record SelectionTransformGesture(
        SelectionTransformOperation Operation,
        IntRect StartBounds,
        double StartCanvasX,
        double StartCanvasY,
        double StartRotationAngle);

    private readonly record struct SelectionTransformResult(
        SelectionTransformPreview Preview,
        IntRect Bounds,
        int DeltaX,
        int DeltaY,
        double RotationDegrees);

    private SelectionTransformGesture? _selectionTransformGesture;

    private void DispatchSelectionTransform(SelectionTransformPointerEvent e)
    {
        var session = Current();
        if (!_selectionMode || session is null) return;

        switch (e.Phase)
        {
            case SelectionTransformPhase.Pressed:
            {
                var overlay = _selection.GetOverlay(session);
                if (overlay is null) return;
                var centerX = overlay.Bounds.X + overlay.Bounds.Width * 0.5d;
                var centerY = overlay.Bounds.Y + overlay.Bounds.Height * 0.5d;
                _selectionTransformGesture = new SelectionTransformGesture(
                    e.Operation,
                    overlay.Bounds,
                    e.CanvasX,
                    e.CanvasY,
                    AngleDegrees(e.CanvasX - centerX, e.CanvasY - centerY));
                _selectionStart = null;
                _selectionVertices.Clear();
                _canvasPointerActive = true;
                _canvas.SetSelectionTransformPreview(new SelectionTransformPreview(
                    overlay.Bounds.X,
                    overlay.Bounds.Y,
                    overlay.Bounds.Width,
                    overlay.Bounds.Height,
                    0d));
                break;
            }

            case SelectionTransformPhase.Moved:
            {
                if (_selectionTransformGesture is not { } gesture || gesture.Operation != e.Operation) return;
                var result = ComputeSelectionTransform(gesture, e);
                _canvas.SetSelectionTransformPreview(result.Preview);
                break;
            }

            case SelectionTransformPhase.Released:
            {
                if (_selectionTransformGesture is not { } gesture || gesture.Operation != e.Operation) return;
                var result = ComputeSelectionTransform(gesture, e);
                _selectionTransformGesture = null;
                _canvasPointerActive = false;
                _canvas.SetSelectionTransformPreview(null);

                Safe(() => CommitSelectionTransform(session, gesture, result));
                QueueRefreshAll();
                break;
            }

            case SelectionTransformPhase.Canceled:
                CancelSelectionTransformGesture();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(e));
        }
    }

    private void CommitSelectionTransform(
        MyLovePixel.Application.DocumentSession session,
        SelectionTransformGesture gesture,
        SelectionTransformResult result)
    {
        switch (gesture.Operation)
        {
            case SelectionTransformOperation.Move:
                if (result.DeltaX != 0 || result.DeltaY != 0)
                    _selection.Move(session, result.DeltaX, result.DeltaY);
                break;

            case SelectionTransformOperation.ScaleTopLeft:
            case SelectionTransformOperation.ScaleTopRight:
            case SelectionTransformOperation.ScaleBottomLeft:
            case SelectionTransformOperation.ScaleBottomRight:
                if (!SameBounds(gesture.StartBounds, result.Bounds))
                    _selection.ScaleToBounds(session, result.Bounds);
                break;

            case SelectionTransformOperation.Rotate:
                if (Math.Abs(result.RotationDegrees) >= 0.000001d)
                    _selection.Rotate(session, result.RotationDegrees);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(gesture.Operation));
        }
    }

    private static SelectionTransformResult ComputeSelectionTransform(
        SelectionTransformGesture gesture,
        SelectionTransformPointerEvent e)
    {
        var start = gesture.StartBounds;
        var shift = (e.Modifiers & KeyModifiers.Shift) != 0;

        if (gesture.Operation == SelectionTransformOperation.Move)
        {
            var dx = RoundPixel(e.CanvasX - gesture.StartCanvasX);
            var dy = RoundPixel(e.CanvasY - gesture.StartCanvasY);
            if (shift)
            {
                if (Math.Abs(dx) >= Math.Abs(dy)) dy = 0;
                else dx = 0;
            }
            var bounds = new IntRect(
                checked(start.X + dx),
                checked(start.Y + dy),
                start.Width,
                start.Height);
            return new SelectionTransformResult(
                new SelectionTransformPreview(bounds.X, bounds.Y, bounds.Width, bounds.Height, 0d),
                bounds,
                dx,
                dy,
                0d);
        }

        if (gesture.Operation == SelectionTransformOperation.Rotate)
        {
            var centerX = start.X + start.Width * 0.5d;
            var centerY = start.Y + start.Height * 0.5d;
            var currentAngle = AngleDegrees(e.CanvasX - centerX, e.CanvasY - centerY);
            var rotation = NormalizeDegrees(currentAngle - gesture.StartRotationAngle);
            if (shift) rotation = Math.Round(rotation / 15d, MidpointRounding.AwayFromZero) * 15d;
            return new SelectionTransformResult(
                new SelectionTransformPreview(start.X, start.Y, start.Width, start.Height, rotation),
                start,
                0,
                0,
                rotation);
        }

        var pointerX = RoundPixel(e.CanvasX);
        var pointerY = RoundPixel(e.CanvasY);
        var fixedRight = checked(start.X + start.Width);
        var fixedBottom = checked(start.Y + start.Height);
        int width;
        int height;

        switch (gesture.Operation)
        {
            case SelectionTransformOperation.ScaleTopLeft:
                width = Math.Max(1, fixedRight - Math.Min(pointerX, fixedRight - 1));
                height = Math.Max(1, fixedBottom - Math.Min(pointerY, fixedBottom - 1));
                break;
            case SelectionTransformOperation.ScaleTopRight:
                width = Math.Max(1, Math.Max(pointerX, start.X + 1) - start.X);
                height = Math.Max(1, fixedBottom - Math.Min(pointerY, fixedBottom - 1));
                break;
            case SelectionTransformOperation.ScaleBottomLeft:
                width = Math.Max(1, fixedRight - Math.Min(pointerX, fixedRight - 1));
                height = Math.Max(1, Math.Max(pointerY, start.Y + 1) - start.Y);
                break;
            case SelectionTransformOperation.ScaleBottomRight:
                width = Math.Max(1, Math.Max(pointerX, start.X + 1) - start.X);
                height = Math.Max(1, Math.Max(pointerY, start.Y + 1) - start.Y);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(gesture.Operation));
        }

        if (shift)
        {
            var scaleX = width / (double)start.Width;
            var scaleY = height / (double)start.Height;
            if (Math.Abs(scaleX - 1d) >= Math.Abs(scaleY - 1d))
                height = Math.Max(1, RoundPixel(start.Height * scaleX));
            else
                width = Math.Max(1, RoundPixel(start.Width * scaleY));
        }

        var x = gesture.Operation is SelectionTransformOperation.ScaleTopLeft or SelectionTransformOperation.ScaleBottomLeft
            ? checked(fixedRight - width)
            : start.X;
        var y = gesture.Operation is SelectionTransformOperation.ScaleTopLeft or SelectionTransformOperation.ScaleTopRight
            ? checked(fixedBottom - height)
            : start.Y;
        var target = new IntRect(x, y, width, height);
        return new SelectionTransformResult(
            new SelectionTransformPreview(target.X, target.Y, target.Width, target.Height, 0d),
            target,
            0,
            0,
            0d);
    }

    private void CancelSelectionTransformGesture()
    {
        if (_selectionTransformGesture is null && !_canvasPointerActive) return;
        _selectionTransformGesture = null;
        _canvasPointerActive = false;
        _canvas.SetSelectionTransformPreview(null);
        RefreshCanvas(updatePreview: false);
    }

    private static bool SameBounds(IntRect a, IntRect b) =>
        a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;

    private static int RoundPixel(double value) =>
        checked((int)Math.Round(value, MidpointRounding.AwayFromZero));

    private static double AngleDegrees(double x, double y) => Math.Atan2(y, x) * 180d / Math.PI;

    private static double NormalizeDegrees(double degrees)
    {
        var result = degrees % 360d;
        if (result <= -180d) result += 360d;
        if (result > 180d) result -= 360d;
        return result;
    }
}
