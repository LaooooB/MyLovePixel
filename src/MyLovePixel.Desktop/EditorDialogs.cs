using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Export;

namespace MyLovePixel.Desktop;

public sealed record CanvasSizeChoice(int Width, int Height);

public sealed class NewProjectDialog : Window
{
    private readonly NumericUpDown _width = Number(64, 1, 4096);
    private readonly NumericUpDown _height = Number(64, 1, 4096);

    public NewProjectDialog()
    {
        Title = "New";
        Width = 330;
        Height = 230;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        var root = new StackPanel { Margin = new Thickness(14), Spacing = 10 };
        var presets = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var size in new[] { 16, 32, 64, 128, 256 })
        {
            var b = new Button { Content = size.ToString() };
            b.Click += (_, _) => { _width.Value = size; _height.Value = size; };
            presets.Children.Add(b);
        }
        root.Children.Add(presets);
        root.Children.Add(Row("W", _width));
        root.Children.Add(Row("H", _height));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6 };
        var cancel = new Button { Content = "×" }; cancel.Classes.Add("icon"); ToolTip.SetTip(cancel, "Cancel"); cancel.Click += (_, _) => Close(null);
        var ok = new Button { Content = "✓" }; ok.Classes.Add("icon"); ok.Classes.Add("primary"); ToolTip.SetTip(ok, "Create");
        ok.Click += (_, _) => Close(new CanvasSizeChoice((int)(_width.Value ?? 64), (int)(_height.Value ?? 64)));
        actions.Children.Add(cancel); actions.Children.Add(ok); root.Children.Add(actions);
        Content = root;
    }

    private static Control Row(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("32,*") };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(control, 1); grid.Children.Add(control); return grid;
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new() { Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0" };
}

public sealed class ColorDialog : Window
{
    private readonly NumericUpDown _r;
    private readonly NumericUpDown _g;
    private readonly NumericUpDown _b;
    private readonly NumericUpDown _a;
    private readonly Border _preview = new() { Height = 34, CornerRadius = new CornerRadius(5) };

    public ColorDialog(Rgba32 initial)
    {
        Title = "Color";
        Width = 300;
        Height = 300;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;
        _r = Channel(initial.R); _g = Channel(initial.G); _b = Channel(initial.B); _a = Channel(initial.A);
        foreach (var n in new[] { _r, _g, _b, _a }) n.ValueChanged += (_, _) => RefreshPreview();

        var root = new StackPanel { Margin = new Thickness(14), Spacing = 8 };
        root.Children.Add(_preview);
        root.Children.Add(Row("R", _r)); root.Children.Add(Row("G", _g)); root.Children.Add(Row("B", _b)); root.Children.Add(Row("A", _a));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6 };
        var cancel = new Button { Content = "×" }; cancel.Classes.Add("icon"); cancel.Click += (_, _) => Close(null);
        var ok = new Button { Content = "✓" }; ok.Classes.Add("icon"); ok.Classes.Add("primary"); ok.Click += (_, _) => Close(Current());
        actions.Children.Add(cancel); actions.Children.Add(ok); root.Children.Add(actions);
        Content = root;
        RefreshPreview();
    }

    private Rgba32 Current() => new((byte)(_r.Value ?? 0), (byte)(_g.Value ?? 0), (byte)(_b.Value ?? 0), (byte)(_a.Value ?? 255));
    private void RefreshPreview() { var c = Current(); _preview.Background = new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B)); }
    private static NumericUpDown Channel(byte value) => new() { Value = value, Minimum = 0, Maximum = 255, Increment = 1, FormatString = "0" };
    private static Control Row(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("32,*") };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }); Grid.SetColumn(control, 1); grid.Children.Add(control); return grid;
    }
}

public sealed class ExportDialog : Window
{
    private readonly ComboBox _layout = new() { ItemsSource = Enum.GetValues<ExportLayout>(), SelectedItem = ExportLayout.SpriteSheet };
    private readonly CheckBox _trim = new() { IsChecked = true, Content = "Trim" };
    private readonly NumericUpDown _scale = Number(1, 1, 64);
    private readonly NumericUpDown _padding = Number(0, 0, 4096);
    private readonly NumericUpDown _extrude = Number(0, 0, 4096);
    private readonly NumericUpDown _columns = Number(0, 0, 4096);
    private readonly CheckBox _pot = new() { Content = "POT" };

