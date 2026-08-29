using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private readonly PixelPreviewView _quickPreview = new();
    private readonly WrapPanel _studioPaletteSwatches = new() { ItemWidth = 20, ItemHeight = 20 };
    private readonly NumericUpDown _studioR = ChannelInput();
    private readonly NumericUpDown _studioG = ChannelInput();
    private readonly NumericUpDown _studioB = ChannelInput();
    private readonly TextBox _studioHex = new() { Text = "#000000", MinWidth = 112 };
    private readonly Border _studioColorPreview = Swatch();
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

        return new Border
        {
            Height = 218,
            Margin = new Thickness(10, 10, 10, 8),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Background = EditorThemeTokens.PreviewBackground,
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
        if (_studioPaletteSwatches.Children.Count == 0)
        {
            foreach (var color in BuildStudioPaletteColors())
            {
                var captured = color;
                var button = new Button
                {
                    Width = 18,
                    Height = 18,
                    MinHeight = 18,
                    Padding = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    BorderBrush = EditorThemeTokens.PanelBorder,
                    BorderThickness = new Thickness(1),
                    Content = new Border
                    {
                        Background = Brush(captured),
                        CornerRadius = new CornerRadius(2),
                    },
                };
                ToolTip.SetTip(button, $"Apply #{captured.R:X2}{captured.G:X2}{captured.B:X2} to the active color");
                button.Click += (_, _) => ApplyStudioColor(captured);
                _studioPaletteSwatches.Children.Add(button);
            }
        }

        _studioColorPreview.Width = 28;
        _studioColorPreview.Height = 28;

        _studioR.ValueChanged += (_, _) => ApplyStudioRgb();
        _studioG.ValueChanged += (_, _) => ApplyStudioRgb();
        _studioB.ValueChanged += (_, _) => ApplyStudioRgb();
        _studioHex.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            ApplyStudioHex();
            e.Handled = true;
        };
        _studioHex.LostFocus += (_, _) => ApplyStudioHex();

        var rgb = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*,Auto,*"), ColumnSpacing = 5 };
        rgb.Children.Add(ChannelLabel("R"));
        rgb.Children.Add(Place(_studioR, 1));
        rgb.Children.Add(Place(ChannelLabel("G"), 2));
        rgb.Children.Add(Place(_studioG, 3));
        rgb.Children.Add(Place(ChannelLabel("B"), 4));
        rgb.Children.Add(Place(_studioB, 5));

        var hex = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 6 };
        hex.Children.Add(ChannelLabel("HEX"));
        hex.Children.Add(Place(_studioHex, 1));
        hex.Children.Add(Place(_studioColorPreview, 2));

        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(_studioPaletteSwatches);
        body.Children.Add(rgb);
        body.Children.Add(hex);

        var expander = new Expander
        {
            Header = "Palette · 128 colors",
            IsExpanded = true,
            Content = body,
        };

        return new Border
        {
            Padding = new Thickness(10, 7),
            CornerRadius = EditorThemeTokens.CardRadius,
            Background = EditorThemeTokens.SurfaceRaised,
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(1),
            Child = expander,
        };
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

        if (_studioHex.IsKeyboardFocusWithin || _studioR.IsKeyboardFocusWithin || _studioG.IsKeyboardFocusWithin || _studioB.IsKeyboardFocusWithin)
            return;

        var colors = session.GetToolColors();
        var active = _studioSecondaryTarget ? colors.Secondary : colors.Primary;
        if (active != _studioColor) SyncStudioColor(active);
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
            SetError("HEX color must be #RRGGBB or #RRGGBBAA.");
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
            _studioHex.Text = color.A == 255
                ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
                : $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
            _studioColorPreview.Background = Brush(color);
        }
        finally
        {
            _syncingStudioColor = false;
        }
    }

    private static bool TryParseHex(string? text, out Rgba32 color)
    {
        color = default;
        var value = (text ?? string.Empty).Trim();
        if (value.StartsWith('#')) value = value[1..];
        if (value.Length is not (6 or 8)) return false;
        if (!byte.TryParse(value.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !byte.TryParse(value.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !byte.TryParse(value.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b)) return false;
        var a = (byte)255;
        if (value.Length == 8 && !byte.TryParse(value.AsSpan(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a)) return false;
        color = new Rgba32(r, g, b, a);
        return true;
    }

    private static IReadOnlyList<Rgba32> BuildStudioPaletteColors()
    {
        var colors = new List<Rgba32>(128);
        var variants = new (double Saturation, double Value)[]
        {
            (0.90, 0.38),
            (0.82, 0.56),
            (0.78, 0.72),
            (0.68, 0.84),
            (0.58, 0.94),
            (0.42, 0.98),
            (0.28, 0.92),
        };

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

    private static bool IsEditingText(object? source) => source is TextBox or NumericUpDown or ComboBox;

    private void SelectQuickTool(string id)
    {
        var session = Current();
        if (session is null) return;
        Safe(() =>
        {
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

        var availableWidth = Math.Max(240d, Bounds.Width - EditorThemeTokens.ToolRailWidth - EditorThemeTokens.RightPanelWidth - 120d);
        var availableHeight = Math.Max(220d, Bounds.Height - EditorThemeTokens.TimelineHeight - 170d);
        var zoom = Math.Min(availableWidth / canvas.Width, availableHeight / canvas.Height) * 0.9d;
        SetZoom(Math.Clamp(zoom, 0.125d, 32d));
    }
}

internal sealed class PixelPreviewView : Control
{
    private readonly Dictionary<uint, IBrush> _brushes = [];
    private CanvasPresentation? _presentation;

    public void SetPresentation(CanvasPresentation? presentation)
    {
        if (ReferenceEquals(_presentation, presentation)) return;
        _presentation = presentation;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        if (bounds.Width <= 1d || bounds.Height <= 1d) return;

        context.FillRectangle(EditorThemeTokens.PreviewBackground, bounds);
        var presentation = _presentation;
        if (presentation is null || presentation.Size.Width <= 0 || presentation.Size.Height <= 0) return;

        var padding = 4d;
        var usableWidth = Math.Max(1d, bounds.Width - padding * 2d);
        var usableHeight = Math.Max(1d, bounds.Height - padding * 2d);
        var scale = Math.Min(usableWidth / presentation.Size.Width, usableHeight / presentation.Size.Height);
        var drawWidth = presentation.Size.Width * scale;
        var drawHeight = presentation.Size.Height * scale;
        var originX = (bounds.Width - drawWidth) / 2d;
        var originY = (bounds.Height - drawHeight) / 2d;

        var bytes = presentation.Rgba.Span;
        for (var y = 0; y < presentation.Size.Height; y++)
        for (var x = 0; x < presentation.Size.Width; x++)
        {
            var offset = ((y * presentation.Size.Width) + x) * 4;
            DrawPreviewPixel(
                context,
                originX,
                originY,
                scale,
                x,
                y,
                bytes[offset],
                bytes[offset + 1],
                bytes[offset + 2],
                bytes[offset + 3]);
        }

        foreach (var preview in presentation.PreviewPixels)
        {
            DrawPreviewPixel(
                context,
                originX,
                originY,
                scale,
                preview.Point.X,
                preview.Point.Y,
                preview.Color.R,
                preview.Color.G,
                preview.Color.B,
                preview.Color.A);
        }
    }

    private void DrawPreviewPixel(
        DrawingContext context,
        double originX,
        double originY,
        double scale,
        int x,
        int y,
        byte r,
        byte g,
        byte b,
        byte a)
    {
        if (a == 0) return;

        // Snap the scaled source cell outward to device-independent pixel bounds.
        // This keeps source pixels contiguous even when the fitted scale is fractional.
        var left = Math.Floor(originX + x * scale);
        var top = Math.Floor(originY + y * scale);
        var right = Math.Ceiling(originX + (x + 1) * scale);
        var bottom = Math.Ceiling(originY + (y + 1) * scale);
        if (right <= left || bottom <= top) return;

        context.FillRectangle(
            GetCompositeBrush(r, g, b, a),
            new Rect(left, top, right - left, bottom - top));
    }

    private IBrush GetCompositeBrush(byte r, byte g, byte b, byte a)
    {
        const byte backgroundR = 255;
        const byte backgroundG = 255;
        const byte backgroundB = 255;

        if (a < 255)
        {
            var inverse = 255 - a;
            r = (byte)((r * a + backgroundR * inverse + 127) / 255);
            g = (byte)((g * a + backgroundG * inverse + 127) / 255);
            b = (byte)((b * a + backgroundB * inverse + 127) / 255);
        }

        var key = ((uint)r << 16) | ((uint)g << 8) | b;
        if (_brushes.TryGetValue(key, out var brush)) return brush;
        brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        _brushes[key] = brush;
        return brush;
    }
}
