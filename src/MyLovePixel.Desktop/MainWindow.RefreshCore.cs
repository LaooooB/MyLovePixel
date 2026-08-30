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

    private void RefreshCanvas(bool updatePreview = true)
    {
        var session = Current();
        var presentation = session is null
            ? null
            : _plugins.RenderCanvas(
                session,
                _onionSkin
                    ? new OnionSkinPresentationSettings(_onionPrevious, _onionNext, _onionOpacity, _onionFalloff)
                    : null);
        var selectionOverlay = session is null ? null : _selection.GetOverlay(session);
        _canvas.SelectionTransformInput = DispatchSelectionTransform;
        _canvas.SetPresentation(presentation, session?.Zoom ?? 1d, selectionOverlay);
        _canvas.SetSelectionTransformEnabled(_selectionMode && selectionOverlay is not null);
        if (updatePreview) _quickPreview.SetPresentation(presentation);
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
            RefreshCanvas(updatePreview: false);
        });
        if (_selectionMode) select.Classes.Add("selected");
        _toolsPanel.Children.Add(select);
        _toolsPanel.Children.Add(SeparatorH());

        var tools = _plugins.GetTools(session);
        for (var index = 0; index < tools.Count; index++)
        {
            var tool = tools[index];
            var id = tool.Id;
            var shortcut = index switch
            {
                < 9 => (index + 1).ToString(),
                9 => "0",
                _ => null,
            };
            var tip = shortcut is null
                ? tool.DisplayName
                : $"{tool.DisplayName} · {shortcut}";
            var button = IconButton(ToolGlyph(id), tool.DisplayName, () =>
            {
                CancelSelectionTransformGesture();
                _selectionMode = false;
                session.EnsureEditableCel();
                _plugins.SelectTool(session, id);
                RefreshAll();
            });
            ToolTip.SetTip(button, tip);
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

            var direct = new TextBlock
            {
                Text = "Free Transform: drag inside to move · drag any corner to enlarge or shrink · drag the round handle to rotate through 16 fixed directions (22.5° each). Hold Shift to lock movement or keep scale aspect ratio.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0),
            };
            direct.Classes.Add("subtle");
            _toolOptionsPanel.Children.Add(direct);
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
                    var id = option.Id;
                    if (option.Minimum is { } minimum && option.Maximum is { } maximum)
                    {
                        var slider = new GestureRackParameterSlider(
                            ShortOption(option.DisplayName),
                            (int)option.Value,
                            minimum,
                            maximum,
                            value => SetToolOptionFromSlider(session, id, value));
                        _toolOptionsPanel.Children.Add(Labeled(ShortOption(option.DisplayName), slider));
                    }
                    else
                    {
                        var input = new NumericUpDown
                        {
                            Value = (int)option.Value,
                            Minimum = option.Minimum ?? int.MinValue,
                            Maximum = option.Maximum ?? int.MaxValue,
                            Increment = 1,
                            FormatString = "0",
                        };
                        input.ValueChanged += (_, _) => { if (input.Value is { } v) session.SetToolOption(id, (int)v); };
                        _toolOptionsPanel.Children.Add(Labeled(ShortOption(option.DisplayName), input));
                    }
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
    }

    private static void AddPanelLabel(Panel panel, string text)
    {
        var label = new TextBlock { Text = text, Margin = new Thickness(0, 4, 0, 0) };
        label.Classes.Add("toolbar-label");
        panel.Children.Add(label);
    }
}