    public ExportDialog()
    {
        Title = "Export";
        Width = 350;
        Height = 390;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;
        var root = new StackPanel { Margin = new Thickness(14), Spacing = 8 };
        root.Children.Add(Row("Layout", _layout)); root.Children.Add(_trim);
        root.Children.Add(Row("Scale", _scale)); root.Children.Add(Row("Pad", _padding)); root.Children.Add(Row("Extrude", _extrude)); root.Children.Add(Row("Cols", _columns)); root.Children.Add(_pot);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6 };
        var cancel = new Button { Content = "×" }; cancel.Classes.Add("icon"); cancel.Click += (_, _) => Close(null);
        var ok = new Button { Content = "✓" }; ok.Classes.Add("icon"); ok.Classes.Add("primary"); ok.Click += (_, _) => Close(Build());
        actions.Children.Add(cancel); actions.Children.Add(ok); root.Children.Add(actions); Content = root;
    }

    private ExportPreset Build() => new()
    {
        Name = "Desktop",
        Layout = _layout.SelectedItem is ExportLayout layout ? layout : ExportLayout.SpriteSheet,
        Trim = _trim.IsChecked == true,
        Scale = (int)(_scale.Value ?? 1),
        Padding = (int)(_padding.Value ?? 0),
        Extrude = (int)(_extrude.Value ?? 0),
        SpriteSheetColumns = (int)(_columns.Value ?? 0),
        PowerOfTwoAtlas = _pot.IsChecked == true,
        ImageBaseName = "sprite",
        MetadataFileName = "sprite.json",
    };

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new() { Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0" };
    private static Control Row(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("72,*") };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }); Grid.SetColumn(control, 1); grid.Children.Add(control); return grid;
    }
}

public sealed record AnimationRangeChoice(string Name, int Start, int End, MyLovePixel.Core.Document.AnimationLoopMode LoopMode);

public sealed class AnimationRangeDialog : Window
{
    private readonly TextBox _name;
    private readonly NumericUpDown _start;
    private readonly NumericUpDown _end;
    private readonly ComboBox? _loop;

    public AnimationRangeDialog(string name, int start, int end, int frameCount, MyLovePixel.Core.Document.AnimationLoopMode? loopMode)
    {
        Title = loopMode is null ? "Tag" : "Clip";
        Width = 340;
        Height = loopMode is null ? 230 : 270;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        _name = new TextBox { Text = name };
        _start = Number(start + 1, 1, Math.Max(1, frameCount));
        _end = Number(end + 1, 1, Math.Max(1, frameCount));
        if (loopMode is not null)
            _loop = new ComboBox { ItemsSource = Enum.GetValues<MyLovePixel.Core.Document.AnimationLoopMode>(), SelectedItem = loopMode.Value };

        var root = new StackPanel { Margin = new Thickness(14), Spacing = 8 };
        root.Children.Add(Row("Name", _name));
        root.Children.Add(Row("Start", _start));
        root.Children.Add(Row("End", _end));
        if (_loop is not null) root.Children.Add(Row("Loop", _loop));
        root.Children.Add(Actions(
            () =>
            {
                var first = Math.Max(0, (int)(_start.Value ?? 1) - 1);
                var last = Math.Max(first, (int)(_end.Value ?? 1) - 1);
                var loop = _loop?.SelectedItem is MyLovePixel.Core.Document.AnimationLoopMode value ? value : MyLovePixel.Core.Document.AnimationLoopMode.Loop;
                Close(new AnimationRangeChoice(string.IsNullOrWhiteSpace(_name.Text) ? Title : _name.Text!.Trim(), first, last, loop));
            },
            () => Close(null)));
        Content = root;
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new() { Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0" };
    private static Control Row(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("72,*"), ColumnSpacing = 6 };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        text.Classes.Add("muted");
        grid.Children.Add(text); Grid.SetColumn(control, 1); grid.Children.Add(control); return grid;
    }
    private static Control Actions(Action accept, Action cancel)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6 };
        var no = new Button { Content = "×" }; no.Classes.Add("icon"); no.Classes.Add("ghost"); ToolTip.SetTip(no, "Cancel"); no.Click += (_, _) => cancel();
        var yes = new Button { Content = "✓" }; yes.Classes.Add("icon"); yes.Classes.Add("primary"); ToolTip.SetTip(yes, "Apply"); yes.Click += (_, _) => accept();
        row.Children.Add(no); row.Children.Add(yes); return row;
    }
}

