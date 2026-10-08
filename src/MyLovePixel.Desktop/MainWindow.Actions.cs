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
    private bool _canvasRefreshQueued;
    private bool _canvasPointerActive;
    private bool _refreshing;




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
        if (_busy || _closed) return;
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






    private void RefreshActions()
    {
        var hasDocument = Current() is not null;
        foreach (var control in _documentControls) control.IsEnabled = hasDocument;
        _documentSelector.IsEnabled = hasDocument;
        foreach (var panel in new Control[] { _toolOptionsPanel, _layersPanel, _palettePanel, _tilesPanel, _animationPanel, _studioPaletteSwatches })
            panel.IsEnabled = hasDocument;
        _effectsPanel.IsEnabled = hasDocument && !Current()!.GetLayers().Any(l => l.IsCurrent && l.Locked);
        if (_timelineContent is not null) _timelineContent.IsEnabled = hasDocument;
        foreach (var pair in _actionControls)
        {
            var enabled = _actions.CanExecute(pair.Key, _actionContext);
            foreach (var control in pair.Value) control.IsEnabled = enabled;
        }
    }




    private void RecoverCandidate(string id) { Safe(() => _recovery.Recover(id)); RefreshAll(); }
    private void DismissCandidate(string id) { Safe(() => _recovery.Dismiss(id)); RefreshRecovery(); }
    private DocumentSession? Current() => _workspace.CurrentSession;
}
