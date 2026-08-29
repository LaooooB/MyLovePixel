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
    private readonly StackPanel _toolOptionsPanel = new() { Spacing = 8 };
    private readonly StackPanel _layersPanel = new() { Spacing = 6 };
    private readonly StackPanel _palettePanel = new() { Spacing = 8 };
    private readonly StackPanel _effectsPanel = new() { Spacing = 8 };
    private readonly StackPanel _tilesPanel = new() { Spacing = 8 };
    private readonly StackPanel _animationPanel = new() { Spacing = 8 };
    private readonly StackPanel _pluginsPanel = new() { Spacing = 8 };
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
        Width = 1480;
        Height = 920;
        MinWidth = 1080;
        MinHeight = 700;
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

        var top = BuildTopBar();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        var status = new Border
        {
            Background = EditorThemeTokens.Surface,
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(10, 4),
            Child = _status,
        };
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);

        var timeline = BuildTimeline();
        DockPanel.SetDock(timeline, Dock.Bottom);
        root.Children.Add(timeline);

        root.Children.Add(BuildWorkspace());
        return root;
    }

    private Control BuildTopBar()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 10 };

        var project = ToolbarGroup("Project",
            TextIconButton("＋", "New", "New project · Ctrl+N", async () => await NewProjectAsync()),
            ActionTextButton(BuiltinActionIds.OpenProject, "⌂", "Open", "Open project · Ctrl+O"),
            TextIconButton("⇥", "Import", "Import PNG", ImportPngAsync),
            ActionTextButton(BuiltinActionIds.SaveProject, "▣", "Save", "Save project · Ctrl+S", primary: true),
            TextIconButton("⇧", "Save As", "Save As · Ctrl+Shift+S", async () => await InvokeActionAsync(BuiltinActionIds.SaveProjectAs)),
            TextIconButton("⇩", "Export", "Export · Ctrl+E", ExportAsync));
        row.Children.Add(project);

        var history = ToolbarGroup("History",
            ActionIcon(BuiltinActionIds.Undo, "↶", "Undo · Ctrl+Z"),
            ActionIcon(BuiltinActionIds.Redo, "↷", "Redo · Ctrl+Y"),
            IconButton("×", "Clear canvas · Ctrl+Z to undo", ClearCanvas));
        Grid.SetColumn(history, 1);
        row.Children.Add(history);

        var view = ToolbarGroup("View",
            IconButton("−", "Zoom out", () => ChangeZoom(0.8)),
            TextIconButton("", "100%", "Reset zoom to 100%", () => SetZoom(1d)),
            IconButton("＋", "Zoom in", () => ChangeZoom(1.25)),
            BuildGridToggleButton(),
            ToggleIcon("◐", "Invert black / white", () => _invertView, v => { _invertView = v; _canvas.SetInvert(v); }));
        Grid.SetColumn(view, 3);
        row.Children.Add(view);

        return new Border
        {
            Background = EditorThemeTokens.Surface,
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(10, 6),
            Child = row,
        };
    }

    private static Control ToolbarGroup(string title, params Control[] controls)
    {
        var root = new StackPanel { Spacing = 3 };
        var label = new TextBlock { Text = title };
        label.Classes.Add("toolbar-label");
        root.Children.Add(label);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var control in controls) row.Children.Add(control);
        root.Children.Add(row);
        return root;
    }

    private Control BuildWorkspace()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{EditorThemeTokens.ToolRailWidth},*,{EditorThemeTokens.RightPanelWidth}")
        };

        var toolsRoot = new DockPanel();
        var toolsTitle = new TextBlock
        {
            Text = "TOOLS",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 5),
        };
        toolsTitle.Classes.Add("toolbar-label");
        DockPanel.SetDock(toolsTitle, Dock.Top);
        toolsRoot.Children.Add(toolsTitle);
        toolsRoot.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _toolsPanel,
        });

        var rail = new Border
        {
            Background = EditorThemeTokens.Surface,
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = toolsRoot,
        };
        Grid.SetColumn(rail, 0);
        grid.Children.Add(rail);

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
                Padding = new Thickness(18),
                Margin = new Thickness(38),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = _canvas,
            },
        };
        Grid.SetColumn(canvasHost, 1);
        grid.Children.Add(canvasHost);

        var inspector = BuildInspector();
        Grid.SetColumn(inspector, 2);
        grid.Children.Add(inspector);
        return grid;
    }

    private Control BuildInspector()
    {
        var editorPage = InspectorScroll(
            SectionCard("Tool options", "Changes apply to the active drawing or selection tool.", _toolOptionsPanel),
            SectionCard("Color", "Choose Primary or Secondary, then pick a color from the palette below.", _palettePanel),
            BuildStudioPaletteEditor());

        var layersPage = InspectorScroll(
            SectionCard("Layers", "Select, rename, reorder, hide, lock and change opacity.", _layersPanel));

        var extensions = new StackPanel { Spacing = 10 };
        extensions.Children.Add(SectionCard("Plugins", "Load extensions and use plugin-provided commands or panels.", _pluginsPanel));
        extensions.Children.Add(SectionCard("Recovery", "Recover autosaved work after an interrupted session.", _recoveryPanel));
        extensions.Children.Add(SectionCard("Diagnostics", "Rendering and undo-memory diagnostics for troubleshooting.", _diagnostics));

        var advancedTabs = new TabControl
        {
            ItemsSource = new object[]
            {
                TextTab("Effects", InspectorScroll(SectionCard("Effects", "Non-destructive effect stack and parameter keyframes.", _effectsPanel))),
                TextTab("Tilemap", InspectorScroll(SectionCard("Tilemap", "Tilesets, maps, flags, AutoTile and tile-pixel editing.", _tilesPanel))),
                TextTab("Animation", InspectorScroll(SectionCard("Animation", "Animation metadata, onion skin, clips, tags and collision data.", _animationPanel))),
                TextTab("Extensions", new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = new Border { Padding = new Thickness(10), Child = extensions },
                }),
            },
        };

        var tabs = new TabControl
        {
            Background = EditorThemeTokens.Surface,
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(1, 0, 0, 0),
            ItemsSource = new object[]
            {
                TextTab("Edit", editorPage),
                TextTab("Photo", BuildPhotoPixelPanel()),
                TextTab("Layers", layersPage),
                TextTab("Advanced", advancedTabs),
            },
        };

        var root = new DockPanel { Background = EditorThemeTokens.Surface };
        var title = new Border
        {
            Padding = new Thickness(12, 10, 12, 7),
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(1, 0, 0, 1),
            Child = new TextBlock { Text = "Inspector", FontSize = 14, FontWeight = FontWeight.SemiBold },
        };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);

        var preview = BuildInspectorPreviewBox();
        DockPanel.SetDock(preview, Dock.Top);
        root.Children.Add(preview);

        root.Children.Add(tabs);
        return root;
    }

    private static ScrollViewer InspectorScroll(params Control[] controls)
    {
        var stack = new StackPanel { Spacing = 10 };
        foreach (var control in controls) stack.Children.Add(control);
        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border { Padding = new Thickness(10), Child = stack },
        };
    }

    private Control BuildTimeline()
    {
        var outer = new DockPanel { LastChildFill = true, Background = EditorThemeTokens.Surface };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(10, 6, 10, 5) };
        var title = new TextBlock { Text = "Timeline", FontSize = 13, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(title);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Left };
        controls.Children.Add(IconButton("▶", "Play / Pause animation", TogglePlayback));
        controls.Children.Add(ToggleTextButton("◌", "Onion Skin", "Onion skin", () => _onionSkin, value => { _onionSkin = value; RefreshCanvas(); RefreshAnimation(); }));
        controls.Children.Add(IconButton("⧉", "Duplicate frame", () => Current()?.DuplicateCurrentFrame(false)));
        controls.Children.Add(TextIconButton("⛓", "Linked Copy", "Linked frame copy", () => Current()?.DuplicateCurrentFrame(true)));
        controls.Children.Add(IconButton("×", "Delete frame", () => Current()?.RemoveCurrentFrame()));
        controls.Children.Add(IconButton("←", "Move frame left", () => Current()?.MoveCurrentFrame(-1)));
        controls.Children.Add(IconButton("→", "Move frame right", () => Current()?.MoveCurrentFrame(1)));
        Grid.SetColumn(controls, 1);
        header.Children.Add(controls);

        _timelineStatus.VerticalAlignment = VerticalAlignment.Center;
        _timelineStatus.Classes.Add("muted");
        Grid.SetColumn(_timelineStatus, 2);
        header.Children.Add(_timelineStatus);

        DockPanel.SetDock(header, Dock.Top);
        outer.Children.Add(header);
        outer.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border { Padding = new Thickness(10, 0, 10, 8), Child = _timelineFrames },
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