public sealed record SpriteSliceChoice(
    string Name,
    int X,
    int Y,
    int Width,
    int Height,
    int PivotX,
    int PivotY,
    MyLovePixel.Core.Document.NineSliceInsets? NineSlice);

public sealed class SpriteSliceDialog : Window
{
    private readonly TextBox _name;
    private readonly NumericUpDown _x;
    private readonly NumericUpDown _y;
    private readonly NumericUpDown _w;
    private readonly NumericUpDown _h;
    private readonly NumericUpDown _px;
    private readonly NumericUpDown _py;
    private readonly CheckBox _nine = new() { Content = "9-slice" };
    private readonly NumericUpDown _left;
    private readonly NumericUpDown _top;
    private readonly NumericUpDown _right;
    private readonly NumericUpDown _bottom;

    public SpriteSliceDialog(MyLovePixel.Core.Document.SpriteSlice slice)
    {
        Title = "Slice";
        Width = 370;
        Height = 470;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        _name = new TextBox { Text = slice.Name };
        _x = Number(slice.Bounds.X, -8192, 8192); _y = Number(slice.Bounds.Y, -8192, 8192);
        _w = Number(slice.Bounds.Width, 1, 8192); _h = Number(slice.Bounds.Height, 1, 8192);
        _px = Number(slice.Pivot.X, -8192, 8192); _py = Number(slice.Pivot.Y, -8192, 8192);
        var nine = slice.NineSlice;
        _nine.IsChecked = nine is not null;
        _left = Number(nine?.Left ?? 0, 0, 8192); _top = Number(nine?.Top ?? 0, 0, 8192);
        _right = Number(nine?.Right ?? 0, 0, 8192); _bottom = Number(nine?.Bottom ?? 0, 0, 8192);

        var root = new StackPanel { Margin = new Thickness(14), Spacing = 8 };
        root.Children.Add(Row("Name", _name));
        root.Children.Add(Row("Origin", Pair(_x, _y)));
        root.Children.Add(Row("Size", Pair(_w, _h)));
        root.Children.Add(Row("Pivot", Pair(_px, _py)));
        root.Children.Add(_nine);
        root.Children.Add(Row("Insets", Quad(_left, _top, _right, _bottom)));
        root.Children.Add(Actions(
            () =>
            {
                MyLovePixel.Core.Document.NineSliceInsets? insets = _nine.IsChecked == true
                    ? new MyLovePixel.Core.Document.NineSliceInsets((int)(_left.Value ?? 0), (int)(_top.Value ?? 0), (int)(_right.Value ?? 0), (int)(_bottom.Value ?? 0))
                    : null;
                Close(new SpriteSliceChoice(
                    string.IsNullOrWhiteSpace(_name.Text) ? "Slice" : _name.Text!.Trim(),
                    (int)(_x.Value ?? 0), (int)(_y.Value ?? 0), (int)(_w.Value ?? 1), (int)(_h.Value ?? 1),
                    (int)(_px.Value ?? 0), (int)(_py.Value ?? 0), insets));
            },
            () => Close(null)));
        Content = root;
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new() { Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0" };
    private static StackPanel Pair(Control a, Control b) { var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 }; row.Children.Add(a); row.Children.Add(b); return row; }
    private static StackPanel Quad(params Control[] values) { var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 }; foreach (var value in values) row.Children.Add(value); return row; }
    private static Control Row(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("72,*"), ColumnSpacing = 6 };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }; text.Classes.Add("muted");
        grid.Children.Add(text); Grid.SetColumn(control, 1); grid.Children.Add(control); return grid;
    }
    private static Control Actions(Action accept, Action cancel)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6 };
        var no = new Button { Content = "×" }; no.Classes.Add("icon"); no.Classes.Add("ghost"); ToolTip.SetTip(no, "Cancel"); no.Click += (_, _) => cancel();
        var yes = new Button { Content = "✓" }; yes.Classes.Add("icon"); yes.Classes.Add("primary"); ToolTip.SetTip(yes, "Apply"); yes.Click += (_, _) => accept();
        row.Children.Add(no); row.Children.Add(yes); return row;
    }
}
