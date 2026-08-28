using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using MyLovePixel.Application;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Desktop;

public sealed record AutoTileDialogResult(
    IntRect Area,
    AutoTileNeighborModePresentation NeighborMode,
    IReadOnlyList<AutoTileMappingPresentation> Mappings,
    TileId? FallbackTileId);

public sealed class AutoTileDialog : Window
{
    private readonly IReadOnlyList<TilePresentation> _tiles;
    private readonly StackPanel _rows = new() { Spacing = 6 };
    private readonly NumericUpDown _x;
    private readonly NumericUpDown _y;
    private readonly NumericUpDown _width;
    private readonly NumericUpDown _height;
    private readonly ComboBox _mode;
    private readonly ComboBox _fallback;

    public AutoTileDialog(IReadOnlyList<TilePresentation> tiles, int startX, int startY)
    {
        _tiles = tiles;
        Title = "AutoTile";
        Width = 680;
        Height = 590;
        MinWidth = 560;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        _x = Number(startX, -8192, 8192);
        _y = Number(startY, -8192, 8192);
        _width = Number(8, 1, 512);
        _height = Number(8, 1, 512);
        _mode = new ComboBox { ItemsSource = new[] { "4-neighbor", "8-neighbor" }, SelectedIndex = 0 };
        _fallback = new ComboBox { ItemsSource = tiles.Select(v => v.Name).ToArray(), SelectedIndex = tiles.Count > 0 ? 0 : -1 };

        var root = new DockPanel();
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(12, 8, 12, 12),
        };
        footer.Children.Add(DialogChrome.TextButton("Cancel", () => Close(null)));
        var apply = DialogChrome.TextButton("Apply AutoTile", Apply, primary: true);
        apply.IsEnabled = tiles.Count > 0;
        footer.Children.Add(apply);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var body = new StackPanel { Spacing = 10, Margin = new Thickness(16) };
        body.Children.Add(new TextBlock { Text = "AutoTile rule", FontSize = 15, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        body.Children.Add(DialogChrome.Help("Apply neighbor-mask mappings to a rectangular tilemap area. Mask, tile and weight can be edited per mapping."));
        body.Children.Add(DialogChrome.Labeled("Area X / Y", Pair(_x, _y)));
        body.Children.Add(DialogChrome.Labeled("Width / Height", Pair(_width, _height)));
        body.Children.Add(DialogChrome.Labeled("Neighbors", _mode));
        body.Children.Add(DialogChrome.Labeled("Fallback tile", _fallback));
        body.Children.Add(new Separator());

        var mappingHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,80,34"), ColumnSpacing = 6 };
        mappingHeader.Children.Add(Header("Mask"));
        mappingHeader.Children.Add(Place(Header("Tile"), 1));
        mappingHeader.Children.Add(Place(Header("Weight"), 2));
        body.Children.Add(mappingHeader);
        body.Children.Add(_rows);
        body.Children.Add(DialogChrome.TextButton("Add Mapping", () => AddRow(0, 0, 1)));

        root.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = body,
        });
        Content = root;

        if (tiles.Count > 0) AddRow(0, 0, 1);
    }

    private void AddRow(byte mask, int tileIndex, int weight)
    {
        if (_tiles.Count == 0) return;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,80,34"), ColumnSpacing = 6 };
        var maskInput = Number(mask, 0, 255);
        var tile = new ComboBox
        {
            ItemsSource = _tiles.Select(v => v.Name).ToArray(),
            SelectedIndex = Math.Clamp(tileIndex, 0, _tiles.Count - 1),
        };
        var weightInput = Number(weight, 1, 100000);
        row.Children.Add(maskInput);
        row.Children.Add(Place(tile, 1));
        row.Children.Add(Place(weightInput, 2));
        row.Children.Add(Place(DialogChrome.IconButton("×", "Remove mapping", () => _rows.Children.Remove(row)), 3));
        _rows.Children.Add(row);
    }

    private void Apply()
    {
        if (_tiles.Count == 0 || (uint)_fallback.SelectedIndex >= (uint)_tiles.Count) return;
        var mappings = new List<AutoTileMappingPresentation>();
        foreach (var row in _rows.Children.OfType<Grid>())
        {
            var mask = checked((byte)(((NumericUpDown)row.Children[0]).Value ?? 0));
            var combo = (ComboBox)row.Children[1];
            if ((uint)combo.SelectedIndex >= (uint)_tiles.Count) continue;
            var weight = (int)(((NumericUpDown)row.Children[2]).Value ?? 1);
            mappings.Add(new AutoTileMappingPresentation(mask, _tiles[combo.SelectedIndex].Id, weight));
        }

        Close(new AutoTileDialogResult(
            new IntRect(
                (int)(_x.Value ?? 0),
                (int)(_y.Value ?? 0),
                (int)(_width.Value ?? 1),
                (int)(_height.Value ?? 1)),
            _mode.SelectedIndex == 1 ? AutoTileNeighborModePresentation.Eight : AutoTileNeighborModePresentation.Four,
            mappings,
            _tiles[_fallback.SelectedIndex].Id));
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new()
    {
        Value = value,
        Minimum = min,
        Maximum = max,
        Increment = 1,
        FormatString = "0",
    };

    private static StackPanel Pair(Control a, Control b)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(a);
        row.Children.Add(b);
        return row;
    }

    private static TextBlock Header(string text)
    {
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.Classes.Add("subtle");
        return label;
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
