using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyLovePixel.Application;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private bool _busy;
    private bool _closed;
    private bool _forceRefresh;
    private DocumentSession? _lastUiSession;
    private long _lastUiVersion = -1;
    private FrameId? _lastUiFrame;
    private LayerId? _lastUiLayer;
    private double _lastUiZoom;
    private string? _documentPickerSignature;
    private readonly Dictionary<Control, string> _panelStamps = [];
    private EditorEditGesture? _parameterEdit;
    private Control? _parameterControl;

    private void RefreshAll(bool force = true)
    {
        if (_closed || _busy) return;
        if (_refreshing) { QueueRefreshAll(); return; }
        _refreshing = true;
        try
        {
            ObserveCurrentSession();
            var session = Current();
            var changed = !ReferenceEquals(_lastUiSession, session) || _lastUiVersion != session?.DocumentVersion || _lastUiFrame != session?.CurrentFrameId || _lastUiLayer != session?.CurrentLayerId;
            if (force || changed || _lastUiZoom != session?.Zoom) RefreshCanvas();
            RefreshActions();
            RefreshTools();
            RefreshToolOptions();
            RefreshLayers();
            RefreshPalette();
            RefreshTimeline();
            var stamp = $"{session?.GetHashCode()}:{session?.DocumentVersion}:{session?.CurrentFrameId}:{session?.CurrentLayerId}";
            RefreshLazy(_effectsPanel, stamp + ':' + _selectedEffect, RefreshEffects, force);
            RefreshLazy(_tilesPanel, stamp + ':' + _selectedTilemap + ':' + _selectedTile, RefreshTiles, force);
            RefreshLazy(_animationPanel, stamp, RefreshAnimation, force);
            RefreshLazy(_pluginsPanel, stamp + ':' + _plugins.Plugins.Count + ':' + _plugins.Diagnostics.Count, RefreshPlugins, force);
            RefreshLazy(_recoveryPanel, "recovery", RefreshRecovery, false);
            RefreshDocumentPicker();
            RefreshPlaybackState();
            RefreshContext();
            RefreshStatus();
            _lastUiSession = session;
            _lastUiVersion = session?.DocumentVersion ?? -1;
            _lastUiFrame = session?.CurrentFrameId;
            _lastUiLayer = session?.CurrentLayerId;
            _lastUiZoom = session?.Zoom ?? 0;
        }
        catch (Exception ex) { CrashLog.Write("Refresh", ex); SetError(ex.Message); }
        finally { _refreshing = false; }
    }

    private void RefreshLazy(Control panel, string stamp, Action refresh, bool force)
    {
        if (!panel.IsEffectivelyVisible || panel.IsKeyboardFocusWithin || _parameterEdit is not null) return;
        if (!force && _panelStamps.TryGetValue(panel, out var previous) && previous == stamp) return;
        var scroll = panel.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        var offset = scroll?.Offset;
        var expanded = panel.GetVisualDescendants().OfType<Expander>().ToDictionary(e => e.Header?.ToString() ?? string.Empty, e => e.IsExpanded, StringComparer.Ordinal);
        refresh();
        _panelStamps[panel] = stamp;
        foreach (var expander in panel.GetVisualDescendants().OfType<Expander>())
            if (expanded.TryGetValue(expander.Header?.ToString() ?? string.Empty, out var isExpanded)) expander.IsExpanded = isExpanded;
        if (scroll is not null && offset is { } saved)
            Dispatcher.UIThread.Post(() => { if (!_closed) scroll.Offset = saved; }, DispatcherPriority.Background);
    }

    private void ObserveCurrentSession()
    {
        if (ReferenceEquals(_observedSession, Current())) return;
        if (_observedSession is not null) _observedSession.StateChanged -= OnSessionChanged;
        _observedSession = Current();
        if (_observedSession is not null) _observedSession.StateChanged += OnSessionChanged;
        _optionsSignature = null;
        _panelStamps.Clear();
        _sampleInfo.IsVisible = false;
    }

    private void OnWorkspaceChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(() => OnWorkspaceChanged(sender, e)); return; }
        ObserveCurrentSession();
        _selectionStart = null;
        QueueRefreshAll();
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(() => OnSessionChanged(sender, e)); return; }
        if (_canvasPointerActive || _parameterEdit is not null) QueueCanvasRefresh();
        else QueueRefreshAll();
    }

    private void QueueCanvasRefresh()
    {
        if (_canvasRefreshQueued || _closed) return;
        _canvasRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _canvasRefreshQueued = false;
            if (_closed || _busy) return;
            try { RefreshCanvas(updatePreview: !_canvasPointerActive); RefreshStatus(); }
            catch (Exception ex) { SetError(ex.Message); }
        }, DispatcherPriority.Background);
    }

    private void QueueRefreshAll()
    {
        if (_refreshQueued || _closed) return;
        _refreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshQueued = false;
            var force = _forceRefresh;
            _forceRefresh = false;
            RefreshAll(force);
        }, DispatcherPriority.Background);
    }

    private void RefreshDocumentPicker()
    {
        var sessions = _workspace.Sessions;
        var signature = string.Join('|', sessions.Select(s => $"{s.GetHashCode()}:{s.FilePath}:{s.IsDirty}:{s.IsRecovered}"));
        if (signature == _documentPickerSignature && _documentSelector.SelectedItem is DocumentChoice current && ReferenceEquals(current.Session, Current())) return;
        _documentPickerSignature = signature;
        _syncDocuments = true;
        try
        {
            var choices = sessions.Select((s, i) => new DocumentChoice(s, (s.FilePath is null ? $"Untitled {i + 1}" : Path.GetFileName(s.FilePath)) + (s.IsRecovered ? " — recovered" : string.Empty) + (s.IsDirty ? " *" : string.Empty))).ToArray();
            _documentSelector.ItemsSource = choices;
            _documentSelector.SelectedItem = choices.FirstOrDefault(c => ReferenceEquals(c.Session, Current()));
        }
        finally { _syncDocuments = false; }
    }

    private void RefreshContext()
    {
        if (Current() is not { } session) { _contextName.Text = string.Empty; return; }
        var layer = session.GetLayers().FirstOrDefault(l => l.IsCurrent);
        var frame = session.CaptureSnapshot().FrameOrder.ToList().IndexOf(session.CurrentFrameId) + 1;
        var tool = _eyedropperMode ? "Eyedropper" : _selectionMode ? "Selection" : _plugins.GetTools(session).FirstOrDefault(t => t.IsActive)?.DisplayName;
        _contextName.Text = $"{tool} · {layer?.Name} · Frame {frame}" + (layer?.Locked == true ? " · Locked" : layer?.Visible == false ? " · Hidden" : string.Empty);
    }

    private void RefreshStatus()
    {
        if (_busy) return;
        _status.Foreground = EditorThemeTokens.TextSecondary;
        if (Current() is not { } session)
        {
            Title = "MyLovePixel";
            _status.Text = "No open document";
            return;
        }
        var name = session.FilePath is null ? "Untitled" : Path.GetFileName(session.FilePath);
        Title = $"MyLovePixel — {name}{(session.IsDirty ? " *" : string.Empty)}";
        var size = _canvas.Presentation?.Size;
        var position = _hover is { } point ? $"{point.X}, {point.Y}" : "—";
        _status.Text = $"Pixel {position}   ·   {size?.Width}×{size?.Height}   ·   {session.Zoom * 100:0.#}%   ·   {(session.IsDirty ? "Unsaved" : session.FilePath is null ? "New document" : "Saved")}" + (session.IsRecovered ? "   ·   Recovery copy" : string.Empty);
    }

    private void ShowNotice(string text, bool error = false)
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(() => ShowNotice(text, error)); return; }
        _notice.Text = text;
        _notice.Foreground = error ? EditorThemeTokens.Danger : EditorThemeTokens.TextPrimary;
        _noticeHost.IsVisible = true;
    }

    private void UpdateParameter(DocumentSession session, Control control, string name, Action apply)
    {
        if (_busy) return;
        if (!ReferenceEquals(control, _parameterControl))
        {
            FinishParameterEdit();
            _parameterEdit = session.BeginUserEdit(name);
            _parameterControl = control;
        }
        try { apply(); }
        catch (Exception ex) { FinishParameterEdit(false); SetError(ex.Message); }
    }

    private void WireParameterCompletion(Control control)
    {
        control.LostFocus += (_, _) => { if (!control.IsKeyboardFocusWithin && ReferenceEquals(control, _parameterControl)) FinishParameterEdit(); };
        control.KeyDown += (_, e) =>
        {
            if (!ReferenceEquals(control, _parameterControl)) return;
            if (e.Key == Key.Enter) { FinishParameterEdit(); e.Handled = true; }
            else if (e.Key == Key.Escape) { FinishParameterEdit(false); e.Handled = true; }
        };
        control.DetachedFromVisualTree += (_, _) => { if (ReferenceEquals(control, _parameterControl)) FinishParameterEdit(); };
    }

    private void FinishParameterEdit(bool commit = true)
    {
        var edit = _parameterEdit;
        if (edit is null) return;
        _parameterEdit = null;
        _parameterControl = null;
        edit.Finish(commit);
        if (!commit) _canvas.Focus();
        _forceRefresh = true;
        QueueRefreshAll();
    }
}
