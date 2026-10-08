using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private ScrollViewer? _canvasScroll;
    private IPointer? _panPointer;
    private Point _panStart;
    private Point _panCanvasOrigin;
    private Vector _panOffset;
    private Vector _panCurrentOffset;
    private Cursor? _panPreviousCursor;
    private bool _panWithSpace;
    private bool _navigationSpaceHeld;
    private bool _cameraCommitPending;
    private readonly MatrixTransform _cameraTransform = new();
    private static readonly Cursor PanCursor = new(StandardCursorType.Hand);
    private bool _zoomAnimating;
    private bool _zoomFramePending;
    private bool _navigationClosed;
    private bool _pointerStatusQueued;
    private double _zoomFrom;
    private double _zoomTarget;
    private double _zoomBaseZoom;
    private long _zoomStarted;
    private Point _zoomAnchor;
    private Point _zoomSourceAnchor;
    private Point _zoomBaseFrameOrigin;
    private Point _zoomBaseCanvasLocal;
    private DocumentSession? _zoomSession;

    private void InitializeCanvasNavigation(ScrollViewer host)
    {
        if (_comfortCanvasFrame is { } frame)
            frame.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
        // Consume navigation before any tool can create a stroke.
        host.AddHandler(PointerPressedEvent, (_, e) =>
        {
            var properties = e.GetCurrentPoint(host).Properties;
            var pan = properties.IsMiddleButtonPressed || (properties.IsLeftButtonPressed && _navigationSpaceHeld);
            if (!pan)
            {
                if (properties.IsLeftButtonPressed) { StopCanvasZoom(); FinishCameraLayout(); }
                return;
            }
            if (IsScrollbarSource(e.Source) || _panPointer is not null) return;
            StopCanvasZoom(); FinishCameraLayout();
            if (_canvasPointerActive || _selectionStart is not null) CancelCanvasInteraction();
            _panStart = e.GetPosition(host);
            _panCanvasOrigin = _canvas.TranslatePoint(default, host) ?? default;
            _panCurrentOffset = _panOffset = host.Offset;
            _panWithSpace = !properties.IsMiddleButtonPressed;
            _panPreviousCursor = host.Cursor;
            _panPointer = e.Pointer;
            _cameraTransform.Matrix = Matrix.Identity;
            if (_comfortCanvasFrame is { } content) content.RenderTransform = _cameraTransform;
            host.Cursor = PanCursor;
            e.Pointer.Capture(host);
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
        host.AddHandler(PointerMovedEvent, (_, e) =>
        {
            if (_panPointer != e.Pointer) return;
            var properties = e.GetCurrentPoint(host).Properties;
            if (!(_panWithSpace ? properties.IsLeftButtonPressed : properties.IsMiddleButtonPressed))
            { EndCanvasPan(); return; }
            var requested = _panOffset - (e.GetPosition(host) - _panStart);
            _panCurrentOffset = new Vector(
                Math.Clamp(requested.X, 0, Math.Max(0, host.Extent.Width - host.Viewport.Width)),
                Math.Clamp(requested.Y, 0, Math.Max(0, host.Extent.Height - host.Viewport.Height)));
            var delta = _panOffset - _panCurrentOffset;
            // A compositor translation follows the pointer immediately. The scroll
            // layout and its scrollbars are committed only when the gesture ends.
            _cameraTransform.Matrix = Matrix.CreateTranslation(delta.X, delta.Y);
            _canvas.SetNavigationViewport(new Rect(-_panCanvasOrigin.X - delta.X, -_panCanvasOrigin.Y - delta.Y,
                host.Viewport.Width, host.Viewport.Height));
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
        host.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (_panPointer != e.Pointer) return;
            var properties = e.GetCurrentPoint(host).Properties;
            if (_panWithSpace ? !properties.IsLeftButtonPressed : !properties.IsMiddleButtonPressed)
                EndCanvasPan();
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
        host.PointerCaptureLost += (_, _) => EndCanvasPan();
        host.AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            if (e.Delta.Y == 0 || IsScrollbarSource(e.Source)) return;
            e.Handled = true;
            if (_panPointer is not null || e.GetCurrentPoint(host).Properties.IsLeftButtonPressed) return;
            AnimateCanvasZoom(Math.Pow(1.2, Math.Clamp(e.Delta.Y, -8d, 8d)), e.GetPosition(host));
        }, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None && !IsTextEntry(e.Source))
            { _navigationSpaceHeld = true; e.Handled = true; return; }
            if (e.Key != Key.Escape || _panPointer is null) return;
            EndCanvasPan(); e.Handled = true;
        }, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) => { if (e.Key == Key.Space) _navigationSpaceHeld = false; }, RoutingStrategies.Tunnel);
        host.SizeChanged += (_, _) =>
        {
            if (_comfortCanvasFrame is not null)
            {
                _comfortCanvasFrame.UseLayoutRounding = false;
                _comfortCanvasFrame.Margin = new Thickness(Math.Max(38, host.Bounds.Width * 0.5), Math.Max(38, host.Bounds.Height * 0.5));
            }
        };
        Deactivated += (_, _) => { _navigationSpaceHeld = false; EndCanvasPan(); StopCanvasZoom(); };
        Closed += (_, _) => { _navigationClosed = true; EndCanvasPan(); StopCanvasZoom(); };
    }

    private static bool IsScrollbarSource(object? source) => source is Control control &&
        (control is ScrollBar || control.GetVisualAncestors().OfType<ScrollBar>().Any());
    private static bool IsTextEntry(object? source) => source is Control control &&
        (control is TextBox or NumericUpDown or ComboBox || control.GetVisualAncestors().Any(x => x is TextBox or NumericUpDown or ComboBox));

    private void EndCanvasPan()
    {
        if (_panPointer is not { } pointer) return;
        _panPointer = null;
        if (_comfortCanvasFrame is { } frame) frame.RenderTransform = null;
        if (_canvasScroll is { } host)
        {
            host.Cursor = _panPreviousCursor;
            host.Offset = _panCurrentOffset;
            _cameraCommitPending = true;
        }
        if (ReferenceEquals(pointer.Captured, _canvasScroll)) pointer.Capture(null);
    }

    private void FinishCameraLayout()
    {
        if (!_cameraCommitPending || _navigationClosed) return;
        _cameraCommitPending = false;
        _canvasScroll?.UpdateLayout();
    }

    private Point CanvasViewportCenter() => _canvasScroll is { } host
        ? new Point(host.Viewport.Width * 0.5, host.Viewport.Height * 0.5) : default;

    private void ApplyCanvasZoom(double zoom, Point anchor)
    {
        if (Current() is not { } session) return;
        zoom = Math.Clamp(zoom, DocumentSession.MinimumZoom, DocumentSession.MaximumZoom);
        if (_canvasScroll is not { } host || _canvas.Presentation is null)
        { session.SetZoom(zoom); return; }
        if (_zoomAnimating && ReferenceEquals(_zoomSession, session) && _comfortCanvasFrame is { } frame)
        {
            session.SetZoom(zoom);
            var scale = zoom / _zoomBaseZoom;
            _cameraTransform.Matrix = new Matrix(scale, 0, 0, scale,
                anchor.X - _zoomSourceAnchor.X * zoom - _zoomBaseFrameOrigin.X - _zoomBaseCanvasLocal.X * scale,
                anchor.Y - _zoomSourceAnchor.Y * zoom - _zoomBaseFrameOrigin.Y - _zoomBaseCanvasLocal.Y * scale);
            frame.RenderTransform = _cameraTransform;
            _canvas.SetNavigationViewport(new Rect((_zoomSourceAnchor.X * zoom - anchor.X) / scale,
                (_zoomSourceAnchor.Y * zoom - anchor.Y) / scale, host.Viewport.Width / scale, host.Viewport.Height / scale));
            return;
        }
        var origin = _canvas.TranslatePoint(default, host) ?? default;
        var canvasPoint = (anchor - origin) / _canvas.Zoom;
        session.SetZoom(zoom);
        host.UpdateLayout();
        var newOrigin = _canvas.TranslatePoint(default, host) ?? default;
        host.Offset += newOrigin + canvasPoint * _canvas.Zoom - anchor;
        _cameraCommitPending = true;
    }

    private void CenterCanvasViewport()
    {
        if (_canvasScroll is not { } host) return;
        StopCanvasZoom();
        host.UpdateLayout();
        host.Offset = new Vector(Math.Max(0, (host.Extent.Width - host.Viewport.Width) * 0.5),
            Math.Max(0, (host.Extent.Height - host.Viewport.Height) * 0.5));
        _cameraCommitPending = true;
    }

    private void AnimateCanvasZoom(double factor, Point anchor)
    {
        if (!double.IsFinite(factor) || factor <= 0 || Current() is not { } session || _canvasScroll is not { } host || _comfortCanvasFrame is not { } frame) return;
        if (!_zoomAnimating)
        {
            FinishCameraLayout();
            _zoomBaseZoom = _canvas.Zoom;
            _zoomBaseFrameOrigin = frame.TranslatePoint(default, host) ?? default;
            _zoomBaseCanvasLocal = _canvas.TranslatePoint(default, frame) ?? default;
        }
        _zoomTarget = Math.Clamp((_zoomAnimating && ReferenceEquals(_zoomSession, session) ? _zoomTarget : session.Zoom) * factor,
            DocumentSession.MinimumZoom, DocumentSession.MaximumZoom);
        _zoomFrom = session.Zoom;
        _zoomAnchor = anchor;
        _zoomSourceAnchor = (anchor - (_canvas.TranslatePoint(default, host) ?? default)) / session.Zoom;
        _zoomSession = session;
        _zoomStarted = Stopwatch.GetTimestamp();
        _zoomAnimating = true;
        RequestCanvasZoomFrame();
    }

    private void RequestCanvasZoomFrame()
    {
        if (_zoomFramePending || _navigationClosed || !IsVisible) return;
        _zoomFramePending = true;
        RequestAnimationFrame(_ =>
        {
            _zoomFramePending = false;
            if (!_zoomAnimating || _navigationClosed || !ReferenceEquals(Current(), _zoomSession)) return;
            var progress = Math.Clamp(Stopwatch.GetElapsedTime(_zoomStarted).TotalMilliseconds / 75d, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 3);
            var zoom = Math.Exp(Math.Log(_zoomFrom) + (Math.Log(_zoomTarget) - Math.Log(_zoomFrom)) * eased);
            ApplyCanvasZoom(progress >= 1 ? _zoomTarget : zoom, _zoomAnchor);
            if (progress >= 1) StopCanvasZoom();
            else RequestCanvasZoomFrame();
        });
    }

    private void StopCanvasZoom()
    {
        if (!_zoomAnimating) return;
        var session = _zoomSession;
        _zoomAnimating = false;
        _zoomSession = null;
        if (_comfortCanvasFrame is { } frame) frame.RenderTransform = null;
        if (_navigationClosed || session is null || !ReferenceEquals(session, Current()) || _canvasScroll is not { } host) return;
        _canvas.SetViewZoom(session.Zoom);
        // One final layout establishes exact pixel coordinates before drawing.
        // There is no forced layout in the animation-frame path above.
        host.UpdateLayout();
        var origin = _canvas.TranslatePoint(default, host) ?? default;
        host.Offset += origin + _zoomSourceAnchor * session.Zoom - _zoomAnchor;
        _cameraCommitPending = true;
    }

    private void QueuePointerStatus()
    {
        if (_pointerStatusQueued || _navigationClosed) return;
        _pointerStatusQueued = true;
        RequestAnimationFrame(_ =>
        {
            _pointerStatusQueued = false;
            if (!_navigationClosed) RefreshStatus();
        });
    }
}
