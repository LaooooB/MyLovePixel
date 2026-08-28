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

public sealed partial class MainWindow : Window
{
    private enum SelectionGestureMode { Rectangle, Ellipse, Lasso, ByColor }

    private const int TimelinePageSize = 24;

    private readonly EditorWorkspace _workspace = new();
    private readonly ActionRegistry _actions = ActionRegistry.CreateDefault();
    private readonly ShortcutMap _shortcuts = ShortcutMap.CreateDefault();
    private readonly AvaloniaEditorInteraction _interaction;
    private readonly EditorActionContext _actionContext;
    private readonly RecoveryWorkspaceCoordinator _recovery;
    private readonly PluginWorkspaceRuntime _plugins;
    private readonly SelectionWorkspaceRuntime _selection = new();
    private readonly PlaybackWorkspaceRuntime _playback = new();
    private readonly DispatcherTimer _autosaveTimer;
    private readonly DispatcherTimer _playbackTimer;
    private long _playbackTimestamp;

    private readonly Dictionary<ActionId, List<Control>> _actionControls = [];
    private readonly PixelCanvasView _canvas = new();
    private readonly StackPanel _toolsPanel = new() { Spacing = 5 };
    private readonly StackPanel _toolOptionsPanel = new() { Spacing = 6 };
    private readonly StackPanel _layersPanel = new() { Spacing = 5 };
    private readonly StackPanel _palettePanel = new() { Spacing = 6 };
    private readonly StackPanel _effectsPanel = new() { Spacing = 6 };
    private readonly StackPanel _tilesPanel = new() { Spacing = 6 };
    private readonly StackPanel _animationPanel = new() { Spacing = 6 };
    private readonly StackPanel _pluginsPanel = new() { Spacing = 6 };
    private readonly PluginPanelView _pluginPanelView = new() { MaxHeight = 260 };
    private readonly StackPanel _recoveryPanel = new() { Spacing = 6 };
    private readonly TextBlock _diagnostics = new() { TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _timelineFrames = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly TextBlock _status = new();
    private readonly TextBlock _timelineStatus = new();
    private readonly Border _primarySwatch = Swatch();
    private readonly Border _secondarySwatch = Swatch();

    private DocumentSession? _observedSession;
    private int _timelineStart;
    private bool _selectionMode;
    private SelectionGestureMode _selectionGesture = SelectionGestureMode.Rectangle;
    private (int X, int Y)? _selectionStart;
    private readonly List<IntPoint> _selectionVertices = [];
    private (int X, int Y)? _hover;
    private bool _invertView;
    private bool _gridVisible = true;
    private bool _onionSkin;
    private int _onionPrevious = 1;
    private int _onionNext = 1;
    private byte _onionOpacity = 96;
    private double _onionFalloff = 0.65;
    private PaletteId? _selectedPalette;
    private byte? _selectedPaletteIndex;
    private TilesetId? _selectedTileset;
    private TileId? _selectedTile;
    private TilemapId? _selectedTilemap;
    private bool _tileErase;
    private TileCellFlags _tileFlags;
    private int _tileViewportX;
    private int _tileViewportY;
    private (int X, int Y)? _selectedTileCell;
    private EffectInstanceId? _selectedEffect;

    public MainWindow()
    {
        Width = 1440;
        Height = 900;
        MinWidth = 980;
        MinHeight = 640;
        Title = "MyLovePixel";
        Background = EditorThemeTokens.AppBackground;
        TransparencyBackgroundFallback = EditorThemeTokens.AppBackground;

        _interaction = new AvaloniaEditorInteraction(this);
        _actionContext = new EditorActionContext(_workspace, _interaction);
        _plugins = _workspace.Plugins();
        _recovery = new RecoveryWorkspaceCoordinator(_workspace, GetRecoveryRootDirectory(), AutosavePolicy.Default);
        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _autosaveTimer.Tick += OnAutosaveTick;
        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _playbackTimer.Tick += OnPlaybackTick;
        _playbackTimestamp = Stopwatch.GetTimestamp();

        _canvas.PointerInput = DispatchCanvasPointer;
        _canvas.CancelPointerInput = CancelCanvasInteraction;
        _canvas.HoverPixelChanged = value => { _hover = value; RefreshStatus(); };
        _canvas.SecondaryPickRequested = PickColorFromCanvas;
        _canvas.ZoomFactorRequested = ChangeZoom;

        Content = BuildShell();
        _workspace.Changed += OnWorkspaceChanged;
        KeyDown += OnKeyDown;
        Closed += (_, _) =>
        {
            _autosaveTimer.Stop();
            _playbackTimer.Stop();
            _plugins.Dispose();
        };

        _workspace.NewDocument(64, 64);
        _autosaveTimer.Start();
        _playbackTimer.Start();
        RefreshAll();
    }

    private Control BuildShell()
    {
        var root = new DockPanel { Background = EditorThemeTokens.AppBackground };
        var top = BuildTopBar(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var status = new Border
        {
            Background = EditorThemeTokens.Surface,
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8, 3),
            Child = _status,
        };
        DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var timeline = BuildTimeline(); DockPanel.SetDock(timeline, Dock.Bottom); root.Children.Add(timeline);
        root.Children.Add(BuildWorkspace());
        return root;
    }

    private Control BuildTopBar()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var project = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(7, 5) };
        project.Children.Add(IconButton("＋", "New · Ctrl+N", async () => await NewProjectAsync()));
        project.Children.Add(ActionIcon(BuiltinActionIds.OpenProject, "⌂", "Open · Ctrl+O"));
        project.Children.Add(IconButton("⇥", "Import PNG", ImportPngAsync));
        project.Children.Add(ActionIcon(BuiltinActionIds.SaveProject, "▣", "Save · Ctrl+S", primary: true));
        project.Children.Add(IconButton("⇧", "Save As · Ctrl+Shift+S", async () => await InvokeActionAsync(BuiltinActionIds.SaveProjectAs)));
        project.Children.Add(IconButton("⇩", "Export · Ctrl+E", ExportAsync));
        project.Children.Add(SeparatorV());
        project.Children.Add(ActionIcon(BuiltinActionIds.Undo, "↶", "Undo · Ctrl+Z"));
        project.Children.Add(ActionIcon(BuiltinActionIds.Redo, "↷", "Redo · Ctrl+Y"));
        row.Children.Add(project);

