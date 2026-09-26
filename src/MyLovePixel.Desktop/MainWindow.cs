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

    private const int TimelinePageSize = 12;

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
        Width = 1280;
        Height = 820;
        MinWidth = 640;
        MinHeight = 400;
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
        _canvas.SecondaryPickRequested = ErasePixelFromCanvas;
        _canvas.ZoomFactorRequested = ChangeZoom;

        Content = BuildShell();
        InstallUxInput();
        _workspace.Changed += OnWorkspaceChanged;
        KeyDown += OnKeyDown;
        Closed += (_, _) =>
        {
            _closed = true;
            FinishParameterEdit();
            _autosaveTimer.Stop();
            _playbackTimer.Stop();
            _plugins.Dispose();
        };

        _workspace.NewDocument(64, 64);
        _autosaveTimer.Start();
        _playbackTimer.Start();
        RefreshAll();
    }

}
