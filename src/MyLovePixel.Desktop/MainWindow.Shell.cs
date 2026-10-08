using MyLovePixel.Core.Document;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private readonly ScrollViewer _canvasScroll = new();
    private readonly ComboBox _documentSelector = new() { MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _contextName = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _notice = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _noticeHost = new() { IsVisible = false };
    private readonly ProgressBar _busyProgress = new() { IsIndeterminate = true, Width = 52, Height = 4, IsVisible = false };
    private readonly TabControl _sideTabs = new();
    private readonly Expander _previewExpander = new() { Header = "Preview", IsExpanded = true };
    private readonly Expander _timelineExpander = new() { Header = "Timeline", IsExpanded = true };
    private readonly Button _playButton = new();
    private readonly Button _previousPage = new();
    private readonly Button _nextPage = new();
    private readonly NumericUpDown _frameDuration = Number(100, 1, 60_000);
    private Grid? _workspaceGrid;
    private ScrollViewer? _timelineContent;
    private Control? _fullViewOptions;
    private Control? _compactViewOptions;
    private readonly List<Control> _documentControls = [];
    private Control? _toolbar;
    private Control? _editorBody;
    private Control? _colorEditor;
    private bool _syncDocuments;

    private Control BuildShell()
    {
        var root = new DockPanel { Background = EditorThemeTokens.AppBackground };
        _timelineExpander.HorizontalAlignment = HorizontalAlignment.Stretch;
        _previewExpander.HorizontalAlignment = HorizontalAlignment.Stretch;
        _toolbar = BuildTopBar();
        DockPanel.SetDock(_toolbar, Dock.Top);
        root.Children.Add(_toolbar);

        var status = new Border { Background = EditorThemeTokens.Surface, Padding = new Thickness(10, 5), Child = _status };
        _status.TextTrimming = TextTrimming.CharacterEllipsis;
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);

        var noticeRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
        noticeRow.Children.Add(_busyProgress);
        noticeRow.Children.Add(Place(Named(_notice, "workspace.notice", "Notification"), 1));
        noticeRow.Children.Add(Place(TextIconButton("", "Dismiss", "Dismiss notification", () => _noticeHost.IsVisible = false), 2));
        _noticeHost.Child = noticeRow;
        _noticeHost.Padding = new Thickness(10, 7);
        _noticeHost.Background = EditorThemeTokens.SurfaceRaised;
        _noticeHost.BorderBrush = EditorThemeTokens.StrongBorder;
        _noticeHost.BorderThickness = new Thickness(0, 1, 0, 0);
        DockPanel.SetDock(_noticeHost, Dock.Bottom);
        root.Children.Add(_noticeHost);

        _timelineExpander.Content = _timelineContent = new ScrollViewer
        {
            Content = BuildTimeline(),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _timelineExpander.HorizontalContentAlignment = HorizontalAlignment.Stretch;

        _editorBody = BuildWorkspace();
        root.Children.Add(_editorBody);
        SizeChanged += (_, _) => UpdateAdaptiveLayout();
        UpdateAdaptiveLayout();
        return root;
    }

    private Control BuildTopBar()
    {
        var row = Icons(
            Named(TextIconButton("＋", "New", "New project · Ctrl+N", NewProjectAsync), "project.new", "New project"),
            ActionTextButton(BuiltinActionIds.OpenProject, "⌂", "Open", "Open project · Ctrl+O"),
            TextIconButton("⇥", "Import", "Import PNG or sprite JSON", ImportAssetAsync),
            ActionTextButton(BuiltinActionIds.SaveProject, "▣", "Save", "Save project · Ctrl+S", true),
            ActionTextButton(BuiltinActionIds.SaveProjectAs, "⇧", "Save As", "Save project as · Ctrl+Shift+S"),
            ActionTextButton(BuiltinActionIds.ExportProject, "⇩", "Export", "Export assets · Ctrl+E"),
            ActionTextButton(BuiltinActionIds.Undo, "↶", "Undo", "Undo · Ctrl+Z"),
            ActionTextButton(BuiltinActionIds.Redo, "↷", "Redo", "Redo · Ctrl+Y / Ctrl+Shift+Z"),
            DocumentControl(TextIconButton("×", "Clear Frame", "Clear unlocked layers in the current frame · Undo available", ClearCanvas)));
        return new Border { Background = EditorThemeTokens.Surface, Padding = new Thickness(8, 7, 3, 2), Child = row };
    }

    private Control BuildWorkspace()
    {
        var grid = _workspaceGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{EditorThemeTokens.ToolRailWidth},*,{EditorThemeTokens.RightPanelWidth}"),
            RowDefinitions = new RowDefinitions("*,Auto"),
        };
        _toolsPanel.Margin = new Thickness(7, 8);
        var rail = new Border
        {
            Background = EditorThemeTokens.Surface,
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = new ScrollViewer { Content = _toolsPanel, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
        };
        grid.Children.Add(rail);

        var canvasArea = new DockPanel();
        var documentRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 5, Margin = new Thickness(8, 5) };
        Named(_documentSelector, "workspace.documents", "Open documents");
        _documentSelector.SelectionChanged += (_, _) =>
        {
            if (_syncDocuments || _documentSelector.SelectedItem is not DocumentChoice choice) return;
            FinishParameterEdit();
            CancelCanvasInteraction();
            _workspace.Activate(choice.Session);
        };
        documentRow.Children.Add(_documentSelector);
        documentRow.Children.Add(Place(DocumentControl(Named(TextIconButton("×", "Close", "Close document · Ctrl+W", CloseCurrentDocumentAsync), "project.close", "Close document")), 1));
        DockPanel.SetDock(documentRow, Dock.Top);
        canvasArea.Children.Add(documentRow);

        _fullViewOptions = Icons(BuildGridToggleButton(),
            ToggleTextButton("◐", "Invert View", "Display-only inversion", () => _invertView,
                value => { _invertView = value; _canvas.SetInvert(value); }));
        _compactViewOptions = TextIconButton("", "View", "Grid and display options", ShowViewMenu);
        var viewRow = DocumentControl(Icons(
            TextIconButton("", "Fit", "Fit canvas · F", FitCanvas),
            TextIconButton("", "100%", "Actual size · 1", () => SetZoom(1d)),
            TextIconButton("−", "Zoom −", "Zoom out", () => ChangeZoom(0.8)),
            TextIconButton("＋", "Zoom +", "Zoom in", () => ChangeZoom(1.25)),
            _fullViewOptions, _compactViewOptions));
        viewRow.Margin = new Thickness(8, 0, 3, 0);
        DockPanel.SetDock(viewRow, Dock.Top);
        canvasArea.Children.Add(viewRow);
        _contextName.Margin = new Thickness(9, 2, 9, 5);
        DockPanel.SetDock(_contextName, Dock.Top);
        canvasArea.Children.Add(_contextName);

        _canvasScroll.Background = EditorThemeTokens.CanvasWorkspace;
        _canvasScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _canvasScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _canvasScroll.Content = _navigationFrame = new Border
        {
            Padding = new Thickness(400, 300),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = _canvas,
        };
        _canvasScroll.SizeChanged += (_, _) => ResizeNavigationSpace();
        canvasArea.Children.Add(_canvasScroll);
        grid.Children.Add(Place(canvasArea, 1));
        // Keep the full-height inspector beside the timeline, so saved colors
        // gain room without hiding animation controls or shrinking the canvas.
        var inspector = BuildInspector();
        Grid.SetRowSpan(inspector, 2);
        grid.Children.Add(Place(inspector, 2));
        Grid.SetRow(_timelineExpander, 1);
        Grid.SetColumnSpan(_timelineExpander, 2);
        grid.Children.Add(_timelineExpander);
        return grid;
    }

    private Control BuildInspector()
    {
        var root = new DockPanel { Background = EditorThemeTokens.Surface };
        _previewExpander.Content = BuildInspectorPreviewBox();
        _previewExpander.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        var previewShortcut = Named(SlimButton("Open large preview", OpenPreviewWindow), "preview.enlarge", "Open larger live preview");
        previewShortcut.HorizontalAlignment = HorizontalAlignment.Stretch;
        previewShortcut.Margin = new Thickness(9, 6);
        DockPanel.SetDock(previewShortcut, Dock.Bottom);
        root.Children.Add(previewShortcut);

        var extensions = new StackPanel { Spacing = 10 };
        extensions.Children.Add(Expander("Plugins", _pluginsPanel));
        extensions.Children.Add(Expander("Recovery", _recoveryPanel));
        extensions.Children.Add(Expander("Diagnostics", _diagnostics));
        _sideTabs.ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(() => new UniformGrid { Columns = 4 });
        _sideTabs.ItemsSource = new object[]
        {
            TextTab("Colors", BuildColorsPage()),
            TextTab("Layers", InspectorScroll(_layersPanel)),
            TextTab("Tools", InspectorScroll(_toolOptionsPanel)),
            TextTab("Effects", InspectorScroll(_effectsPanel)),
            TextTab("Tiles", InspectorScroll(_tilesPanel)),
            TextTab("Preview", _previewExpander),
            TextTab("More", InspectorScroll(Expander("Animation", _animationPanel), extensions)),
        };
        _sideTabs.SelectedIndex = 0;
        _sideTabs.SelectionChanged += (_, _) => QueueRefreshAll();
        root.Children.Add(_sideTabs);
        return root;
    }

    private static ScrollViewer InspectorScroll(params Control[] controls)
    {
        var stack = new StackPanel { Spacing = 10 };
        foreach (var control in controls) stack.Children.Add(control);
        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(10, 8), Child = stack },
        };
    }

    private Control BuildTimeline()
    {
        _playButton.Content = "Play";
        Named(_playButton, "timeline.play", "Play animation");
        _playButton.Click += (_, _) => { FinishParameterEdit(); TogglePlayback(); RefreshPlaybackState(); };
        _previousPage.Content = "Previous";
        _previousPage.Click += (_, _) => ChangeTimelinePage(-1);
        _nextPage.Content = "Next";
        _nextPage.Click += (_, _) => ChangeTimelinePage(1);
        Named(_previousPage, "timeline.previous", "Previous frame page");
        Named(_nextPage, "timeline.next", "Next frame page");
        var mode = new ComboBox { ItemsSource = new[] { "Loop", "Ping-pong" }, SelectedIndex = 0, MinWidth = 108 };
        mode.SelectionChanged += (_, _) => _playback.SetLoopMode(mode.SelectedIndex == 1 ? AnimationLoopMode.PingPong : AnimationLoopMode.Loop, Current());
        var controls = Icons(_playButton, mode,
            ToggleTextButton("◌", "Onion Skin", "Previous / next frame overlays", () => _onionSkin, value => { _onionSkin = value; RefreshCanvas(); }),
            TextIconButton("⧉", "Duplicate", "Independent frame copy", () => Current()?.DuplicateCurrentFrame(false)),
            TextIconButton("⛓", "Linked Copy", "Shared pixels: editing one linked frame also changes its copies", () => Current()?.DuplicateCurrentFrame(true)),
            TextIconButton("×", "Delete", "Delete current frame · Undo available", () => Current()?.RemoveCurrentFrame()),
            TextIconButton("←", "Left", "Move frame left", () => Current()?.MoveCurrentFrame(-1)),
            TextIconButton("→", "Right", "Move frame right", () => Current()?.MoveCurrentFrame(1)),
            _previousPage, _nextPage);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), ColumnSpacing = 6 };
        footer.Children.Add(_timelineStatus);
        footer.Children.Add(Place(new TextBlock { Text = "Duration", VerticalAlignment = VerticalAlignment.Center }, 1));
        _frameDuration.Width = 84;
        _frameDuration.ShowButtonSpinner = false;
        Named(_frameDuration, "timeline.duration", "Frame duration in milliseconds");
        _frameDuration.ValueChanged += (_, _) =>
        {
            if (!_syncTimeline && Current() is { } session && _frameDuration.Value is { } value)
                UpdateParameter(session, _frameDuration, "Frame duration", () => session.SetCurrentFrameDuration((long)(value * 1000m)));
        };
        WireParameterCompletion(_frameDuration);
        footer.Children.Add(Place(_frameDuration, 2));
        footer.Children.Add(Place(new TextBlock { Text = "ms", VerticalAlignment = VerticalAlignment.Center }, 3));
        var body = new StackPanel { Spacing = 5, Margin = new Thickness(10, 0, 10, 7) };
        body.Children.Add(controls);
        body.Children.Add(new ScrollViewer { Content = _timelineFrames, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 94 });
        body.Children.Add(footer);
        return body;
    }

    private sealed record DocumentChoice(DocumentSession Session, string Name)
    {
        public override string ToString() => Name;
    }
}