        var view = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(7, 5), HorizontalAlignment = HorizontalAlignment.Right };
        view.Children.Add(IconButton("−", "Zoom out", () => ChangeZoom(0.8)));
        view.Children.Add(IconButton("1", "1:1", () => SetZoom(1d)));
        view.Children.Add(IconButton("＋", "Zoom in", () => ChangeZoom(1.25)));
        view.Children.Add(ToggleIcon("#", "Pixel grid", () => _gridVisible, v => { _gridVisible = v; _canvas.SetGrid(v); }));
        view.Children.Add(ToggleIcon("◐", "Invert black / white", () => _invertView, v => { _invertView = v; _canvas.SetInvert(v); }));
        Grid.SetColumn(view, 2); row.Children.Add(view);

        return new Border
        {
            Background = EditorThemeTokens.Surface,
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = row,
        };
    }

    private Control BuildWorkspace()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions($"{EditorThemeTokens.ToolRailWidth},*,{EditorThemeTokens.RightPanelWidth}") };
        var rail = new Border
        {
            Background = EditorThemeTokens.Surface,
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _toolsPanel,
            },
        };
        Grid.SetColumn(rail, 0); grid.Children.Add(rail);

        var canvasHost = new ScrollViewer
        {
            Background = EditorThemeTokens.CanvasWorkspace,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border
            {
                Background = EditorThemeTokens.CanvasFrame,
                BorderBrush = EditorThemeTokens.StrongBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16),
                Margin = new Thickness(32),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = _canvas,
            },
        };
        Grid.SetColumn(canvasHost, 1); grid.Children.Add(canvasHost);

        var inspector = BuildInspector(); Grid.SetColumn(inspector, 2); grid.Children.Add(inspector);
        return grid;
    }

    private Control BuildInspector()
    {
        var tabs = new TabControl
        {
            Background = EditorThemeTokens.Surface,
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(1, 0, 0, 0),
            ItemsSource = new object[]
            {
                Tab("⚙", "Tool", _toolOptionsPanel),
                Tab("☷", "Layers", _layersPanel),
                Tab("◉", "Color", _palettePanel),
                Tab("✦", "Effects", _effectsPanel),
                Tab("▦", "Tilemap", _tilesPanel),
                Tab("▶", "Animation", _animationPanel),
                Tab("◇", "Plugins", _pluginsPanel),
                Tab("↺", "Recovery", _recoveryPanel),
                Tab("⌁", "Diagnostics", _diagnostics),
            },
        };
        return tabs;
    }

    private static TabItem Tab(string glyph, string tip, Control content)
    {
        var header = new TextBlock { Text = glyph, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center };
        ToolTip.SetTip(header, tip);
        return new TabItem
        {
            Header = header,
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new Border { Padding = new Thickness(8), Child = content },
            },
        };
    }

    private Control BuildTimeline()
    {
        var outer = new DockPanel { LastChildFill = true, Background = EditorThemeTokens.Surface };
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(7, 5) };
        controls.Children.Add(IconButton("▶", "Play / Pause", TogglePlayback));
        controls.Children.Add(ToggleIcon("◌", "Onion skin", () => _onionSkin, value => { _onionSkin = value; RefreshCanvas(); RefreshAnimation(); }));
        controls.Children.Add(IconButton("⧉", "Duplicate frame", () => Current()?.DuplicateCurrentFrame(false)));
        controls.Children.Add(IconButton("⛓", "Linked frame", () => Current()?.DuplicateCurrentFrame(true)));
        controls.Children.Add(IconButton("×", "Delete frame", () => Current()?.RemoveCurrentFrame()));
        controls.Children.Add(IconButton("←", "Move frame left", () => Current()?.MoveCurrentFrame(-1)));
        controls.Children.Add(IconButton("→", "Move frame right", () => Current()?.MoveCurrentFrame(1)));
        controls.Children.Add(SeparatorV());
        controls.Children.Add(_timelineStatus);
        DockPanel.SetDock(controls, Dock.Top); outer.Children.Add(controls);
        outer.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border { Padding = new Thickness(7, 0, 7, 7), Child = _timelineFrames },
        });
        return new Border
        {
            Height = EditorThemeTokens.TimelineHeight,
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = outer,
        };
    }
}
