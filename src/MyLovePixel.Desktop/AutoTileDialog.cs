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
    private readonly StackPanel _rows = new() { Spacing = 5 };
    private readonly NumericUpDown _x;
    private readonly NumericUpDown _y;
    private readonly NumericUpDown _width;
    private readonly NumericUpDown _height;
    private readonly ComboBox _mode;
    private readonly ComboBox _fallback;

    public AutoTileDialog(IReadOnlyList<TilePresentation> tiles, int startX, int startY)
    {
        _tiles = tiles;
        Title = "AutoTile"; Width = 620; Height = 520; MinWidth = 500; MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = EditorThemeTokens.AppBackground;

        _x = Number(startX, -8192, 8192); _y = Number(startY, -8192, 8192);
        _width = Number(8, 1, 512); _height = Number(8, 1, 512);
        _mode = new ComboBox { ItemsSource = new[] { "4", "8" }, SelectedIndex = 0 };
        _fallback = new ComboBox { ItemsSource = tiles.Select(v => v.Name).ToArray(), SelectedIndex = tiles.Count > 0 ? 0 : -1 };

        var root = new DockPanel();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(10, 8) };
        actions.Children.Add(ActionButton("＋", "Add mapping", () => AddRow(0, 0, 1)));
        actions.Children.Add(ActionButton("×", "Cancel", () => Close(null)));
        var apply = ActionButton("✓", "Apply", Apply); apply.Classes.Add("primary"); apply.IsEnabled = tiles.Count > 0; actions.Children.Add(apply);
        DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);

        var body = new StackPanel { Spacing = 10, Margin = new Thickness(12) };
        body.Children.Add(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*,90"), ColumnSpacing = 5,
            Children = { _x, Place(_y, 1), Place(_width, 2), Place(_height, 3), Place(_mode, 4) },
        });
        body.Children.Add(_fallback);
        body.Children.Add(new Separator());
        body.Children.Add(_rows);
        root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body });
        Content = root;

        if (tiles.Count > 0) AddRow(0, 0, 1);
    }

    private void AddRow(byte mask, int tileIndex, int weight)
    {
        if (_tiles.Count == 0) return;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,70,30"), ColumnSpacing = 5 };
        var maskInput = Number(mask, 0, 255);
        var tile = new ComboBox { ItemsSource = _tiles.Select(v => v.Name).ToArray(), SelectedIndex = Math.Clamp(tileIndex, 0, _tiles.Count - 1) };
        var weightInput = Number(weight, 1, 100000);
        row.Children.Add(maskInput); row.Children.Add(Place(tile, 1)); row.Children.Add(Place(weightInput, 2));
        row.Children.Add(Place(ActionButton("×", "Remove mapping", () => _rows.Children.Remove(row)), 3));
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
        var result = new AutoTileDialogResult(
            new IntRect((int)(_x.Value ?? 0), (int)(_y.Value ?? 0), (int)(_width.Value ?? 1), (int)(_height.Value ?? 1)),
            _mode.SelectedIndex == 1 ? AutoTileNeighborModePresentation.Eight : AutoTileNeighborModePresentation.Four,
            mappings,
            _tiles[_fallback.SelectedIndex].Id);
        Close(result);
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new() { Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0" };
    private static T Place<T>(T control, int column) where T : Control { Grid.SetColumn(control, column); return control; }
    private static Button ActionButton(string glyph, string tip, Action action) { var b = new Button { Content = glyph }; b.Classes.Add("small-icon"); b.Classes.Add("ghost"); ToolTip.SetTip(b, tip); b.Click += (_, _) => action(); return b; }
}
