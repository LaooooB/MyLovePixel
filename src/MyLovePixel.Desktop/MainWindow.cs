using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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
    private readonly Dictionary<ActionId, List<Control>> _actionControls = [];
    private readonly PixelCanvasView _canvas = new();
    private readonly StackPanel _toolsPanel = new() { Spacing = 4 };
    private readonly StackPanel _layersPanel = new() { Spacing = 4 };
    private readonly StackPanel _toolOptionsPanel = new() { Spacing = 6 };
    private readonly StackPanel _palettePanel = new() { Spacing = 4 };
    private readonly StackPanel _timelineFrames = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly TextBlock _toolStatus = new();
    private readonly TextBlock _documentStatus = new();
    private readonly TextBlock _timelineStatus = new();
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
        _canvas.PointerInput = DispatchCanvasPointer;
        _canvas.CancelPointerInput = () => _workspace.CurrentSession?.CancelToolInteraction();
        Content = BuildShell();

        _workspace.Changed += OnWorkspaceChanged;
        KeyDown += OnKeyDown;
        _workspace.NewDocument(64, 64);
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
            BorderBrush = Brushes.DimGray,
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
            BorderBrush = Brushes.DimGray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = panel,
        };
    }

    private Control BuildWorkspace()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });

        var left = BuildLeftPanel();
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        var canvasHost = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border
            {
                Background = Brushes.Black,
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
        var content = new StackPanel { Spacing = 8, Margin = new Thickness(8) };
        content.Children.Add(SectionTitle("Tools"));
        content.Children.Add(_toolStatus);
        content.Children.Add(_toolsPanel);
        content.Children.Add(SectionTitle("Layers"));
        content.Children.Add(_layersPanel);
        return PanelBorder(content, new Thickness(0, 0, 1, 0));
    }

    private Control BuildRightPanel()
    {
        var content = new StackPanel { Spacing = 8, Margin = new Thickness(8) };
        content.Children.Add(SectionTitle("Tool Options"));
        content.Children.Add(_toolOptionsPanel);
        content.Children.Add(SectionTitle("Palette"));
        content.Children.Add(_palettePanel);
        content.Children.Add(SectionTitle("View"));

        var zoomRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        zoomRow.Children.Add(CreateViewButton("−", () => ChangeZoom(0.5)));
        zoomRow.Children.Add(CreateViewButton("+", () => ChangeZoom(2.0)));
        zoomRow.Children.Add(CreateViewButton("1:1", () => SetZoom(1d)));
        content.Children.Add(zoomRow);
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
            Height = 128,
            BorderBrush = Brushes.DimGray,
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
            return;
        }
        var name = session.FilePath is null ? "Untitled" : Path.GetFileName(session.FilePath);
        Title = $"MyLovePixel — {name}{(session.IsDirty ? " *" : string.Empty)}";
        _documentStatus.Text = $"Frame {session.CurrentFrameId} · Layer {session.CurrentLayerId} · Zoom {session.Zoom:0.###}×";
        _toolStatus.Text = $"Active: {session.ActiveToolId}{(session.HasEditableCel ? string.Empty : " · no Cel")}";
    }

    private void RefreshCanvas()
    {
        var session = _workspace.CurrentSession;
        _canvas.SetPresentation(session?.RenderCanvas(), session?.Zoom ?? 1d);
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
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
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

    private static TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        FontWeight = FontWeight.SemiBold,
        FontSize = 14,
        Margin = new Thickness(0, 4, 0, 2),
    };

    private static Border PanelBorder(Control child, Thickness borderThickness) => new()
    {
        BorderBrush = Brushes.DimGray,
        BorderThickness = borderThickness,
        Child = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = child,
        },
    };
}
