using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MyLovePixel.Application;
using MyLovePixel.Export;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private bool _closeApproved;
    private bool _closingPrompt;
    private bool _autosaveRunning;
    private ExportPreset? _lastExportPreset;

    private async Task NewProjectAsync()
    {
        if (_busy) return;
        FinishParameterEdit();
        var choice = await new NewProjectDialog().ShowDialog<CanvasSizeChoice?>(this);
        if (choice is null) return;
        _workspace.NewDocument(choice.Width, choice.Height);
        _selectionMode = _eyedropperMode = false;
        RefreshAll();
        FitCanvas();
        _canvas.Focus();
    }

    private async Task ImportAssetAsync()
    {
        if (_busy) return;
        FinishParameterEdit();
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import image or sprite metadata",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Pixel assets") { Patterns = ["*.png", "*.json"] }],
        });
        if (files.Count == 0) return;
        var path = files[0].Path.LocalPath;
        var succeeded = await RunBusyAsync("Importing…", () =>
        {
            if (string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)) _workspace.ImportSpriteMetadata(path);
            else _workspace.ImportPng(path);
        });
        if (!succeeded) return;
        _selectionMode = _eyedropperMode = false;
        RefreshAll(); FitCanvas(); _canvas.Focus();
        ShowNotice("Imported " + Path.GetFileName(path));
    }

    private async Task ExportAsync()
    {
        if (_busy || Current() is not { } session) return;
        FinishParameterEdit();
        var preset = await new ExportDialog().ShowDialog<ExportPreset?>(this);
        if (preset is null) return;
        _lastExportPreset = preset;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Export folder", AllowMultiple = false });
        if (folders.Count == 0) return;
        var directory = folders[0].Path.LocalPath;
        var success = await RunBusyAsync("Exporting…", () => _plugins.Export(session, _lastExportPreset, directory));
        if (success) ShowNotice("Exported to " + directory);
        _canvas.Focus();
    }

    private async Task<bool> SaveDocumentAsync(DocumentSession session, bool saveAs = false)
    {
        FinishParameterEdit();
        var path = saveAs ? null : session.FilePath;
        if (path is null) path = await _interaction.PickSaveProjectAsync(session, CancellationToken.None);
        if (path is null) return false;
        var target = path;
        if (!await RunBusyAsync("Saving…", () => _workspace.Save(session, target))) return false;
        ShowNotice("Saved " + Path.GetFileName(target));
        return true;
    }

    private async Task InvokeActionAsync(ActionId id)
    {
        if (_busy) return;
        FinishParameterEdit();
        _canvas.CancelActivePointer();
        try
        {
            if (id == BuiltinActionIds.NewProject) { await NewProjectAsync(); return; }
            if (id == BuiltinActionIds.ExportProject) { await ExportAsync(); return; }
            if (id == BuiltinActionIds.SaveProject || id == BuiltinActionIds.SaveProjectAs)
            {
                if (Current() is { } session) await SaveDocumentAsync(session, id == BuiltinActionIds.SaveProjectAs);
                return;
            }
            if (id == BuiltinActionIds.OpenProject)
            {
                var path = await _interaction.PickOpenProjectAsync(CancellationToken.None);
                if (path is null) return;
                if (await RunBusyAsync("Opening…", () => _workspace.Open(path))) { RefreshAll(); FitCanvas(); }
                return;
            }
            if (Current() is { } current) _playback.Stop(current);
            if (_actions.CanExecute(id, _actionContext)) await _actions.ExecuteAsync(id, _actionContext);
        }
        catch (Exception ex) { CrashLog.Write("Action", ex); SetError(ex.Message); }
        finally { RefreshAll(false); }
    }

    private async Task<bool> RunBusyAsync(string message, Action operation)
    {
        if (_busy) return false;
        FinishParameterEdit();
        _canvas.CancelActivePointer();
        if (Current() is { } session) _playback.Stop(session);
        _busy = true;
        _busyProgress.IsVisible = true;
        if (_toolbar is not null) _toolbar.IsEnabled = false;
        if (_editorBody is not null) _editorBody.IsEnabled = false;
        _timelineExpander.IsEnabled = false;
        ShowNotice(message);
        try
        {
            await Task.Run(operation);
            return true;
        }
        catch (Exception ex) { CrashLog.Write("File operation", ex); SetError(ex.Message); return false; }
        finally
        {
            _busy = false;
            _busyProgress.IsVisible = false;
            if (_toolbar is not null) _toolbar.IsEnabled = true;
            if (_editorBody is not null) _editorBody.IsEnabled = true;
            _timelineExpander.IsEnabled = true;
            RefreshAll(false);
        }
    }

    private async Task<bool> ConfirmCloseDocumentAsync(DocumentSession session)
    {
        FinishParameterEdit();
        if (!session.IsDirty) return true;
        var name = session.FilePath is null ? "Untitled" : Path.GetFileName(session.FilePath);
        var decision = await new UnsavedChangesDialog(name).ShowDialog<CloseDocumentDecision>(this);
        return decision switch
        {
            CloseDocumentDecision.Discard => true,
            CloseDocumentDecision.Save => await SaveDocumentAsync(session),
            _ => false,
        };
    }

    private async Task CloseCurrentDocumentAsync()
    {
        if (_busy || _closingPrompt || Current() is not { } session) return;
        _closingPrompt = true;
        try
        {
            _canvas.CancelActivePointer();
            _playback.Stop(session);
            if (await ConfirmCloseDocumentAsync(session))
            {
                _workspace.Close(session);
                RefreshAll();
                _canvas.Focus();
            }
        }
        catch (Exception ex) { SetError(ex.Message); }
        finally { _closingPrompt = false; }
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeApproved) return;
        if (_busy) { e.Cancel = true; ShowNotice("Finish the current file operation before closing."); return; }
        FinishParameterEdit();
        _canvas.CancelActivePointer();
        if (!_workspace.Sessions.Any(s => s.IsDirty)) return;
        e.Cancel = true;
        if (_closingPrompt) return;
        _closingPrompt = true;
        try
        {
            foreach (var session in _workspace.Sessions.ToArray())
                if (!await ConfirmCloseDocumentAsync(session)) return;
            _closeApproved = true;
            Close();
        }
        catch (Exception ex) { SetError(ex.Message); }
        finally { _closingPrompt = false; }
    }

    private void OnAutosaveTick(object? sender, EventArgs e)
    {
        if (_closed || _busy || _autosaveRunning || _canvasPointerActive || _parameterEdit is not null) return;
        _autosaveRunning = true;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (_closed || _busy || _canvasPointerActive || _parameterEdit is not null) return;
                var attempts = _recovery.Tick(DateTimeOffset.UtcNow);
                var failure = attempts.FirstOrDefault(a => !a.WroteCheckpoint);
                if (failure is not null) SetError(failure.Error ?? "Could not save a recovery copy.");
                if (attempts.Count > 0) _panelStamps.Remove(_recoveryPanel);
            }
            catch (Exception ex) { SetError(ex.Message); }
            finally { _autosaveRunning = false; }
        }, DispatcherPriority.ApplicationIdle);
    }
}
