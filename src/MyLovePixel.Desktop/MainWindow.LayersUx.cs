using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private DocumentSession? _layerSession;
    private readonly StackPanel _layerRows = new() { Spacing = 4 };
    private readonly Dictionary<LayerId, LayerRow> _layerItems = [];
    private LayerId[] _layerOrder = [];
    private Button? _deleteLayerButton;

    private void RefreshLayers()
    {
        var session = Current();
        if (_layersPanel.Children.Count == 0)
        {
            _deleteLayerButton = TextIconButton("×", "Delete", "Delete current layer · Undo available", () => Current()?.RemoveCurrentLayer());
            _layersPanel.Children.Add(Icons(
                TextIconButton("＋", "Add", "Add layer", () => Current()?.AddLayer()),
                TextIconButton("↑", "Up", "Move layer up", () => Current()?.MoveCurrentLayer(-1)),
                TextIconButton("↓", "Down", "Move layer down", () => Current()?.MoveCurrentLayer(1)),
                _deleteLayerButton));
            var headings = new Grid { ColumnDefinitions = new ColumnDefinitions("46,40,*,72"), ColumnSpacing = 4 };
            foreach (var pair in new[] { ("Visible", 0), ("Lock", 1), ("Layer", 2), ("Opacity %", 3) })
                headings.Children.Add(Place(new TextBlock { Text = pair.Item1, FontSize = 12, Foreground = EditorThemeTokens.TextSecondary, TextWrapping = TextWrapping.Wrap }, pair.Item2));
            _layersPanel.Children.Add(headings);
            _layersPanel.Children.Add(_layerRows);
        }
        if (!ReferenceEquals(session, _layerSession))
        {
            _layerSession = session;
            _layerRows.Children.Clear();
            _layerItems.Clear();
            _layerOrder = [];
        }
        if (session is null) return;
        var layers = session.GetLayers();
        if (_deleteLayerButton is not null) _deleteLayerButton.IsEnabled = layers.Count > 1;
        var order = layers.Select(l => l.Id).ToArray();
        foreach (var layer in layers)
        {
            if (!_layerItems.TryGetValue(layer.Id, out var row))
            {
                var id = layer.Id;
                row = new LayerRow();
                _layerItems.Add(id, row);
                var captured = row;
                Named(row.Select, "layer." + id, "Select " + layer.Name);
                row.Select.Click += (_, _) => { FinishParameterEdit(); session.SelectLayer(id); };
                row.Select.DoubleTapped += async (_, _) => await RenameLayerAsync(session, id);
                row.Select.KeyDown += async (_, e) => { if (e.Key == Key.F2) { e.Handled = true; await RenameLayerAsync(session, id); } };
                row.Visible.Click += (_, _) => { if (!captured.Syncing) { FinishParameterEdit(); session.SetLayerVisibility(id, captured.Visible.IsChecked == true); } };
                row.Locked.Click += (_, _) => { if (!captured.Syncing) { FinishParameterEdit(); session.SetLayerLocked(id, captured.Locked.IsChecked == true); } };
                row.Opacity.ValueChanged += (_, _) =>
                {
                    if (!captured.Syncing && captured.Opacity.Value is { } value)
                        UpdateParameter(session, captured.Opacity, "Layer opacity", () => session.SetLayerOpacity(id, (byte)Math.Round(value * 255m / 100m)));
                };
                WireParameterCompletion(row.Opacity);
            }
            row.Syncing = true;
            try
            {
                row.Name.Text = layer.Name;
                row.Visible.IsChecked = layer.Visible;
                row.Locked.IsChecked = layer.Locked;
                if (!ReferenceEquals(row.Opacity, _parameterControl)) row.Opacity.Value = Math.Round(layer.Opacity * 100m / 255m, 1);
                AutomationProperties.SetName(row.Visible, "Visible: " + layer.Name);
                AutomationProperties.SetName(row.Locked, "Locked: " + layer.Name);
                AutomationProperties.SetName(row.Opacity, "Opacity of " + layer.Name + ", percent");
                ToolTip.SetTip(row.Select, layer.Name + " · Double-click or F2 to rename");
                row.Root.Background = layer.IsCurrent ? EditorThemeTokens.SurfaceSelected : EditorThemeTokens.Surface;
                row.Root.BorderBrush = layer.IsCurrent ? EditorThemeTokens.Accent : EditorThemeTokens.PanelBorder;
            }
            finally { row.Syncing = false; }
        }
        if (!_layerOrder.SequenceEqual(order))
        {
            _layerOrder = order;
            _layerRows.Children.Clear();
            foreach (var id in order) _layerRows.Children.Add(_layerItems[id].Root);
            foreach (var removed in _layerItems.Keys.Except(order).ToArray()) _layerItems.Remove(removed);
        }
    }

    private async Task RenameLayerAsync(DocumentSession session, LayerId id)
    {
        FinishParameterEdit();
        var name = session.GetLayers().First(l => l.Id == id).Name;
        var value = await new RenameItemDialog("Rename layer", name).ShowDialog<string?>(this);
        if (value is not null) Safe(() => session.RenameLayer(id, value));
        RefreshLayers();
    }

    private sealed class LayerRow
    {
        public readonly Border Root;
        public readonly CheckBox Visible = new() { HorizontalAlignment = HorizontalAlignment.Center };
        public readonly CheckBox Locked = new() { HorizontalAlignment = HorizontalAlignment.Center };
        public readonly TextBlock Name = new() { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        public readonly Button Select;
        public readonly NumericUpDown Opacity = new() { Minimum = 0, Maximum = 100, Increment = 1, FormatString = "0.#", Padding = new Thickness(2), MinWidth = 64 };
        public bool Syncing;

        public LayerRow()
        {
            Select = new Button { Content = Name, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(3, 4), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent };
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("46,40,*,72"), ColumnSpacing = 4 };
            grid.Children.Add(Visible);
            grid.Children.Add(Place(Locked, 1));
            grid.Children.Add(Place(Select, 2));
            grid.Children.Add(Place(Opacity, 3));
            Root = new Border { Child = grid, Padding = new Thickness(3), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
        }
    }
}
