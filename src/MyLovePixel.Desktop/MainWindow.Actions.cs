using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MyLovePixel.Application;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using MyLovePixel.Export;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private bool _refreshQueued;
    private bool _refreshing;

    private async Task NewProjectAsync()
    {
        var choice = await new NewProjectDialog().ShowDialog<CanvasSizeChoice?>(this);
        if (choice is null) return;
        _workspace.NewDocument(choice.Width, choice.Height);
        _selectionMode = false;
    }

    private async Task ExportAsync()
    {
        var session = Current(); if (session is null) return;
        var preset = await new ExportDialog().ShowDialog<ExportPreset?>(this);
        if (preset is null) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Export", AllowMultiple = false });
        if (folders.Count == 0) return;
        Safe(() => _plugins.Export(session, preset, folders[0].Path.LocalPath));
    }

    private async Task ImportPngAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import PNG",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }],
        });
        if (files.Count == 0) return;
        Safe(() => _workspace.ImportPng(files[0].Path.LocalPath));
        _selectionMode = false;
        RefreshAll();
    }

    private async Task EditSelectedTileAsync()
    {
        var session = Current();
        if (session is null || _selectedTileset is not { } tilesetId || _selectedTile is not { } tileId) return;
        await new TilePixelDialog(session, tilesetId, tileId).ShowDialog(this);
        RefreshAll();
    }

    private async Task EditClipAsync(AnimationClipPresentation clip)
    {
        var session = Current(); if (session is null) return;
        var count = session.CaptureSnapshot().FrameOrder.Count;
        var value = await new AnimationRangeDialog(clip.Name, clip.Start, clip.End, count, clip.LoopMode).ShowDialog<AnimationRangeChoice?>(this);
        if (value is { } choice) session.UpdateAnimationClip(clip.Id, choice.Name, choice.Start, choice.End, choice.LoopMode);
    }

    private async Task EditTagAsync(AnimationTagPresentation tag)
    {
        var session = Current(); if (session is null) return;
        var count = session.CaptureSnapshot().FrameOrder.Count;
        var value = await new AnimationRangeDialog(tag.Name, tag.Start, tag.End, count, null).ShowDialog<AnimationRangeChoice?>(this);
        if (value is { } choice) session.UpdateAnimationTag(tag.Id, choice.Name, choice.Start, choice.End);
    }

    private async Task EditSliceAsync(SpriteSlice slice)
    {
        var session = Current(); if (session is null) return;
        var value = await new SpriteSliceDialog(slice).ShowDialog<SpriteSliceChoice?>(this);
        if (value is { } choice) session.UpdateSpriteSlice(slice.Id, choice.Name, choice.X, choice.Y, choice.Width, choice.Height, choice.PivotX, choice.PivotY, choice.NineSlice);
    }

    private async Task EditHitboxesAsync()
    {
        var session = Current(); if (session is null) return;
        var value = await new AnimationBoxesDialog("Hitboxes", session.GetCurrentHitboxes()).ShowDialog<IReadOnlyList<AnimationBoxPresentation>?>(this);
        if (value is not null) Safe(() => session.SetHitboxes(value));
        RefreshAnimation();
    }

    private async Task EditHurtboxesAsync()
    {
        var session = Current(); if (session is null) return;
        var value = await new AnimationBoxesDialog("Hurtboxes", session.GetCurrentHurtboxes()).ShowDialog<IReadOnlyList<AnimationBoxPresentation>?>(this);
        if (value is not null) Safe(() => session.SetHurtboxes(value));
        RefreshAnimation();
    }

    private async Task EditSocketsAsync()
    {
        var session = Current(); if (session is null) return;
        var value = await new AnimationSocketsDialog(session.GetCurrentSockets()).ShowDialog<IReadOnlyList<AnimationSocketPresentation>?>(this);
        if (value is not null) Safe(() => session.SetSockets(value));
        RefreshAnimation();
    }

    private async Task EditEventsAsync()
    {
        var session = Current(); if (session is null) return;
        var value = await new AnimationEventsDialog(session.GetCurrentAnimationEvents()).ShowDialog<IReadOnlyList<AnimationEventPresentation>?>(this);
        if (value is not null) Safe(() => session.SetAnimationEvents(value));
        RefreshAnimation();
    }

    private async Task EditColorCyclesAsync()
    {
        var session = Current(); if (session is null) return;
        var value = await new AnimationCyclesDialog(session.GetPaletteEditors(), session.GetCurrentColorCycles()).ShowDialog<IReadOnlyList<AnimationColorCyclePresentation>?>(this);
        if (value is not null) Safe(() => session.SetColorCycles(value));
        RefreshAnimation();
    }

    private async Task LoadPluginAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Plugin",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Plugin") { Patterns = ["*.dll"] }],
        });
        if (files.Count == 0) return;
        var result = _plugins.LoadAssembly(files[0].Path.LocalPath);
        if (!result.Succeeded) SetError(result.Error ?? "Plugin load failed");
        RefreshAll();
    }

    private void TogglePlayback()
    {
        var session = Current(); if (session is null) return;
        _playback.Toggle(session); _playbackTimestamp = Stopwatch.GetTimestamp();
    }

    private void OnPlaybackTick(object? sender, EventArgs e)
    {
        var session = Current(); if (session is null || !_playback.IsPlaying(session)) { _playbackTimestamp = Stopwatch.GetTimestamp(); return; }
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_playbackTimestamp, now); _playbackTimestamp = now;
        Safe(() => _playback.Advance(session, Math.Max(0, (long)(elapsed.TotalMilliseconds * 1000d))));
    }

    private void MoveSelection(int dx, int dy)
    {
        var session = Current(); if (session is null) return;
        Safe(() => _selection.Move(session, dx, dy)); RefreshCanvas();
    }

    private void TransformSelection(Action action) { Safe(action); RefreshCanvas(); }

    private void OnWorkspaceChanged(object? sender, EventArgs e)
    {
        ObserveCurrentSession();
        _timelineStart = 0;
        _selectionStart = null;
        QueueRefreshAll();
    }

    private void ObserveCurrentSession()
    {
        if (ReferenceEquals(_observedSession, Current())) return;
        if (_observedSession is not null) _observedSession.StateChanged -= OnSessionChanged;
        _observedSession = Current();
        if (_observedSession is not null) _observedSession.StateChanged += OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, EventArgs e) => QueueRefreshAll();

    private void QueueRefreshAll()
    {
        if (_refreshQueued) return;
        _refreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshQueued = false;
            RefreshAll();
        }, DispatcherPriority.Background);
    }

    private void RefreshActions()
    {
        foreach (var pair in _actionControls)
        {
            var enabled = _actions.CanExecute(pair.Key, _actionContext);
            foreach (var control in pair.Value) control.IsEnabled = enabled;
        }
    }

    private async Task InvokeActionAsync(ActionId id)
    {
        try { if (_actions.CanExecute(id, _actionContext)) await _actions.ExecuteAsync(id, _actionContext); }
        catch (Exception ex) { SetError(ex.Message); }
        RefreshAll();
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.N) { e.Handled = true; await NewProjectAsync(); return; }
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.E) { e.Handled = true; await ExportAsync(); return; }
        if (e.Key == Key.Escape)
        {
            if (Current() is { } session) { _selection.Clear(session); _plugins.CancelTool(session); }
            _selectionStart = null; e.Handled = true; RefreshCanvas(); return;
        }
        var modifiers = ShortcutModifiers.None;
        if ((e.KeyModifiers & KeyModifiers.Control) != 0) modifiers |= ShortcutModifiers.Control;
        if ((e.KeyModifiers & KeyModifiers.Shift) != 0) modifiers |= ShortcutModifiers.Shift;
        if ((e.KeyModifiers & KeyModifiers.Alt) != 0) modifiers |= ShortcutModifiers.Alt;
        if ((e.KeyModifiers & KeyModifiers.Meta) != 0) modifiers |= ShortcutModifiers.Meta;
        if (!_shortcuts.TryResolve(new ShortcutGesture(e.Key.ToString(), modifiers), out var id)) return;
        e.Handled = true; await InvokeActionAsync(id);
    }

    private void OnAutosaveTick(object? sender, EventArgs e)
    {
        var attempts = _recovery.Tick(DateTimeOffset.UtcNow);
        if (attempts.Any(v => !v.WroteCheckpoint)) SetError(attempts.First(v => !v.WroteCheckpoint).Error ?? "Autosave failed");
        RefreshRecovery();
    }

    private void RecoverCandidate(string id) { Safe(() => _recovery.Recover(id)); RefreshAll(); }
    private void DismissCandidate(string id) { Safe(() => _recovery.Dismiss(id)); RefreshRecovery(); }
    private void ChangeZoom(double factor) { if (Current() is { } s) s.SetZoom(s.Zoom * factor); }
    private void SetZoom(double zoom) => Current()?.SetZoom(zoom);
    private DocumentSession? Current() => _workspace.CurrentSession;
}
