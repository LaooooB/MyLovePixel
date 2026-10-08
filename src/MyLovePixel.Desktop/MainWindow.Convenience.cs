using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private readonly PixelPreviewView _quickPreview = new();
    private readonly NumericUpDown _studioR = ChannelInput();
    private readonly NumericUpDown _studioG = ChannelInput();
    private readonly NumericUpDown _studioB = ChannelInput();
    private readonly TextBox _studioHex = new() { Text = "#000000", PlaceholderText = "#654321", MinWidth = 112, MaxLength = 32 };
    private readonly Border _studioColorPreview = Swatch();
    private DocumentSession? _studioBoundSession;
    private bool _convenienceInstalled;
    private bool _syncingStudioColor;
    private bool _studioSecondaryTarget;
    private Rgba32 _studioColor = new(0, 0, 0, 255);

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_convenienceInstalled) return;
        _convenienceInstalled = true;

        _canvas.PointerWheelChanged += OnConvenienceCanvasWheel;
        KeyDown += OnConvenienceKeyDown;
        RefreshConvenienceUi();
        ApplyCanvasDisplaySettings();
        Dispatcher.UIThread.Post(FitCanvas, DispatcherPriority.Background);
    }

    protected override void OnClosed(EventArgs e)
    {
        _canvas.PointerWheelChanged -= OnConvenienceCanvasWheel;
        KeyDown -= OnConvenienceKeyDown;
        base.OnClosed(e);
    }

    private Control BuildInspectorPreviewBox()
    {
        _quickPreview.Height = 210;
        _quickPreview.HorizontalAlignment = HorizontalAlignment.Stretch;
        _quickPreview.ClipToBounds = true;

        return _comfortPreviewFrame = new Border
        {
            Height = 218,
            Margin = new Thickness(10, 10, 10, 8),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Background = CanvasBackdrop.Solid(_displaySettings.Frame),
            Child = _quickPreview,
        };
    }

    private Button BuildGridToggleButton()
    {
        var button = new Button
        {
            MinWidth = 76,
            Padding = new Thickness(9, 5),
        };
        button.Classes.Add("text-action");
        ToolTip.SetTip(button, "Show or hide the pixel grid");

        void Sync()
        {
            button.Content = _gridVisible ? "Grid On" : "Grid Off";
            if (_gridVisible)
            {
                if (!button.Classes.Contains("selected")) button.Classes.Add("selected");
            }
            else
            {
                button.Classes.Remove("selected");
            }
        }

        button.Click += (_, _) =>
        {
            _gridVisible = !_gridVisible;
            _canvas.SetGrid(_gridVisible);
            Sync();
        };
        Sync();
        return button;
    }

    private Control BuildStudioPaletteEditor()
    {
        _studioColorPreview.Width = 24;
        _studioColorPreview.Height = 24;
        _studioHex.MinWidth = 100;
        AutomationProperties.SetAutomationId(_studioHex, "studio-hex-input");
        AutomationProperties.SetName(_studioHex, "Active drawing HEX color, RRGGBB or RRGGBBAA");
        _studioR.ValueChanged += (_, _) => ApplyStudioRgb();
        _studioG.ValueChanged += (_, _) => ApplyStudioRgb();
        _studioB.ValueChanged += (_, _) => ApplyStudioRgb();
        _studioHex.TextChanged += (_, _) => RefreshStudioHexFeedback();
        _studioHex.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { ApplyStudioHex(); e.Handled = true; }
            else if (e.Key == Key.Escape) { SyncStudioColor(_studioColor); e.Handled = true; }
        };
        _studioApplyHex = LibraryButton("Apply", ApplyStudioHex, "studio-hex-apply");
        var hex = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 5 };
        hex.Children.Add(_studioHex);
        hex.Children.Add(Place(_studioApplyHex, 1));
        hex.Children.Add(Place(BuildColorPickerButton(), 2));
        var rgb = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*,Auto,*"), ColumnSpacing = 5 };
        rgb.Children.Add(ChannelLabel("R")); rgb.Children.Add(Place(_studioR, 1));
        rgb.Children.Add(Place(ChannelLabel("G"), 2)); rgb.Children.Add(Place(_studioG, 3));
        rgb.Children.Add(Place(ChannelLabel("B"), 4)); rgb.Children.Add(Place(_studioB, 5));
        var body = new StackPanel { Spacing = 6 };
        body.Children.Add(_palettePanel);
        body.Children.Add(hex);
        body.Children.Add(_studioHexHint);
        _saveActiveLibraryButton = LibraryButton("Save current", SaveActiveLibraryColor, "color-library-save-current");
        _pinTemporaryButton = LibraryButton("Add temp", PinTemporaryColor, "color-library-pin-temporary");
        ToolTip.SetTip(_pinTemporaryButton, "Keep this color in Temporary. Pick another pixel and add it too. Your slots survive restarting.");
        var rgbButton = LibraryButton("RGB", () => { }, "color-rgb-channels");
        var rgbBody = new StackPanel { Spacing = 8, Width = 280 };
        rgbBody.Children.Add(rgb);
        rgbBody.Children.Add(BuildTransparentColorButton());
        var rgbFlyout = new Flyout { Content = rgbBody, Placement = PlacementMode.Bottom };
        rgbButton.Click += (_, _) => rgbFlyout.ShowAt(rgbButton);
        ToolTip.SetTip(rgbButton, "Edit red, green and blue channels.");
        body.Children.Add(LibraryRow(_saveActiveLibraryButton, _pinTemporaryButton,
            LibraryButton("Eyedropper", SelectEyedropper, "color-eyedropper"), rgbButton));
        var result = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8, Margin = new Thickness(10) };
        result.Children.Add(new Border
        {
            Padding = new Thickness(8), CornerRadius = EditorThemeTokens.CardRadius,
            Background = EditorThemeTokens.SurfaceRaised, BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(1), Child = body,
        });
        var library = BuildUserPaletteEditor();
        Grid.SetRow(library, 1); result.Children.Add(library);
        RefreshStudioHexFeedback();
        return result;
    }

    private static NumericUpDown ChannelInput() => new()
    {
        Value = 0,
        Minimum = 0,
        Maximum = 255,
        Increment = 1,
        FormatString = "0",
        MinWidth = 54,
    };

    private static TextBlock ChannelLabel(string text)
    {
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.Classes.Add("muted");
        return label;
    }

    private void RefreshConvenienceUi()
    {
        var session = Current();
        if (session is null) return;

        var contextChanged = !ReferenceEquals(_studioBoundSession, session);
        _studioBoundSession = session;
        if (!contextChanged && (_studioHex.IsKeyboardFocusWithin || _studioR.IsKeyboardFocusWithin || _studioG.IsKeyboardFocusWithin || _studioB.IsKeyboardFocusWithin))
            return;

        var colors = session.GetToolColors();
        var active = _studioSecondaryTarget ? colors.Secondary : colors.Primary;
        if (contextChanged || active != _studioColor) SyncStudioColor(active);
    }

    private void SetStudioColorTarget(bool secondary)
    {
        _studioSecondaryTarget = secondary;
        var session = Current();
        if (session is not null)
        {
            var colors = session.GetToolColors();
            SyncStudioColor(secondary ? colors.Secondary : colors.Primary);
        }
        RefreshPalette();
    }

    private void ApplyStudioColor(Rgba32 color)
    {
        var session = Current();
        if (session is null) return;
        var current = session.GetToolColors();
        session.SetToolColors(
            _studioSecondaryTarget ? current.Primary : color,
            _studioSecondaryTarget ? color : current.Secondary);
        SyncStudioColor(color);
    }

    private void ApplyStudioRgb()
    {
        if (_syncingStudioColor) return;
        var color = new Rgba32(
            (byte)(_studioR.Value ?? 0m),
            (byte)(_studioG.Value ?? 0m),
            (byte)(_studioB.Value ?? 0m),
            _studioColor.A);
        ApplyStudioColor(color);
    }

    private void ApplyStudioHex()
    {
        if (_syncingStudioColor || !_studioHex.IsInitialized) return;
        if (!TryParseHex(_studioHex.Text, out var color))
        {
            _studioHexHint.Text = "Invalid HEX. Use #RRGGBB or #RRGGBBAA, for example #654321.";
            SetError("HEX color must be #RRGGBB or #RRGGBBAA. The drawing color has not changed.");
            return;
        }
        ApplyStudioColor(color);
    }

    private void SyncStudioColor(Rgba32 color)
    {
        _syncingStudioColor = true;
        try
        {
            _studioColor = color;
            _studioR.Value = color.R;
            _studioG.Value = color.G;
            _studioB.Value = color.B;
            _studioHex.Text = HexColor.Format(color);
            _studioColorPreview.Background = Brush(color);
        }
        finally
        {
            _syncingStudioColor = false;
        }
        RefreshStudioHexFeedback();
    }

    private static bool TryParseHex(string? text, out Rgba32 color) => HexColor.TryParse(text, out color);

    private static IReadOnlyList<Rgba32> BuildStudioPaletteColors()
    {
        var colors = new List<Rgba32>(512);
        var variants = new List<(double Saturation, double Value)>(31);

        var saturations = new[] { 0.22, 0.38, 0.54, 0.70, 0.86, 1.00 };
        var values = new[] { 0.30, 0.46, 0.62, 0.78, 0.94 };
        foreach (var value in values)
        foreach (var saturation in saturations)
            variants.Add((saturation, value));
        variants.Add((0.12, 0.99));

        foreach (var variant in variants)
        for (var hueIndex = 0; hueIndex < 16; hueIndex++)
            colors.Add(HsvToRgba(hueIndex * 360d / 16d, variant.Saturation, variant.Value));

        for (var i = 0; i < 16; i++)
        {
            var value = (byte)Math.Round(i * 255d / 15d);
            colors.Add(new Rgba32(value, value, value, 255));
        }

        return colors;
    }

    private static Rgba32 HsvToRgba(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var h = (hue % 360d) / 60d;
        var x = c * (1d - Math.Abs((h % 2d) - 1d));
        var (r1, g1, b1) = h switch
        {
            < 1d => (c, x, 0d),
            < 2d => (x, c, 0d),
            < 3d => (0d, c, x),
            < 4d => (0d, x, c),
            < 5d => (x, 0d, c),
            _ => (c, 0d, x),
        };
        var m = value - c;
        return new Rgba32(
            (byte)Math.Round((r1 + m) * 255d),
            (byte)Math.Round((g1 + m) * 255d),
            (byte)Math.Round((b1 + m) * 255d),
            255);
    }

    private void OnConvenienceCanvasWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.Handled || e.Delta.Y == 0d) return;
        ChangeZoom(e.Delta.Y > 0d ? 1.25d : 0.8d);
        e.Handled = true;
    }

    private void OnConvenienceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || IsEditingText(e.Source)) return;
        switch (e.Key)
        {
            case Key.I:
                SelectEyedropper();
                e.Handled = true;
                break;
            case Key.Escape:
                if (_eyedropperMode) { _eyedropperMode = false; RefreshTools(); RefreshToolOptions(); e.Handled = true; }
                break;
            case Key.B:
                SelectQuickTool("core.pencil");
                e.Handled = true;
                break;
            case Key.E:
                SelectQuickTool("core.eraser");
                e.Handled = true;
                break;
            case Key.G:
                SelectQuickTool("core.fill");
                e.Handled = true;
                break;
            case Key.F:
                FitCanvas();
                e.Handled = true;
                break;
            case Key.R:
            case Key.X:
                SwapColors();
                e.Handled = true;
                break;
            case Key.D1:
                SetZoom(1d);
                e.Handled = true;
                break;
        }
    }

    private static bool IsEditingText(object? source) => source is Visual visual &&
        visual.GetSelfAndVisualAncestors().Any(v => v is TextBox or NumericUpDown or ComboBox or Slider);

    private void SelectQuickTool(string id)
    {
        var session = Current();
        if (session is null) return;
        Safe(() =>
        {
            _eyedropperMode = false;
            _selectionMode = false;
            session.EnsureEditableCel();
            _plugins.SelectTool(session, id);
        });
        RefreshTools();
        RefreshToolOptions();
    }

    private void FitCanvas()
    {
        var session = Current();
        if (session is null) return;
        var canvas = session.CaptureSnapshot().Canvas.Size;
        if (canvas.Width <= 0 || canvas.Height <= 0) return;

        var availableWidth = Math.Max(1d, (_canvasScroll?.Viewport.Width ?? 640d) - 100d);
        var availableHeight = Math.Max(1d, (_canvasScroll?.Viewport.Height ?? 480d) - 100d);
        var zoom = Math.Min(availableWidth / canvas.Width, availableHeight / canvas.Height);
        SetZoom(Math.Clamp(zoom, DocumentSession.MinimumZoom, 32d));
        CenterCanvasViewport();
    }
}

