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
        Title = "New Project";
        Width = 380;
        Height = 300;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        var root = new StackPanel { Margin = new Thickness(16), Spacing = 10 };
        var heading = new TextBlock { Text = "Canvas size", FontSize = 15, FontWeight = FontWeight.SemiBold };
        root.Children.Add(heading);
        root.Children.Add(DialogChrome.Help("Choose a preset or enter a custom pixel size."));

        var presets = new WrapPanel { ItemHeight = 32 };
        foreach (var size in new[] { 16, 32, 64, 128, 256 })
        {
            var preset = size;
            var button = new Button { Content = $"{size}×{size}", Margin = new Thickness(0, 0, 6, 6) };
            button.Click += (_, _) => { _width.Value = preset; _height.Value = preset; };
            presets.Children.Add(button);
        }
        root.Children.Add(presets);
        root.Children.Add(DialogChrome.Labeled("Width", _width));
        root.Children.Add(DialogChrome.Labeled("Height", _height));
        root.Children.Add(DialogChrome.ConfirmCancel(
            () => Close(null),
            () => Close(new CanvasSizeChoice((int)(_width.Value ?? 64), (int)(_height.Value ?? 64))),
            "Create"));
        Content = root;
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new()
    {
        Value = value,
        Minimum = min,
        Maximum = max,
        Increment = 1,
        FormatString = "0",
    };
}

public sealed class ColorDialog : Window
{
    private readonly NumericUpDown _r;
    private readonly NumericUpDown _g;
    private readonly NumericUpDown _b;
    private readonly NumericUpDown _a;
    private readonly Border _preview = new() { Height = 42, CornerRadius = new CornerRadius(6) };

    public ColorDialog(Rgba32 initial)
    {
        Title = "Color";
        Width = 340;
        Height = 350;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        _r = Channel(initial.R);
        _g = Channel(initial.G);
        _b = Channel(initial.B);
        _a = Channel(initial.A);
        foreach (var n in new[] { _r, _g, _b, _a }) n.ValueChanged += (_, _) => RefreshPreview();

        var root = new StackPanel { Margin = new Thickness(16), Spacing = 9 };
        root.Children.Add(new TextBlock { Text = "RGBA Color", FontSize = 15, FontWeight = FontWeight.SemiBold });
        root.Children.Add(_preview);
        root.Children.Add(DialogChrome.Labeled("Red", _r));
        root.Children.Add(DialogChrome.Labeled("Green", _g));
        root.Children.Add(DialogChrome.Labeled("Blue", _b));
        root.Children.Add(DialogChrome.Labeled("Alpha", _a));
        root.Children.Add(DialogChrome.ConfirmCancel(() => Close(null), () => Close(Current()), "Apply"));
        Content = root;
        RefreshPreview();
    }

    private Rgba32 Current() => new(
        (byte)(_r.Value ?? 0),
        (byte)(_g.Value ?? 0),
        (byte)(_b.Value ?? 0),
        (byte)(_a.Value ?? 255));

    private void RefreshPreview()
    {
        var c = Current();
        _preview.Background = new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
    }

    private static NumericUpDown Channel(byte value) => new()
    {
        Value = value,
        Minimum = 0,
        Maximum = 255,
        Increment = 1,
        FormatString = "0",
    };
}

public sealed class ExportDialog : Window
{
    private readonly ComboBox _layout = new() { ItemsSource = Enum.GetValues<ExportLayout>(), SelectedItem = ExportLayout.SpriteSheet };
    private readonly CheckBox _trim = new() { IsChecked = true, Content = "Trim transparent edges" };
    private readonly NumericUpDown _scale = Number(1, 1, 64);
    private readonly NumericUpDown _padding = Number(0, 0, 4096);
    private readonly NumericUpDown _extrude = Number(0, 0, 4096);
    private readonly NumericUpDown _columns = Number(0, 0, 4096);
    private readonly CheckBox _pot = new() { Content = "Power-of-two atlas" };

    public ExportDialog()
    {
        Title = "Export";
        Width = 410;
        Height = 460;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        var root = new StackPanel { Margin = new Thickness(16), Spacing = 9 };
        root.Children.Add(new TextBlock { Text = "Export settings", FontSize = 15, FontWeight = FontWeight.SemiBold });
        root.Children.Add(DialogChrome.Help("Configure how the current document is packaged into image and metadata files."));
        root.Children.Add(DialogChrome.Labeled("Layout", _layout));
        root.Children.Add(_trim);
        root.Children.Add(DialogChrome.Labeled("Scale", _scale));
        root.Children.Add(DialogChrome.Labeled("Padding", _padding));
        root.Children.Add(DialogChrome.Labeled("Extrude", _extrude));
        root.Children.Add(DialogChrome.Labeled("Columns", _columns));
        root.Children.Add(_pot);
        root.Children.Add(DialogChrome.ConfirmCancel(() => Close(null), () => Close(Build()), "Continue"));
        Content = root;
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

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new()
    {
        Value = value,
        Minimum = min,
        Maximum = max,
        Increment = 1,
        FormatString = "0",
    };
}

public sealed record AnimationRangeChoice(
    string Name,
    int Start,
    int End,
    MyLovePixel.Core.Document.AnimationLoopMode LoopMode);

public sealed class AnimationRangeDialog : Window
{
    private readonly TextBox _name;
    private readonly NumericUpDown _start;
    private readonly NumericUpDown _end;
    private readonly ComboBox? _loop;

