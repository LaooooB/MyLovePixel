using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed class MainWindow : Window
{
    private const int TimelinePageSize = 24;

    private readonly EditorWorkspace _workspace = new();
    private readonly ActionRegistry _actions = ActionRegistry.CreateDefault();
    private readonly ShortcutMap _shortcuts = ShortcutMap.CreateDefault();
    private readonly AvaloniaEditorInteraction _interaction;
    private readonly EditorActionContext _actionContext;
    private readonly RecoveryWorkspaceCoordinator _recovery;
    private readonly DispatcherTimer _autosaveTimer;
    private readonly Dictionary<ActionId, List<Control>> _actionControls = [];
    private readonly PixelCanvasView _canvas = new();
    private readonly StackPanel _toolsPanel = new() { Spacing = EditorThemeTokens.CompactSpacing };
    private readonly StackPanel _layersPanel = new() { Spacing = EditorThemeTokens.CompactSpacing };
    private readonly StackPanel _toolOptionsPanel = new() { Spacing = 6 };
    private readonly StackPanel _palettePanel = new() { Spacing = EditorThemeTokens.CompactSpacing };
    private readonly StackPanel _recoveryPanel = new() { Spacing = EditorThemeTokens.CompactSpacing };
    private readonly StackPanel _timelineFrames = new() { Orientation = Orientation.Horizontal, Spacing = EditorThemeTokens.CompactSpacing };
    private readonly TextBlock _toolStatus = new();
    private readonly TextBlock _documentStatus = new();
    private readonly TextBlock _timelineStatus = new();
    private readonly TextBlock _performanceStatus = new() { TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _dirtyRegionsToggle = new() { Content = "Dirty regions" };
    private DocumentSession? _observedSession;
    private int _timelineStart;

    public MainWindow()
    {
        Width = 1280;
        Height = 800;
        MinWidth = 900;
        MinHeight = 600;
        Title = "MyLovePixel";

        _interaction = new AvaloniaEditorInteraction(this);
        _actionContext = new EditorActionContext(_workspace, _interaction);
        _recovery = new RecoveryWorkspaceCoordinator(
            _workspace,
            GetRecoveryRootDirectory(),
            AutosavePolicy.Default);
        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _autosaveTimer.Tick += OnAutosaveTick;
        _dirtyRegionsToggle.Click += (_, _) =>
            _workspace.CurrentSession?.SetDirtyRegionVisualization(_dirtyRegionsToggle.IsChecked == true);

        _canvas.PointerInput = DispatchCanvasPointer;
        _canvas.CancelPointerInput = () => _workspace.CurrentSession?.CancelToolInteraction();
        Content = BuildShell();

        _workspace.Changed += OnWorkspaceChanged;
        KeyDown += OnKeyDown;
        Closed += (_, _) => _autosaveTimer.Stop();
        _workspace.NewDocument(64, 64);
        _autosaveTimer.Start();
        RefreshRecovery();
        RefreshAll();
    }

    private Control BuildShell()
    {
        var root = new DockPanel();

        var menu = BuildMenu();
        DockPanel.SetDock(menu, Dock.Top);
        root.Children.Add(menu);

        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        var status = new Border
        {
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(1, 1, 0, 0),
            Padding = new Thickness(8, 4),
            Child = _documentStatus,
        };
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);

        var timeline = BuildTimeline();
        DockPanel.SetDock(timeline, Dock.Bottom);
        root.Children.Add(timeline);

        root.Children.Add(BuildWorkspace());
        return root;
    }

    private Menu BuildMenu()
    {
        var file = new MenuItem
        {
            Header = "_File",
            ItemsSource = new object[]
            {
                CreateActionMenuItem(BuiltinActionIds.NewProject),
                CreateActionMenuItem(BuiltinActionIds.OpenProject),
                new Separator(),
                CreateActionMenuItem(BuiltinActionIds.SaveProject),
                CreateActionMenuItem(BuiltinActionIds.SaveProjectAs),
                new Separator(),
                CreateActionMenuItem(BuiltinActionIds.ExportProject),
            },
        };
        var edit = new MenuItem
        {
            Header = "_Edit",
            ItemsSource = new object[]
            {
                CreateActionMenuItem(BuiltinActionIds.Undo),
                CreateActionMenuItem(BuiltinActionIds.Redo),
            },
        };
        return new Menu { ItemsSource = new object[] { file, edit } };
    }

    private Control BuildToolbar()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8, 6),
        };
        foreach (var id in new[]
                 {
                     BuiltinActionIds.NewProject,
                     BuiltinActionIds.OpenProject,
                     BuiltinActionIds.SaveProject,
                     BuiltinActionIds.ExportProject,
                     BuiltinActionIds.Undo,
                     BuiltinActionIds.Redo,
                 })
            panel.Children.Add(CreateActionButton(id));
        return new Border
        {
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = panel,
        };
    }

    private Control BuildWorkspace()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(EditorThemeTokens.LeftPanelWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(EditorThemeTokens.RightPanelWidth) });

        var left = BuildLeftPanel();
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        var canvasHost = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border
            {
                Background = EditorThemeTokens.CanvasBackground,
                Padding = new Thickness(24),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = _canvas,
            },
        };
        Grid.SetColumn(canvasHost, 1);
        grid.Children.Add(canvasHost);

        var right = BuildRightPanel();
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        return grid;
    }

    private Control BuildLeftPanel()
    {
        var content = new StackPanel { Spacing = EditorThemeTokens.PanelSpacing, Margin = new Thickness(8) };
        content.Children.Add(SectionTitle("Tools"));
        content.Children.Add(_toolStatus);
        content.Children.Add(_toolsPanel);
        content.Children.Add(SectionTitle("Layers"));
        content.Children.Add(_layersPanel);
        return PanelBorder(content, new Thickness(0, 0, 1, 0));
    }

    private Control BuildRightPanel()
    {
        var content = new StackPanel { Spacing = EditorThemeTokens.PanelSpacing, Margin = new Thickness(8) };
        content.Children.Add(SectionTitle("Tool Options"));
        content.Children.Add(_toolOptionsPanel);
        content.Children.Add(SectionTitle("Palette"));
        content.Children.Add(_palettePanel);
        content.Children.Add(SectionTitle("Recovery"));
        content.Children.Add(_recoveryPanel);
        content.Children.Add(SectionTitle("View"));

        var zoomRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = EditorThemeTokens.CompactSpacing };
        zoomRow.Children.Add(CreateViewButton("−", () => ChangeZoom(0.5)));
        zoomRow.Children.Add(CreateViewButton("+", () => ChangeZoom(2.0)));
        zoomRow.Children.Add(CreateViewButton("1:1", () => SetZoom(1d)));
        content.Children.Add(zoomRow);
        content.Children.Add(_dirtyRegionsToggle);
        content.Children.Add(SectionTitle("Diagnostics"));
        content.Children.Add(_performanceStatus);
        return PanelBorder(content, new Thickness(1, 0, 0, 0));
    }

    private Control BuildTimeline()
    {
        var outer = new DockPanel { LastChildFill = true };
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8, 6),
        };
        header.Children.Add(SectionTitle("Timeline"));
        header.Children.Add(CreateViewButton("◀", PreviousTimelinePage));
        header.Children.Add(CreateViewButton("▶", NextTimelinePage));
        header.Children.Add(_timelineStatus);
        DockPanel.SetDock(header, Dock.Top);
        outer.Children.Add(header);
        outer.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border
            {
                Padding = new Thickness(8, 2, 8, 8),
                Child = _timelineFrames,
            },
        });
        return new Border
        {
            Height = EditorThemeTokens.TimelineHeight,
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = outer,
        };
    }

    private MenuItem CreateActionMenuItem(ActionId id)
    {
        var descriptor = _actions.Get(id);
        var item = new MenuItem { Header = descriptor.DisplayName };
        item.Click += async (_, _) => await InvokeActionAsync(id);
        RegisterActionControl(id, item);
        return item;
    }

    private Button CreateActionButton(ActionId id)
    {
        var descriptor = _actions.Get(id);
        var button = new Button { Content = descriptor.DisplayName, Padding = new Thickness(10, 4) };
        button.Click += async (_, _) => await InvokeActionAsync(id);
        RegisterActionControl(id, button);
        return button;
    }

    private static Button CreateViewButton(string label, Action action)
    {
        var button = new Button { Content = label, Padding = new Thickness(8, 3) };
        button.Click += (_, _) => action();
        return button;
    }

    private void RegisterActionControl(ActionId id, Control control)
    {
        if (!_actionControls.TryGetValue(id, out var controls))
        {
            controls = [];
            _actionControls.Add(id, controls);
        }
        controls.Add(control);
    }

    private async Task InvokeActionAsync(ActionId id)
    {
        try
        {
            if (!_actions.CanExecute(id, _actionContext)) return;
            await _actions.ExecuteAsync(id, _actionContext);
            _documentStatus.Text = $"{_actions.Get(id).DisplayName} completed.";
        }
        catch (Exception ex)
        {
            _documentStatus.Text = ex.Message;
        }
        RefreshAll();
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var modifiers = ShortcutModifiers.None;
        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Control) != 0) modifiers |= ShortcutModifiers.Control;
        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Shift) != 0) modifiers |= ShortcutModifiers.Shift;
        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Alt) != 0) modifiers |= ShortcutModifiers.Alt;
        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Meta) != 0) modifiers |= ShortcutModifiers.Meta;
        if (!_shortcuts.TryResolve(new ShortcutGesture(e.Key.ToString(), modifiers), out var id)) return;
        e.Handled = true;
        await InvokeActionAsync(id);
    }

    private void DispatchCanvasPointer(EditorPointerEvent pointerEvent)
    {
        var session = _workspace.CurrentSession;
        if (session is null) return;
        try
        {
            session.DispatchPointer(pointerEvent);
        }
        catch (Exception ex)
        {
            session.CancelToolInteraction();
            _documentStatus.Text = ex.Message;
        }
    }

    private void OnWorkspaceChanged(object? sender, EventArgs e)
    {
        ObserveCurrentSession();
        _timelineStart = 0;
        RefreshAll();
    }

    private void ObserveCurrentSession()
    {
        if (ReferenceEquals(_observedSession, _workspace.CurrentSession)) return;
        if (_observedSession is not null) _observedSession.StateChanged -= OnSessionStateChanged;
        _observedSession = _workspace.CurrentSession;
        if (_observedSession is not null) _observedSession.StateChanged += OnSessionStateChanged;
    }

    private void OnSessionStateChanged(object? sender, EventArgs e) => RefreshAll();

    private void RefreshAll()
    {
        ObserveCurrentSession();
        RefreshActions();
        RefreshTitleAndStatus();
        RefreshCanvas();
        RefreshTools();
        RefreshToolOptions();
        RefreshLayers();
        RefreshPalette();
        RefreshTimeline();
    }

    private void RefreshActions()
    {
        foreach (var pair in _actionControls)
        {
            var enabled = _actions.CanExecute(pair.Key, _actionContext);
            foreach (var control in pair.Value) control.IsEnabled = enabled;
        }
    }

    private void RefreshTitleAndStatus()
    {
        var session = _workspace.CurrentSession;
        if (session is null)
        {
            Title = "MyLovePixel";
            _documentStatus.Text = "No document";
            _toolStatus.Text = "Active: none";
            _dirtyRegionsToggle.IsEnabled = false;
            return;
        }

        var name = session.IsRecovered
            ? $"Recovered{(session.RecoverySourcePath is null ? string.Empty : " — " + Path.GetFileName(session.RecoverySourcePath))}"
            : session.FilePath is null ? "Untitled" : Path.GetFileName(session.FilePath);
        Title = $"MyLovePixel — {name}{(session.IsDirty ? " *" : string.Empty)}";
        _documentStatus.Text = $"Frame {session.CurrentFrameId} · Layer {session.CurrentLayerId} · Zoom {session.Zoom:0.###}×{(session.IsRecovered ? " · recovered copy" : string.Empty)}";
        _toolStatus.Text = $"Active: {session.ActiveToolId}{(session.HasEditableCel ? string.Empty : " · no Cel")}";
        _dirtyRegionsToggle.IsEnabled = true;
        _dirtyRegionsToggle.IsChecked = session.ShowDirtyRegions;
    }

    private void RefreshCanvas()
    {
        var session = _workspace.CurrentSession;
        var presentation = session?.RenderCanvas();
        _canvas.SetPresentation(presentation, session?.Zoom ?? 1d);

        if (session is null || presentation?.Diagnostics is not { } diagnostics)
        {
            _performanceStatus.Text = "No render diagnostics";
            return;
        }

        var history = session.Commands.HistoryDiagnostics;
        _performanceStatus.Text =
            $"{diagnostics.CacheOutcome} · upload {diagnostics.UploadMode} {diagnostics.UploadPixelCount}px\n" +
            $"render hit {diagnostics.Cache.CacheHitCount} · partial {diagnostics.Cache.PartialRecomposeCount} · full {diagnostics.Cache.FullRecomposeCount}\n" +
            $"undo {history.EstimatedHistoryBytes / 1024d:0.0} KiB / {history.MemoryBudgetBytes / 1024d:0.0} KiB · evicted {history.EvictedUndoEntryCount}";
    }

    private void RefreshTools()
    {
        _toolsPanel.Children.Clear();
        var session = _workspace.CurrentSession;
        if (session is null) return;
        foreach (var tool in session.GetTools())
        {
            var button = new Button
            {
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = $"{(tool.IsActive ? "●" : "○")} {tool.DisplayName}",
                IsEnabled = session.HasEditableCel,
            };
            var toolId = tool.Id;
            button.Click += (_, _) => session.SelectTool(toolId);
            _toolsPanel.Children.Add(button);
        }
    }

    private void RefreshToolOptions()
    {
        _toolOptionsPanel.Children.Clear();
        var session = _workspace.CurrentSession;
        if (session is null) return;
        var options = session.GetToolOptions();
        if (options.Count == 0)
        {
            _toolOptionsPanel.Children.Add(new TextBlock { Text = "No editable tool options" });
            return;
        }

        foreach (var option in options)
        {
            switch (option.Kind)
            {
                case ToolOptionPresentationKind.Boolean:
                {
                    var check = new CheckBox
                    {
                        Content = option.DisplayName,
                        IsChecked = (bool)option.Value,
                    };
                    var optionId = option.Id;
                    check.Click += (_, _) => session.SetToolOption(optionId, check.IsChecked == true);
                    _toolOptionsPanel.Children.Add(check);
                    break;
                }
                case ToolOptionPresentationKind.Integer:
                {
                    var value = (int)option.Value;
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = EditorThemeTokens.CompactSpacing };
                    row.Children.Add(new TextBlock
                    {
                        Text = $"{option.DisplayName}: {value}",
                        VerticalAlignment = VerticalAlignment.Center,
                        Width = 150,
                    });
                    var optionId = option.Id;
                    row.Children.Add(CreateViewButton("−", () =>
                    {
                        var next = Math.Max(option.Minimum ?? int.MinValue, value - 1);
                        session.SetToolOption(optionId, next);
                    }));
                    row.Children.Add(CreateViewButton("+", () =>
                    {
                        var next = Math.Min(option.Maximum ?? int.MaxValue, value + 1);
                        session.SetToolOption(optionId, next);
                    }));
                    _toolOptionsPanel.Children.Add(row);
                    break;
                }
                case ToolOptionPresentationKind.Enum:
                {
                    var row = new StackPanel { Spacing = 2 };
                    row.Children.Add(new TextBlock { Text = option.DisplayName });
                    var combo = new ComboBox
                    {
                        ItemsSource = option.AllowedValues,
                        SelectedItem = (string)option.Value,
                    };
                    var optionId = option.Id;
                    combo.SelectionChanged += (_, _) =>
                    {
                        if (combo.SelectedItem is string selected) session.SetToolOption(optionId, selected);
                    };
                    row.Children.Add(combo);
                    _toolOptionsPanel.Children.Add(row);
                    break;
                }
            }
        }
    }

    private void RefreshLayers()
    {
        _layersPanel.Children.Clear();
        var session = _workspace.CurrentSession;
        if (session is null) return;
        foreach (var layer in session.GetLayers())
        {
            var button = new Button
            {
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = $"{(layer.IsCurrent ? "●" : "○")} {layer.Name}  {(layer.Visible ? "visible" : "hidden")}",
            };
            var id = layer.Id;
            button.Click += (_, _) => session.SelectLayer(id);
            _layersPanel.Children.Add(button);
        }
    }

    private void RefreshPalette()
    {
        _palettePanel.Children.Clear();
        var session = _workspace.CurrentSession;
        if (session is null) return;
        var palettes = session.GetPalettes();
        if (palettes.Count == 0)
        {
            _palettePanel.Children.Add(new TextBlock { Text = "RGBA document · no indexed palette" });
            return;
        }
        foreach (var palette in palettes)
            _palettePanel.Children.Add(new TextBlock
            {
                Text = $"{palette.Id} · {palette.ColorCount} colors · transparent {(palette.TransparentIndex?.ToString() ?? "none")}",
                TextWrapping = TextWrapping.Wrap,
            });
    }

    private void RefreshRecovery()
    {
        _recoveryPanel.Children.Clear();
        IReadOnlyList<RecoveryCandidatePresentation> candidates;
        try
        {
            candidates = _recovery.Discover();
        }
        catch (Exception ex)
        {
            _recoveryPanel.Children.Add(new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap });
            return;
        }

        if (candidates.Count == 0)
        {
            _recoveryPanel.Children.Add(new TextBlock { Text = "No recovery checkpoints" });
            return;
        }

        foreach (var candidate in candidates.Take(6))
        {
            var block = new StackPanel { Spacing = 2 };
            var source = candidate.SourcePath is null ? "Untitled" : Path.GetFileName(candidate.SourcePath);
            block.Children.Add(new TextBlock
            {
                Text = $"{source} · {candidate.State}\n{candidate.CreatedUtc?.ToLocalTime():g}",
                TextWrapping = TextWrapping.Wrap,
            });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = EditorThemeTokens.CompactSpacing };
            if (candidate.IsRecoverable)
            {
                var recoverId = candidate.RecoveryId;
                actions.Children.Add(CreateViewButton("Recover", () => RecoverCandidate(recoverId)));
            }
            var dismissId = candidate.RecoveryId;
            actions.Children.Add(CreateViewButton("Dismiss", () => DismissCandidate(dismissId)));
            block.Children.Add(actions);
            _recoveryPanel.Children.Add(block);
        }
    }

    private void RefreshTimeline()
    {
        _timelineFrames.Children.Clear();
        var session = _workspace.CurrentSession;
        if (session is null)
        {
            _timelineStatus.Text = "0 frames";
            return;
        }
        var total = session.CaptureSnapshot().FrameOrder.Count;
        _timelineStart = Math.Clamp(_timelineStart, 0, Math.Max(0, total - 1));
        var window = session.GetTimelineWindow(_timelineStart, TimelinePageSize);
        _timelineStatus.Text = $"{window.StartIndex + 1}–{Math.Min(window.TotalCount, window.StartIndex + window.Items.Count)} / {window.TotalCount}";
        foreach (var frame in window.Items)
        {
            var button = new Button
            {
                Content = $"{(frame.IsCurrent ? "●" : "○")} {frame.Index + 1}\n{frame.DurationTicks}t",
                MinWidth = 64,
                Padding = new Thickness(6, 4),
            };
            var id = frame.Id;
            button.Click += (_, _) => session.SelectFrame(id);
            _timelineFrames.Children.Add(button);
        }
    }

    private void OnAutosaveTick(object? sender, EventArgs e)
    {
        var attempts = _recovery.Tick(DateTimeOffset.UtcNow);
        if (attempts.Count == 0) return;

        var failure = attempts.FirstOrDefault(attempt => !attempt.WroteCheckpoint);
        _documentStatus.Text = failure is null
            ? $"Autosaved {attempts.Count} document(s)."
            : $"Autosave failed: {failure.Error}";
        RefreshRecovery();
    }

    private void RecoverCandidate(string recoveryId)
    {
        try
        {
            _recovery.Recover(recoveryId);
            _timelineStart = 0;
            _documentStatus.Text = "Recovered copy opened. Save explicitly to choose its destination.";
        }
        catch (Exception ex)
        {
            _documentStatus.Text = ex.Message;
        }
        RefreshRecovery();
        RefreshAll();
    }

    private void DismissCandidate(string recoveryId)
    {
        try
        {
            _recovery.Dismiss(recoveryId);
        }
        catch (Exception ex)
        {
            _documentStatus.Text = ex.Message;
        }
        RefreshRecovery();
    }

    private void PreviousTimelinePage()
    {
        _timelineStart = Math.Max(0, _timelineStart - TimelinePageSize);
        RefreshTimeline();
    }

    private void NextTimelinePage()
    {
        var session = _workspace.CurrentSession;
        if (session is null) return;
        var total = session.CaptureSnapshot().FrameOrder.Count;
        if (_timelineStart + TimelinePageSize < total) _timelineStart += TimelinePageSize;
        RefreshTimeline();
    }

    private void ChangeZoom(double factor)
    {
        var session = _workspace.CurrentSession;
        if (session is null) return;
        session.SetZoom(session.Zoom * factor);
    }

    private void SetZoom(double zoom) => _workspace.CurrentSession?.SetZoom(zoom);

    private static string GetRecoveryRootDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = AppContext.BaseDirectory;
        return Path.Combine(root, "MyLovePixel", "Recovery");
    }

    private static TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        FontWeight = FontWeight.SemiBold,
        FontSize = 14,
        Margin = new Thickness(0, 4, 0, 2),
    };

    private static Border PanelBorder(Control child, Thickness borderThickness) => new()
    {
        BorderBrush = EditorThemeTokens.PanelBorder,
        BorderThickness = borderThickness,
        Child = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = child,
        },
    };
}
