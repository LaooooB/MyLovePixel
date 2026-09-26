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


    private void RefreshToolOptions()
    {
        if (ReuseToolOptions()) return;
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
                Text = "Shift: constrain · Esc: cancel",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0),
            };
            direct.Classes.Add("subtle");
            _toolOptionsPanel.Children.Add(direct);
            return;
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



    private static void AddPanelLabel(Panel panel, string text)
    {
        var label = new TextBlock { Text = text, Margin = new Thickness(0, 4, 0, 0) };
        label.Classes.Add("toolbar-label");
        panel.Children.Add(label);
    }
}
