using MyLovePixel.Application;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private EditorEditGesture? _secondaryEraseGesture;
    private DocumentSession? _secondaryEraseSession;
    private LayerId _secondaryEraseLayer;
    private FrameId _secondaryEraseFrame;
    private IntPoint? _lastSecondaryErasePixel;

    private void HandleSecondaryErase(DocumentSession session, EditorPointerEvent e)
    {
        if (e.Kind == EditorPointerKind.Pressed)
        {
            FinishParameterEdit();
            _playback.Stop(session);
            _plugins.CancelTool(session);
            if (session.DrawingBlockedReason is { } blocked) throw new InvalidOperationException(blocked);
            _secondaryEraseSession = session;
            _secondaryEraseLayer = session.CurrentLayerId;
            _secondaryEraseFrame = session.CurrentFrameId;
            _secondaryEraseGesture = session.BeginUserEdit("Erase Pixels");
            _lastSecondaryErasePixel = null;
            _canvasPointerActive = true;
        }
        if (_secondaryEraseGesture is null) return;
        if (!ReferenceEquals(_secondaryEraseSession, session) || _secondaryEraseLayer != session.CurrentLayerId || _secondaryEraseFrame != session.CurrentFrameId)
        {
            FinishSecondaryErase(false);
            return;
        }
        if (e.Kind is EditorPointerKind.Pressed or EditorPointerKind.Moved or EditorPointerKind.Released)
        {
            var size = session.CaptureSnapshot().Canvas.Size;
            if ((uint)e.CanvasPixel.X >= (uint)size.Width || (uint)e.CanvasPixel.Y >= (uint)size.Height)
                _lastSecondaryErasePixel = null;
            else if (_lastSecondaryErasePixel != e.CanvasPixel)
            {
                session.EraseCanvasSegment(_lastSecondaryErasePixel ?? e.CanvasPixel, e.CanvasPixel);
                _lastSecondaryErasePixel = e.CanvasPixel;
                QueueCanvasRefresh();
            }
        }
        if (e.Kind == EditorPointerKind.Released) FinishSecondaryErase(true);
    }

    private void FinishSecondaryErase(bool commit)
    {
        var gesture = _secondaryEraseGesture;
        if (gesture is null) return;
        _secondaryEraseGesture = null;
        _secondaryEraseSession = null;
        _lastSecondaryErasePixel = null;
        try { gesture.Finish(commit); }
        finally { _canvasPointerActive = false; QueueRefreshAll(); }
    }
}
