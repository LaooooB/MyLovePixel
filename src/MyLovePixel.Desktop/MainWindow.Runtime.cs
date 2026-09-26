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
    private void RefreshPlugins()
    {
        _pluginsPanel.Children.Clear();
        _pluginsPanel.Children.Add(TextIconButton("＋", "Load Plugin…", "Load plugin DLL", LoadPluginAsync));
        foreach (var plugin in _plugins.Plugins)
        {
            _pluginsPanel.Children.Add(ListRow(plugin.Name, IconButton("×", $"Unload {plugin.Name}", () =>
            {
                _plugins.Unload(plugin.Id);
                RefreshAll();
            })));
        }

        _pluginsPanel.Children.Add(BuildPluginExtensionControls(Current()));
        _pluginPanelView.SetPanels(_plugins.GetPanels(Current()), (panel, action) =>
        {
            var session = Current();
            if (session is null) return new PluginPanelActionResult(false, false, "No document");
            var result = _plugins.InvokePanelAction(session, panel, action);
            RefreshAll();
            return result;
        });
        _pluginsPanel.Children.Add(_pluginPanelView);

        if (_plugins.Diagnostics.Count > 0) AddPanelLabel(_pluginsPanel, "Plugin diagnostics");
        foreach (var d in _plugins.Diagnostics.TakeLast(6))
        {
            var t = new TextBlock { Text = d, TextWrapping = TextWrapping.Wrap };
            t.Classes.Add("subtle");
            _pluginsPanel.Children.Add(t);
        }
    }

    private void RefreshRecovery()
    {
        _recoveryPanel.Children.Clear();
        IReadOnlyList<RecoveryCandidatePresentation> candidates;
        try { candidates = _recovery.Discover(); }
        catch (Exception ex) { _recoveryPanel.Children.Add(ErrorText(ex.Message)); return; }

        if (candidates.Count == 0)
        {
            var empty = new TextBlock { Text = "No recovery snapshots are available." };
            empty.Classes.Add("subtle");
            _recoveryPanel.Children.Add(empty);
            return;
        }

        foreach (var candidate in candidates.Take(8))
        {
            var name = candidate.SourcePath is null ? "Untitled" : Path.GetFileName(candidate.SourcePath);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 6 };
            row.Children.Add(new TextBlock
            {
                Text = name,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            });
            if (candidate.IsRecoverable)
                row.Children.Add(Place(TextIconButton("↺", "Recover", $"Recover {name}", () => RecoverCandidate(candidate.RecoveryId)), 1));
            row.Children.Add(Place(SmallIcon("×", $"Dismiss recovery snapshot for {name}", () => DismissCandidate(candidate.RecoveryId)), 2));
            _recoveryPanel.Children.Add(row);
        }
    }



    private void DispatchCanvasPointer(EditorPointerEvent e)
    {
        var session = Current();
        if (session is null) return;
        try
        {
            if (!_selectionMode && e.Kind == EditorPointerKind.Pressed)
                _canvasPointerActive = true;

            if (_busy) return;
            if (e.Kind == EditorPointerKind.Pressed)
            {
                FinishParameterEdit();
                _playback.Stop(session);
                if (!_selectionMode)
                {
                    if (session.DrawingBlockedReason is { } blocked) throw new InvalidOperationException(blocked);
                    session.EnsureEditableCel();
                }
            }

            if (_selectionMode)
            {
                if (_selectionGesture == SelectionGestureMode.ByColor && e.Kind == EditorPointerKind.Pressed && (e.Buttons & EditorPointerButtons.Primary) != 0)
                {
                    _selection.SelectByColor(session, e.CanvasPixel.X, e.CanvasPixel.Y);
                    RefreshCanvas(updatePreview: false);
                    return;
                }
                if (e.Kind == EditorPointerKind.Pressed && (e.Buttons & EditorPointerButtons.Primary) != 0)
                {
                    _selectionStart = (e.CanvasPixel.X, e.CanvasPixel.Y);
                    _selectionVertices.Clear();
                    _selectionVertices.Add(e.CanvasPixel);
                }
                if (_selectionStart is { } start && e.Kind is EditorPointerKind.Pressed or EditorPointerKind.Moved or EditorPointerKind.Released)
                {
                    if (_selectionGesture == SelectionGestureMode.Lasso)
                    {
                        if (_selectionVertices.Count == 0 || _selectionVertices[^1] != e.CanvasPixel) _selectionVertices.Add(e.CanvasPixel);
                        if (e.Kind == EditorPointerKind.Released && _selectionVertices.Count >= 3) _selection.SelectLasso(session, _selectionVertices);
                    }
                    else if (_selectionGesture == SelectionGestureMode.Ellipse)
                    {
                        _selection.SelectEllipse(session, start.X, start.Y, e.CanvasPixel.X, e.CanvasPixel.Y);
                    }
                    else
                    {
                        _selection.SelectRectangle(session, start.X, start.Y, e.CanvasPixel.X, e.CanvasPixel.Y);
                    }
                    RefreshCanvas(updatePreview: false);
                    if (e.Kind == EditorPointerKind.Released)
                    {
                        _selectionStart = null;
                        _selectionVertices.Clear();
                    }
                }
                return;
            }

            _plugins.DispatchPointer(session, e);
            if (e.Kind == EditorPointerKind.Released)
            {
                _canvasPointerActive = false;
                QueueRefreshAll();
            }
            else
            {
                QueueCanvasRefresh();
            }
        }
        catch (Exception ex)
        {
            _canvasPointerActive = false;
            CrashLog.Write("CanvasPointer", ex);
            try { _plugins.CancelTool(session); }
            catch (Exception cancelEx) { CrashLog.Write("CanvasPointerCancel", cancelEx); }
            SetError(ex.Message);
        }
    }

    private void CancelCanvasInteraction()
    {
        _canvasPointerActive = false;
        if (Current() is { } session)
        {
            try { _plugins.CancelTool(session); }
            catch (Exception ex)
            {
                CrashLog.Write("CanvasPointerCaptureLost", ex);
                SetError(ex.Message);
            }
        }
        _selectionStart = null;
        _selectionVertices.Clear();
        QueueRefreshAll();
    }

    private void ErasePixelFromCanvas(int x, int y)
    {
        var session = Current();
        if (session is null) return;
        if (_busy) return;
        FinishParameterEdit();
        _playback.Stop(session);
        Safe(() => session.EraseCanvasPixel(x, y));
        QueueRefreshAll();
    }

    private async Task EditColorAsync(bool primary)
    {
        var session = Current();
        if (session is null) return;
        var colors = session.GetToolColors();
        var initial = primary ? colors.Primary : colors.Secondary;
        var value = await new ColorDialog(initial).ShowDialog<Rgba32?>(this);
        if (value is not { } color) return;
        session.SetToolColors(primary ? color : colors.Primary, primary ? colors.Secondary : color);
    }

    private void SwapColors()
    {
        var session = Current();
        if (session is null) return;
        var c = session.GetToolColors();
        session.SetToolColors(c.Secondary, c.Primary);
    }
}
