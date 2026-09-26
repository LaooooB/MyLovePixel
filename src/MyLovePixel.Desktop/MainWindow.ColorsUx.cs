using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private readonly NumericUpDown _studioA = ChannelInput();
    private readonly TextBlock _colorValidation = new() { IsVisible = false, Foreground = EditorThemeTokens.Danger, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _sampleInfo = new() { IsVisible = false, TextWrapping = TextWrapping.Wrap, Foreground = EditorThemeTokens.TextSecondary };
    private Button? _foregroundButton;
    private Button? _backgroundButton;
    private readonly List<(Button Button, Rgba32 Color)> _colorButtons = [];

    private Control BuildColorEditor()
    {
        var body = new StackPanel { Spacing = 7 };
        _foregroundButton = SwatchButton(_primarySwatch, "Foreground color", true);
        _backgroundButton = SwatchButton(_secondarySwatch, "Background color", false);
        var targets = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 5 };
        targets.Children.Add(_foregroundButton);
        targets.Children.Add(Place(_backgroundButton, 1));
        body.Children.Add(targets);

        var channels = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 5 };
        var inputs = new[] { _studioR, _studioG, _studioB, _studioA };
        var names = new[] { "R", "G", "B", "A" };
        for (var i = 0; i < inputs.Length; i++)
        {
            var input = inputs[i];
            input.MinWidth = 48;
            input.ShowButtonSpinner = false;
            Named(input, i == 3 ? "color.alpha" : "color." + names[i].ToLowerInvariant(), i == 3 ? "Alpha, 0 to 255" : names[i] + ", 0 to 255");
            input.ValueChanged += (_, _) => ApplyStudioRgb();
            var column = new StackPanel { Spacing = 3 };
            column.Children.Add(new TextBlock { Text = names[i], Foreground = EditorThemeTokens.TextSecondary });
            column.Children.Add(input);
            channels.Children.Add(Place(column, i));
        }
        body.Children.Add(channels);

        var hex = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 6 };
        hex.Children.Add(new TextBlock { Text = "HEX", VerticalAlignment = VerticalAlignment.Center, Foreground = EditorThemeTokens.TextSecondary });
        Named(_studioHex, "color.hex", "HEX color, RRGGBB or RRGGBBAA");
        _studioHex.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { ApplyStudioHex(); e.Handled = true; }
            else if (e.Key == Key.Escape) { SyncStudioColor(_studioColor); e.Handled = true; }
        };
        _studioHex.LostFocus += (_, _) => ApplyStudioHex();
        hex.Children.Add(Place(_studioHex, 1));
        hex.Children.Add(Place(TextIconButton("⇄", "Swap", "Swap foreground / background · X", SwapColors), 2));
        body.Children.Add(hex);
        body.Children.Add(Named(_colorValidation, "color.validation", "Color validation"));
        body.Children.Add(_sampleInfo);
        SyncStudioColor(_studioColor);
        return new Border { Padding = new Thickness(10, 8), Child = body, BorderBrush = EditorThemeTokens.PanelBorder, BorderThickness = new Thickness(0, 0, 0, 1) };
    }

    private Control BuildStudioPaletteEditor()
    {
        if (_studioPaletteSwatches.Children.Count == 0)
        {
            _studioPaletteSwatches.ItemWidth = 26;
            _studioPaletteSwatches.ItemHeight = 26;
            foreach (var color in new[] { Rgba32.Transparent }.Concat(BuildStudioPaletteColors()))
            {
                var captured = color;
                var label = color.A == 0 ? "Transparent" : Hex(color);
                var button = new Button
                {
                    Width = 24, Height = 24, MinHeight = 24, Padding = new Thickness(2),
                    Content = new ColorSwatchView { Color = color },
                };
                AutomationProperties.SetName(button, label);
                ToolTip.SetTip(button, label);
                ToolTip.SetPlacement(button, PlacementMode.Right);
                button.Click += (_, _) => ApplyStudioColor(captured);
                _colorButtons.Add((button, color));
                _studioPaletteSwatches.Children.Add(button);
            }
            _transparentPaletteInstalled = true;
        }
        var palette = new ScrollViewer { Content = _studioPaletteSwatches, MaxHeight = 156, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var expander = Expander("Color library", palette);
        expander.IsExpanded = true;
        return expander;
    }

    private Control BuildInspectorPreviewBox()
    {
        _quickPreview.Height = 132;
        return new Border { Margin = new Thickness(10, 0, 10, 8), Child = _quickPreview, ClipToBounds = true };
    }

    private void RefreshPalette()
    {
        var session = Current();
        if (session is null) return;
        var colors = session.GetToolColors();
        if (_primarySwatch.Child is ColorSwatchView primary) primary.Color = colors.Primary;
        if (_secondarySwatch.Child is ColorSwatchView secondary) secondary.Color = colors.Secondary;
        if (_foregroundButton is not null) SetSelected(_foregroundButton, !_studioSecondaryTarget);
        if (_backgroundButton is not null) SetSelected(_backgroundButton, _studioSecondaryTarget);
        RefreshConvenienceUi();
    }

    private void RefreshConvenienceUi()
    {
        if (Current() is not { } session) return;
        var colors = session.GetToolColors();
        var color = _studioSecondaryTarget ? colors.Secondary : colors.Primary;
        if (color != _studioColor) SyncStudioColor(color);
    }

    private void SetStudioColorTarget(bool secondary)
    {
        _studioSecondaryTarget = secondary;
        if (Current() is { } session)
        {
            var colors = session.GetToolColors();
            SyncStudioColor(secondary ? colors.Secondary : colors.Primary);
        }
        RefreshPalette();
    }

    private void ApplyStudioColor(Rgba32 color)
    {
        if (_syncingStudioColor || Current() is not { } session) return;
        try
        {
            session.ApplySampledColor(new PixelSample(color), _studioSecondaryTarget);
            SyncStudioColor(color);
            RefreshPalette();
        }
        catch (Exception ex)
        {
            _colorValidation.Text = ex.Message;
            _colorValidation.IsVisible = true;
        }
    }

    private void ApplyStudioRgb()
    {
        if (_syncingStudioColor || _studioR.Value is null || _studioG.Value is null || _studioB.Value is null || _studioA.Value is null) return;
        ApplyStudioColor(new Rgba32((byte)(_studioR.Value ?? 0m), (byte)(_studioG.Value ?? 0m), (byte)(_studioB.Value ?? 0m), (byte)(_studioA.Value ?? 255m)));
    }

    private void ApplyStudioHex()
    {
        if (_syncingStudioColor) return;
        if (!TryParseHex(_studioHex.Text, out var color))
        {
            _colorValidation.Text = "Use #RRGGBB or #RRGGBBAA.";
            _colorValidation.IsVisible = true;
            DataValidationErrors.SetErrors(_studioHex, new[] { "Invalid HEX color" });
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
            _studioR.Value = color.R; _studioG.Value = color.G; _studioB.Value = color.B; _studioA.Value = color.A;
            _studioHex.Text = Hex(color);
            if (_studioColorPreview.Child is ColorSwatchView preview) preview.Color = color;
            _colorValidation.IsVisible = false;
            DataValidationErrors.ClearErrors(_studioHex);
            foreach (var pair in _colorButtons) SetSelected(pair.Button, pair.Color == color);
        }
        finally { _syncingStudioColor = false; }
    }

    private static string Hex(Rgba32 color) => color.A == 255 ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
}
