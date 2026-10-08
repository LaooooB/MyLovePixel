using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private Border? _navigationFrame;
    private long _navigationVersion;
    private IPointer? _workspacePanPointer;
    private Point _workspacePanPoint;

    private void ResizeNavigationSpace()
    {
        if (_navigationFrame is null || _canvasScroll.Bounds.Width < 1 || _canvasScroll.Bounds.Height < 1) return;
        var old = _navigationFrame.Padding;
        var next = new Thickness(Math.Max(80, _canvasScroll.Bounds.Width), Math.Max(80, _canvasScroll.Bounds.Height));
        if (old == next) return;
        var offset = _canvasScroll.Offset + new Vector(next.Left - old.Left, next.Top - old.Top);
        _navigationFrame.Padding = next;
        var version = ++_navigationVersion;
        Dispatcher.UIThread.Post(() => { if (!_closed && version == _navigationVersion) _canvasScroll.Offset = offset; }, DispatcherPriority.Background);
    }
    private void InstallWorkspacePanning()
    {
        _canvasScroll.PointerPressed += (_, e) =>
        {
            var p = e.GetCurrentPoint(_canvasScroll).Properties;
            if (_busy || !(p.IsMiddleButtonPressed || p.IsLeftButtonPressed && _spaceHeld)) return;
            _workspacePanPointer = e.Pointer; _workspacePanPoint = e.GetPosition(this); e.Pointer.Capture(_canvasScroll);
            _canvasScroll.Cursor = new Cursor(StandardCursorType.Hand); e.Handled = true;
        };
        _canvasScroll.PointerMoved += (_, e) =>
        {
            if (!ReferenceEquals(e.Pointer, _workspacePanPointer)) return;
            var point = e.GetPosition(this); _navigationVersion++;
            _canvasScroll.Offset -= point - _workspacePanPoint; _workspacePanPoint = point; e.Handled = true;
        };
        _canvasScroll.PointerReleased += (_, e) => { if (ReferenceEquals(e.Pointer, _workspacePanPointer)) { CancelWorkspacePanning(); e.Handled = true; } };
        _canvasScroll.PointerCaptureLost += (_, _) => CancelWorkspacePanning();
    }
    private void CancelWorkspacePanning()
    {
        var pointer = _workspacePanPointer; _workspacePanPointer = null;
        if (pointer is not null && ReferenceEquals(pointer.Captured, _canvasScroll)) pointer.Capture(null);
        _canvasScroll.Cursor = null;
    }
}
