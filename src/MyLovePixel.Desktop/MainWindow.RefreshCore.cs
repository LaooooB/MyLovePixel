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
    private void RefreshAll()
    {
        if (_refreshing)
        {
            QueueRefreshAll();
            return;
        }

        _refreshing = true;
        try
        {
            ObserveCurrentSession();
            RefreshActions();
            RefreshCanvas();
            RefreshTools();
            RefreshToolOptions();
            RefreshLayers();
            RefreshPalette();
            RefreshEffects();
            RefreshTiles();
            RefreshAnimation();
            RefreshPlugins();
            RefreshRecovery();
            RefreshTimeline();
            RefreshStatus();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RefreshCanvas()
    {
        var session = Current();
        var presentation = session is null
            ? null
            : _plugins.RenderCanvas(
                session,
                _onionSkin
                    ? new OnionSkinPresentationSettings(_onionPrevious, _onionNext, _onionOpacity, _onionFalloff)
                    : null);
        _canvas.SetPresentation(presentation, session?.Zoom ?? 1d, session is null ? null : _selection.GetOverlay(session));
        if (session is null || presentation?.Diagnostics is not { } d)
        {
            _diagnostics.Text = string.Empty;
            return;
        }
        var h = session.Commands.HistoryDiagnostics;
        _diagnostics.Text = $"{d.CacheOutcome}\nUpload {d.UploadMode} · {d.UploadPixelCount}px\nHit {d.Cache.CacheHitCount} · Partial {d.Cache.PartialRecomposeCount} · Full {d.Cache.FullRecomposeCount}\nUndo {h.EstimatedHistoryBytes / 1024d:0.0}/{h.MemoryBudgetBytes / 1024d:0.0} KiB · Evicted {h.EvictedUndoEntryCount}";
    }

    private void RefreshTools()
    {
        _toolsPanel.Children.Clear();
        var session = Current();
        if (session is null) return;
        _toolsPanel.Margin = new Thickness(14, 6, 14, 8);

        var select = IconButton("▧", "Selection", () =>
        {
            _selectionMode = true;
            _plugins.CancelTool(session);
            RefreshTools();
            RefreshToolOptions();
        });
        if (_selectionMode) select.Classes.Add("selected");
        _toolsPanel.Children.Add(select);
        _toolsPanel.Children.Add(SeparatorH());

        foreach (var tool in _plugins.GetTools(session))
        {
            var id = tool.Id;
            var button = IconButton(ToolGlyph(id), tool.DisplayName, () =>
            {
                _selectionMode = false;
                session.EnsureEditableCel();
                _plugins.SelectTool(session, id);
                RefreshAll();
            });
            button.IsEnabled = session.HasEditableCel || session.CaptureSnapshot().Layers.ContainsKey(session.CurrentLayerId);
            if (!_selectionMode && tool.IsActive) button.Classes.Add("selected");
            _toolsPanel.Children.Add(button);
        }
    }

    private void RefreshToolOptions()
    {
        _toolOptionsPanel.Children.Clear();
        var session = Current();
        if (session is null) return;

        if (_selectionMode)
        {
            AddPanelLabel(_toolOptionsPanel, "Selection type");
            _toolOptionsPanel.Children.Add(Icons(
                SelectionModeButton("▧", "Rectangle", SelectionGestureMode.Rectangle),
                SelectionModeButton("○", "Ellipse", SelectionGestureMode.Ellipse),
                SelectionModeButton("⌁", "Lasso", SelectionGestureMode.Lasso),
                SelectionModeButton("◉", "By color", SelectionGestureMode.ByColor)));

            AddPanelLabel(_toolOptionsPanel, "Modify");
            _toolOptionsPanel.Children.Add(Icons(
                TextIconButton("▣", "Select All", "Select all", () => { _selection.SelectAll(session); RefreshCanvas(); }),
                TextIconButton("◐", "Invert", "Invert selection", () => { Safe(() => _selection.Invert(session)); RefreshCanvas(); }),
                IconButton("×", "Clear selection", () => { _selection.Clear(session); RefreshCanvas(); })));

            AddPanelLabel(_toolOptionsPanel, "Move");
            _toolOptionsPanel.Children.Add(Icons(
                IconButton("←", "Move left", () => MoveSelection(-1, 0)),
                IconButton("↑", "Move up", () => MoveSelection(0, -1)),
                IconButton("↓", "Move down", () => MoveSelection(0, 1)),
                IconButton("→", "Move right", () => MoveSelection(1, 0))));

            AddPanelLabel(_toolOptionsPanel, "Transform");
            _toolOptionsPanel.Children.Add(Icons(
                IconButton("↔", "Flip horizontal", () => TransformSelection(() => _selection.FlipHorizontal(session))),
                IconButton("↕", "Flip vertical", () => TransformSelection(() => _selection.FlipVertical(session))),
                IconButton("↻", "Rotate 90°", () => TransformSelection(() => _selection.RotateClockwise(session)))));
            if (_selection.GetOverlay(session) is { } overlay)
            {
                var w = Number(overlay.Bounds.Width, 1, 8192);
                var h = Number(overlay.Bounds.Height, 1, 8192);
                _toolOptionsPanel.Children.Add(Labeled("Scale to", Icons(w, h, TextIconButton("↗", "Apply", "Scale selection", () => TransformSelection(() => _selection.Scale(session, (int)(w.Value ?? 1), (int)(h.Value ?? 1)))))));
            }
            return;
        }

        var active = session.GetTools().FirstOrDefault(value => value.IsActive)?.DisplayName;
        if (!string.IsNullOrWhiteSpace(active))
        {
            var current = new TextBlock { Text = active };
            current.Classes.Add("section-title");
            _toolOptionsPanel.Children.Add(current);
        }

        foreach (var option in session.GetToolOptions())
        {
            switch (option.Kind)
            {
                case ToolOptionPresentationKind.Boolean:
                {
                    var check = new CheckBox { Content = ShortOption(option.DisplayName), IsChecked = (bool)option.Value };
                    var id = option.Id;
                    check.Click += (_, _) => session.SetToolOption(id, check.IsChecked == true);
                    _toolOptionsPanel.Children.Add(check);
                    break;
                }
                case ToolOptionPresentationKind.Integer:
                {
                    var input = new NumericUpDown
                    {
                        Value = (int)option.Value,
                        Minimum = option.Minimum ?? int.MinValue,
                        Maximum = option.Maximum ?? int.MaxValue,
                        Increment = 1,
                        FormatString = "0",
                    };
                    var id = option.Id;
                    input.ValueChanged += (_, _) => { if (input.Value is { } v) session.SetToolOption(id, (int)v); };
                    _toolOptionsPanel.Children.Add(Labeled(ShortOption(option.DisplayName), input));
                    break;
                }
                case ToolOptionPresentationKind.Enum:
                {
                    var combo = new ComboBox { ItemsSource = option.AllowedValues, SelectedItem = (string)option.Value };
                    var id = option.Id;
                    combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string v) session.SetToolOption(id, v); };
                    _toolOptionsPanel.Children.Add(Labeled(ShortOption(option.DisplayName), combo));
                    break;
                }
            }
        }
    }

    private void RefreshLayers()
    {
        _layersPanel.Children.Clear();
        var session = Current();
        if (session is null) return;
        _layersPanel.Children.Add(Icons(
            TextIconButton("＋", "Add Layer", "Add layer", () => session.AddLayer()),
            IconButton("↑", "Move layer up", () => session.MoveCurrentLayer(-1)),
            IconButton("↓", "Move layer down", () => session.MoveCurrentLayer(1)),
            IconButton("×", "Delete layer", () => session.RemoveCurrentLayer())));

        foreach (var layer in session.GetLayers())
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("30,30,*,62"), ColumnSpacing = 5 };
            var eye = SmallIcon(layer.Visible ? "●" : "○", layer.Visible ? "Hide" : "Show", () => session.SetLayerVisibility(layer.Id, !layer.Visible));
            row.Children.Add(eye);
            var lockButton = SmallIcon(layer.Locked ? "◆" : "◇", layer.Locked ? "Unlock" : "Lock", () => session.SetLayerLocked(layer.Id, !layer.Locked));
            Grid.SetColumn(lockButton, 1);
            row.Children.Add(lockButton);
            var name = new TextBox { Text = layer.Name, Padding = new Thickness(6, 3) };
            name.LostFocus += (_, _) => { if (!string.IsNullOrWhiteSpace(name.Text)) Safe(() => session.RenameLayer(layer.Id, name.Text!)); };
            name.PointerPressed += (_, _) => session.SelectLayer(layer.Id);
            if (layer.IsCurrent) name.BorderBrush = EditorThemeTokens.Accent;
            Grid.SetColumn(name, 2);
            row.Children.Add(name);
            var opacity = new NumericUpDown { Value = layer.Opacity, Minimum = 0, Maximum = 255, Increment = 1, FormatString = "0" };
            opacity.ValueChanged += (_, _) => { if (opacity.Value is { } v) Safe(() => session.SetLayerOpacity(layer.Id, (byte)v)); };
            ToolTip.SetTip(opacity, "Layer opacity");
            Grid.SetColumn(opacity, 3);
            row.Children.Add(opacity);
            _layersPanel.Children.Add(row);
        }
    }

    private void RefreshPalette()
    {
        _palettePanel.Children.Clear();
        var session = Current();
        if (session is null) return;
        var colors = session.GetToolColors();
        _primarySwatch.Background = Brush(colors.Primary);
        _secondarySwatch.Background = Brush(colors.Secondary);

        _palettePanel.Children.Add(Labeled("Primary", SwatchButton(_primarySwatch, "Primary color", true)));
        _palettePanel.Children.Add(Labeled("Secondary", SwatchButton(_secondarySwatch, "Secondary color", false)));
        _palettePanel.Children.Add(TextIconButton("⇄", "Swap Colors", "Swap primary and secondary colors", SwapColors));

        var editors = session.GetPaletteEditors();
        var format = session.GetCurrentSurfaceFormat();
        AddPanelLabel(_palettePanel, "Color processing");
        var processing = new WrapPanel { ItemHeight = 34 };
        var quantize = TextIconButton("", "Quantize", "Quantize current image", async () => await QuantizeCurrentAsync());
        var dither = TextIconButton("", "Dither", "Dither current image", async () => await DitherCurrentAsync());
        var shade = TextIconButton("", "Ramp / Shade", "Color ramp shading", async () => await ShadeCurrentAsync());
        var rgba = TextIconButton("", "Convert to RGBA", "Convert Indexed8 to RGBA", ConvertIndexedCurrentToRgba);
        quantize.IsEnabled = format == PixelFormat.Rgba32;
        dither.IsEnabled = format == PixelFormat.Rgba32 && editors.Count > 0;
        shade.IsEnabled = format == PixelFormat.Indexed8;
        rgba.IsEnabled = format == PixelFormat.Indexed8;
        processing.Children.Add(quantize);
        processing.Children.Add(dither);
        processing.Children.Add(shade);
        processing.Children.Add(rgba);
        _palettePanel.Children.Add(processing);

        AddPanelLabel(_palettePanel, "Palette");
        if (editors.Count == 0)
        {
            _palettePanel.Children.Add(TextIconButton("＋", "Create Grayscale Palette", "Create 16-color grayscale palette", () => { session.AddDefaultPalette(); RefreshPalette(); }));
            return;
        }

        if (_selectedPalette is null || editors.All(value => value.Id != _selectedPalette)) _selectedPalette = editors[0].Id;
        foreach (var palette in editors)
        {
            if (_selectedPalette == palette.Id && _selectedPaletteIndex is { } selectedIndex)
            {
                _palettePanel.Children.Add(Icons(
                    SmallIcon("←", "Move palette color left", () => { Safe(() => session.MovePaletteColor(palette.Id, selectedIndex, -1)); _selectedPaletteIndex = checked((byte)Math.Max(0, selectedIndex - 1)); RefreshPalette(); }),
                    SmallIcon("→", "Move palette color right", () => { Safe(() => session.MovePaletteColor(palette.Id, selectedIndex, 1)); _selectedPaletteIndex = checked((byte)Math.Min(palette.Colors.Count - 1, selectedIndex + 1)); RefreshPalette(); })));
            }
            var wrap = new WrapPanel { ItemWidth = 30, ItemHeight = 30 };
            foreach (var entry in palette.Colors)
            {
                var b = new Button { Width = 28, Height = 28, Padding = new Thickness(2), Content = new Border { Background = Brush(entry.Color), CornerRadius = new CornerRadius(3) } };
                ToolTip.SetTip(b, $"Palette index {entry.Index} · #{entry.Color.R:X2}{entry.Color.G:X2}{entry.Color.B:X2}{entry.Color.A:X2}");
                if (_selectedPalette == palette.Id && _selectedPaletteIndex == entry.Index) b.Classes.Add("selected");
                b.Click += (_, _) =>
                {
                    _selectedPalette = palette.Id;
                    _selectedPaletteIndex = entry.Index;
                    var current = session.GetToolColors();
                    session.SetToolColors(entry.Color, current.Secondary);
                    RefreshPalette();
                };
                b.DoubleTapped += async (_, _) =>
                {
                    var edited = await new ColorDialog(entry.Color).ShowDialog<Rgba32?>(this);
                    if (edited is { } value) session.SetPaletteColor(palette.Id, entry.Index, value);
                };
                wrap.Children.Add(b);
            }
            _palettePanel.Children.Add(wrap);
        }
    }

    private static void AddPanelLabel(Panel panel, string text)
    {
        var label = new TextBlock { Text = text, Margin = new Thickness(0, 4, 0, 0) };
        label.Classes.Add("toolbar-label");
        panel.Children.Add(label);
    }
}
