using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private ScrollViewer? _canvasScroll;
    private IPointer? _panPointer;
    private Point _panStart;
    private Vector _panOffset;
    private Cursor? _panPreviousCursor;
    private bool _zoomAnimating;
    private bool _zoomFramePending;
    private bool _navigationClosed;
    private bool _pointerStatusQueued;
    private double _zoomFrom;
    private double _zoomTarget;
    private long _zoomStarted;
    private Point _zoomAnchor;
    private Point _zoomSourceAnchor;
    private DocumentSession? _zoomSession;

    private void InitializeCanvasNavigation(ScrollViewer host)
    {
        // Tunnel navigation before PixelCanvasView dispatches to drawing tools.
        host.AddHandler(PointerPressedEvent, (_, e) =>
        {
            var properties = e.GetCurrentPoint(host).Properties;
            if (properties.IsLeftButtonPressed && !properties.IsMiddleButtonPressed) { StopCanvasZoom(); return; }
            if (!properties.IsMiddleButtonPressed || IsScrollbarSource(e.Source)) return;
            StopCanvasZoom();
            if (_canvasPointerActive || _selectionStart is not null) CancelCanvasInteraction();
            _panStart = e.GetPosition(host);
            _panOffset = host.Offset;
            _panPreviousCursor = host.Cursor;
            _panPointer = e.Pointer;
            host.Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(host);
            host.Focus();
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
        host.AddHandler(PointerMovedEvent, (_, e) =>
        {
            if (_panPointer != e.Pointer) return;
            if (!e.GetCurrentPoint(host).Properties.IsMiddleButtonPressed) { EndCanvasPan(); return; }
            host.Offset = _panOffset - (e.GetPosition(host) - _panStart);
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
        host.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (_panPointer != e.Pointer) return;
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
            if (e.Key != Key.Escape || _panPointer is null) return;
            EndCanvasPan(); e.Handled = true;
        }, RoutingStrategies.Tunnel);
        host.SizeChanged += (_, _) =>
        {
            if (_comfortCanvasFrame is not null)
            {
                _comfortCanvasFrame.UseLayoutRounding = false;
                _comfortCanvasFrame.Margin = new Thickness(Math.Max(38, host.Bounds.Width * 0.5), Math.Max(38, host.Bounds.Height * 0.5));
            }
        };
        Deactivated += (_, _) => { EndCanvasPan(); StopCanvasZoom(); };
        Closed += (_, _) => { _navigationClosed = true; EndCanvasPan(); StopCanvasZoom(); };
    }

    private static bool IsScrollbarSource(object? source) => source is Control control &&
        (control is ScrollBar || control.GetVisualAncestors().OfType<ScrollBar>().Any());

    private void EndCanvasPan()
    {
        if (_panPointer is not { } pointer) return;
        _panPointer = null;
        if (_canvasScroll is { } host) host.Cursor = _panPreviousCursor;
        if (ReferenceEquals(pointer.Captured, _canvasScroll)) pointer.Capture(null);
    }

    private Point CanvasViewportCenter() => _canvasScroll is { } host
        ? new Point(host.Viewport.Width * 0.5, host.Viewport.Height * 0.5) : default;

    private void ApplyCanvasZoom(double zoom, Point anchor)
    {
        if (Current() is not { } session) return;
        zoom = Math.Clamp(zoom, DocumentSession.MinimumZoom, DocumentSession.MaximumZoom);
        if (_canvasScroll is not { } host || _canvas.Presentation is null)
        {
            session.SetZoom(zoom);
            return;
        }
        var origin = _canvas.TranslatePoint(default, host) ?? default;
        // Hold the source coordinate for the complete animation. Recomputing it
        // every frame accumulates layout/scroll rounding and drifts under the cursor.
        var canvasPoint = _zoomAnimating && ReferenceEquals(_zoomSession, session)
            ? _zoomSourceAnchor : (anchor - origin) / _canvas.Zoom;
        session.SetZoom(zoom);
        host.UpdateLayout();
        var newOrigin = _canvas.TranslatePoint(default, host) ?? default;
        host.Offset += newOrigin + canvasPoint * _canvas.Zoom - anchor;
    }

    private void CenterCanvasViewport()
    {
        if (_canvasScroll is not { } host) return;
        host.UpdateLayout();
        host.Offset = new Vector(Math.Max(0, (host.Extent.Width - host.Viewport.Width) * 0.5),
            Math.Max(0, (host.Extent.Height - host.Viewport.Height) * 0.5));
    }

    private void AnimateCanvasZoom(double factor, Point anchor)
    {
        if (!double.IsFinite(factor) || factor <= 0 || Current() is not { } session || _canvasScroll is not { } host) return;
        _zoomTarget = Math.Clamp((_zoomAnimating && ReferenceEquals(_zoomSession, session) ? _zoomTarget : session.Zoom) * factor,
            DocumentSession.MinimumZoom, DocumentSession.MaximumZoom);
        _zoomFrom = session.Zoom;
        _zoomAnchor = anchor;
        _zoomSourceAnchor = (anchor - (_canvas.TranslatePoint(default, host) ?? default)) / _canvas.Zoom;
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
            var progress = Math.Clamp(Stopwatch.GetElapsedTime(_zoomStarted).TotalMilliseconds / 110d, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 3);
            var zoom = Math.Exp(Math.Log(_zoomFrom) + (Math.Log(_zoomTarget) - Math.Log(_zoomFrom)) * eased);
            ApplyCanvasZoom(progress >= 1 ? _zoomTarget : zoom, _zoomAnchor);
            if (progress >= 1) _zoomAnimating = false;
            else RequestCanvasZoomFrame();
        });
    }

    private void StopCanvasZoom() { _zoomAnimating = false; _zoomSession = null; }

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