    public AnimationRangeDialog(
        string name,
        int start,
        int end,
        int frameCount,
        MyLovePixel.Core.Document.AnimationLoopMode? loopMode)
    {
        Title = loopMode is null ? "Edit Tag" : "Edit Clip";
        Width = 390;
        Height = loopMode is null ? 300 : 340;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        _name = new TextBox { Text = name };
        _start = Number(start + 1, 1, Math.Max(1, frameCount));
        _end = Number(end + 1, 1, Math.Max(1, frameCount));
        if (loopMode is not null)
            _loop = new ComboBox
            {
                ItemsSource = Enum.GetValues<MyLovePixel.Core.Document.AnimationLoopMode>(),
                SelectedItem = loopMode.Value,
            };

        var root = new StackPanel { Margin = new Thickness(16), Spacing = 9 };
        root.Children.Add(new TextBlock
        {
            Text = loopMode is null ? "Animation tag" : "Animation clip",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
        });
        root.Children.Add(DialogChrome.Help("Frame numbers are 1-based in this dialog."));
        root.Children.Add(DialogChrome.Labeled("Name", _name));
        root.Children.Add(DialogChrome.Labeled("Start frame", _start));
        root.Children.Add(DialogChrome.Labeled("End frame", _end));
        if (_loop is not null) root.Children.Add(DialogChrome.Labeled("Loop mode", _loop));
        root.Children.Add(DialogChrome.ConfirmCancel(
            () => Close(null),
            () =>
            {
                var first = Math.Max(0, (int)(_start.Value ?? 1) - 1);
                var last = Math.Max(first, (int)(_end.Value ?? 1) - 1);
                var loop = _loop?.SelectedItem is MyLovePixel.Core.Document.AnimationLoopMode value
                    ? value
                    : MyLovePixel.Core.Document.AnimationLoopMode.Loop;
                Close(new AnimationRangeChoice(
                    string.IsNullOrWhiteSpace(_name.Text) ? (loopMode is null ? "Tag" : "Clip") : _name.Text!.Trim(),
                    first,
                    last,
                    loop));
            },
            "Apply"));
        Content = root;
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new()
    {
        Value = value,
        Minimum = min,
        Maximum = max,
        Increment = 1,
        FormatString = "0",
    };
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
    private readonly CheckBox _nine = new() { Content = "Enable 9-slice insets" };
    private readonly NumericUpDown _left;
    private readonly NumericUpDown _top;
    private readonly NumericUpDown _right;
    private readonly NumericUpDown _bottom;

    public SpriteSliceDialog(MyLovePixel.Core.Document.SpriteSlice slice)
    {
        Title = "Edit Slice";
        Width = 430;
        Height = 540;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        _name = new TextBox { Text = slice.Name };
        _x = Number(slice.Bounds.X, -8192, 8192);
        _y = Number(slice.Bounds.Y, -8192, 8192);
        _w = Number(slice.Bounds.Width, 1, 8192);
        _h = Number(slice.Bounds.Height, 1, 8192);
        _px = Number(slice.Pivot.X, -8192, 8192);
        _py = Number(slice.Pivot.Y, -8192, 8192);
        var nine = slice.NineSlice;
        _nine.IsChecked = nine is not null;
        _left = Number(nine?.Left ?? 0, 0, 8192);
        _top = Number(nine?.Top ?? 0, 0, 8192);
        _right = Number(nine?.Right ?? 0, 0, 8192);
        _bottom = Number(nine?.Bottom ?? 0, 0, 8192);

        var root = new StackPanel { Margin = new Thickness(16), Spacing = 9 };
        root.Children.Add(new TextBlock { Text = "Sprite slice", FontSize = 15, FontWeight = FontWeight.SemiBold });
        root.Children.Add(DialogChrome.Labeled("Name", _name));
        root.Children.Add(DialogChrome.Labeled("Origin X / Y", Pair(_x, _y)));
        root.Children.Add(DialogChrome.Labeled("Width / Height", Pair(_w, _h)));
        root.Children.Add(DialogChrome.Labeled("Pivot X / Y", Pair(_px, _py)));
        root.Children.Add(_nine);
        root.Children.Add(DialogChrome.Labeled("Insets L/T/R/B", Quad(_left, _top, _right, _bottom)));
        root.Children.Add(DialogChrome.ConfirmCancel(
            () => Close(null),
            () =>
            {
                MyLovePixel.Core.Document.NineSliceInsets? insets = _nine.IsChecked == true
                    ? new MyLovePixel.Core.Document.NineSliceInsets(
                        (int)(_left.Value ?? 0),
                        (int)(_top.Value ?? 0),
                        (int)(_right.Value ?? 0),
                        (int)(_bottom.Value ?? 0))
                    : null;
                Close(new SpriteSliceChoice(
                    string.IsNullOrWhiteSpace(_name.Text) ? "Slice" : _name.Text!.Trim(),
                    (int)(_x.Value ?? 0),
                    (int)(_y.Value ?? 0),
                    (int)(_w.Value ?? 1),
                    (int)(_h.Value ?? 1),
                    (int)(_px.Value ?? 0),
                    (int)(_py.Value ?? 0),
                    insets));
            },
            "Apply"));
        Content = root;
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
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        row.Children.Add(a);
        row.Children.Add(b);
        return row;
    }

    private static StackPanel Quad(params Control[] values)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        foreach (var value in values) row.Children.Add(value);
        return row;
    }
}
