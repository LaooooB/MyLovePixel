using Avalonia;
using Avalonia.Media;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RenderOptions.SetEdgeMode(_canvas, EdgeMode.Aliased);
        _canvas.SetGrid(_gridVisible);
    }

    private void ClearCanvas()
    {
        var session = Current();
        if (session is null || _busy) return;
        FinishParameterEdit();
        _canvas.CancelActivePointer();
        _playback.Stop(session);
        if (session.DrawingBlockedReason is { } blocked) { SetError(blocked); return; }
        Safe(() => session.ClearCurrentCanvas());
        _selection.Clear(session);
        RefreshAll(false);
    }

    private void SetToolOptionFromSlider(DocumentSession session, string id, int value)
    {
        if (!ReferenceEquals(Current(), session)) return;
        session.SetToolOption(id, value);
        UpdateCanvasCursor();
    }
}
