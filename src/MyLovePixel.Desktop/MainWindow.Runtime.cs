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

    private void RefreshTimeline()
    {
        _timelineFrames.Children.Clear();
        var session = Current();
        if (session is null)
        {
            _timelineStatus.Text = string.Empty;
            return;
        }

        var total = session.CaptureSnapshot().FrameOrder.Count;
        _timelineStart = Math.Clamp(_timelineStart, 0, Math.Max(0, total - 1));
        var window = session.GetTimelineWindow(_timelineStart, TimelinePageSize);
        _timelineStatus.Text = $"Frames {window.StartIndex + 1}–{Math.Min(window.TotalCount, window.StartIndex + window.Items.Count)} of {window.TotalCount}";
        foreach (var frame in window.Items)
        {
            var ms = frame.DurationTicks / 1000d;
            var b = new Button { Content = $"{frame.Index + 1}\n{ms:0}ms", MinWidth = 58, Padding = new Thickness(5, 3) };
            ToolTip.SetTip(b, $"Frame {frame.Index + 1} · {ms:0} ms");
            if (frame.IsCurrent) b.Classes.Add("selected");
            b.Click += (_, _) =>
            {
                _playback.Stop(session);
                session.SelectFrame(frame.Id);
            };
            _timelineFrames.Children.Add(b);
        }

        var current = window.Items.FirstOrDefault(v => v.IsCurrent);
        if (current is not null)
        {
            var duration = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
            var label = new TextBlock { Text = "Duration", VerticalAlignment = VerticalAlignment.Center };
            label.Classes.Add("muted");
            duration.Children.Add(label);
            var ms = new NumericUpDown { Value = current.DurationTicks / 1000m, Minimum = 1, Maximum = 60_000, Increment = 1, FormatString = "0", Width = 82 };
            ToolTip.SetTip(ms, "Current frame duration in milliseconds");
            ms.ValueChanged += (_, _) =>
            {
                if (ms.Value is { } v) Safe(() => session.SetCurrentFrameDuration((long)(v * 1000m)));
            };
            duration.Children.Add(ms);
            duration.Children.Add(new TextBlock { Text = "ms", VerticalAlignment = VerticalAlignment.Center });
            _timelineFrames.Children.Add(duration);
        }
    }

    private void RefreshStatus()
    {
        _status.Foreground = EditorThemeTokens.TextSecondary;
        var session = Current();
        if (session is null)
        {
            Title = "MyLovePixel";
            _status.Text = string.Empty;
            return;
        }

        var s = session.CaptureSnapshot();
        var name = session.FilePath is null ? "Untitled" : Path.GetFileName(session.FilePath);
        Title = $"MyLovePixel — {name}{(session.IsDirty ? " *" : "")}";
        var pos = _hover is { } h ? $"Pixel {h.X}, {h.Y}" : "Pixel —";
        _status.Text = $"{pos}   ·   Canvas {s.Canvas.Size.Width}×{s.Canvas.Size.Height}   ·   Zoom {session.Zoom * 100:0}%{(session.IsDirty ? "   ·   Unsaved changes" : string.Empty)}";
    }

    private void DispatchCanvasPointer(EditorPointerEvent e)
    {
        var session = Current();
        if (session is null) return;
        try
        {
            if (e.Kind == EditorPointerKind.Pressed)
                session.EnsureEditableCel();

            if (_selectionMode)
            {
                if (_selectionGesture == SelectionGestureMode.ByColor && e.Kind == EditorPointerKind.Pressed && (e.Buttons & EditorPointerButtons.Primary) != 0)
                {
                    _selection.SelectByColor(session, e.CanvasPixel.X, e.CanvasPixel.Y);
                    RefreshCanvas();
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
                    RefreshCanvas();
                    if (e.Kind == EditorPointerKind.Released)
                    {
                        _selectionStart = null;
                        _selectionVertices.Clear();
                    }
                }
                return;
            }

            _plugins.DispatchPointer(session, e);
            QueueRefreshAll();
        }
        catch (Exception ex)
        {
            CrashLog.Write("CanvasPointer", ex);
            try { _plugins.CancelTool(session); }
            catch (Exception cancelEx) { CrashLog.Write("CanvasPointerCancel", cancelEx); }
            SetError(ex.Message);
        }
    }

    private void CancelCanvasInteraction()
    {
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

    private void PickColorFromCanvas(int x, int y)
    {
        var session = Current();
        if (session is null) return;
        Safe(() =>
        {
            var color = session.GetCanvasPixel(x, y);
            var current = session.GetToolColors();
            session.SetToolColors(color, current.Secondary);
        });
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
