using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using MyLovePixel.Application;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Desktop;

public sealed class AnimationBoxesDialog : Window
{
    private readonly StackPanel _rows = new() { Spacing = 6 };

    public AnimationBoxesDialog(string title, IReadOnlyList<AnimationBoxPresentation> values)
    {
        Title = title;
        Width = 540;
        Height = 430;
        MinWidth = 420;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        var root = new DockPanel();
        var top = Bar(
            () => AddRow(new AnimationBoxPresentation($"box{_rows.Children.Count + 1}", 0, 0, 1, 1)),
            () => Close(ReadRows()),
            () => Close(null));
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        foreach (var value in values) AddRow(value);
        root.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(10, 2, 10, 10), Child = _rows },
        });
        Content = root;
    }

    private void AddRow(AnimationBoxPresentation value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,58,58,58,58,30"), ColumnSpacing = 4 };
        var name = new TextBox { Text = value.Name, Watermark = "name" };
        var x = Number(value.X, -8192, 8192); var y = Number(value.Y, -8192, 8192);
        var w = Number(value.Width, 1, 8192); var h = Number(value.Height, 1, 8192);
        row.Children.Add(name); row.Children.Add(Place(x, 1)); row.Children.Add(Place(y, 2)); row.Children.Add(Place(w, 3)); row.Children.Add(Place(h, 4));
        var remove = Icon("×", "Remove", () => _rows.Children.Remove(row)); row.Children.Add(Place(remove, 5));
        _rows.Children.Add(row);
    }

    private IReadOnlyList<AnimationBoxPresentation> ReadRows() => _rows.Children.OfType<Grid>().Select(row =>
    {
        var name = (TextBox)row.Children[0];
        var x = (NumericUpDown)row.Children[1]; var y = (NumericUpDown)row.Children[2];
        var w = (NumericUpDown)row.Children[3]; var h = (NumericUpDown)row.Children[4];
        return new AnimationBoxPresentation(
            string.IsNullOrWhiteSpace(name.Text) ? "box" : name.Text!.Trim(),
            (int)(x.Value ?? 0), (int)(y.Value ?? 0), (int)(w.Value ?? 1), (int)(h.Value ?? 1));
    }).ToArray();

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new() { Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0" };
    private static T Place<T>(T control, int column) where T : Control { Grid.SetColumn(control, column); return control; }
    private static Button Icon(string glyph, string tip, Action action) { var b = new Button { Content = glyph }; b.Classes.Add("small-icon"); b.Classes.Add("ghost"); ToolTip.SetTip(b, tip); b.Click += (_, _) => action(); return b; }
    private static Control Bar(Action add, Action accept, Action cancel)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(10, 8) };
        row.Children.Add(Icon("＋", "Add", add));
        var spacer = new Border { HorizontalAlignment = HorizontalAlignment.Stretch }; row.Children.Add(spacer);
        var no = Icon("×", "Cancel", cancel); var yes = Icon("✓", "Apply", accept); yes.Classes.Add("primary");
        row.Children.Add(no); row.Children.Add(yes); return row;
    }
}

public sealed class AnimationSocketsDialog : Window
{
    private readonly StackPanel _rows = new() { Spacing = 6 };

    public AnimationSocketsDialog(IReadOnlyList<AnimationSocketPresentation> values)
    {
        Title = "Sockets";
        Width = 460; Height = 390; MinWidth = 380; MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = EditorThemeTokens.AppBackground;
        var root = new DockPanel();
        var bar = BuildBar(() => AddRow(new AnimationSocketPresentation($"socket{_rows.Children.Count + 1}", 0, 0)), () => Close(ReadRows()), () => Close(null));
        DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar);
        foreach (var value in values) AddRow(value);
        root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new Border { Padding = new Thickness(10, 2, 10, 10), Child = _rows } });
        Content = root;
    }

    private void AddRow(AnimationSocketPresentation value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,64,64,30"), ColumnSpacing = 4 };
        var name = new TextBox { Text = value.Name, Watermark = "name" }; var x = Number(value.X); var y = Number(value.Y);
        row.Children.Add(name); row.Children.Add(Place(x, 1)); row.Children.Add(Place(y, 2));
        row.Children.Add(Place(Icon("×", "Remove", () => _rows.Children.Remove(row)), 3)); _rows.Children.Add(row);
    }

    private IReadOnlyList<AnimationSocketPresentation> ReadRows() => _rows.Children.OfType<Grid>().Select(row => new AnimationSocketPresentation(
        string.IsNullOrWhiteSpace(((TextBox)row.Children[0]).Text) ? "socket" : ((TextBox)row.Children[0]).Text!.Trim(),
        (int)(((NumericUpDown)row.Children[1]).Value ?? 0), (int)(((NumericUpDown)row.Children[2]).Value ?? 0))).ToArray();

    private static NumericUpDown Number(decimal value) => new() { Value = value, Minimum = -8192, Maximum = 8192, Increment = 1, FormatString = "0" };
    private static T Place<T>(T c, int column) where T : Control { Grid.SetColumn(c, column); return c; }
    private static Button Icon(string glyph, string tip, Action action) { var b = new Button { Content = glyph }; b.Classes.Add("small-icon"); b.Classes.Add("ghost"); ToolTip.SetTip(b, tip); b.Click += (_, _) => action(); return b; }
    private static Control BuildBar(Action add, Action accept, Action cancel) { var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(10, 8) }; row.Children.Add(Icon("＋", "Add", add)); row.Children.Add(Icon("×", "Cancel", cancel)); var yes = Icon("✓", "Apply", accept); yes.Classes.Add("primary"); row.Children.Add(yes); return row; }
}

public sealed class AnimationEventsDialog : Window
{
    private readonly StackPanel _rows = new() { Spacing = 6 };

    public AnimationEventsDialog(IReadOnlyList<AnimationEventPresentation> values)
    {
        Title = "Events"; Width = 520; Height = 390; MinWidth = 420; MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = EditorThemeTokens.AppBackground;
        var root = new DockPanel();
        var bar = BuildBar(() => AddRow(new AnimationEventPresentation("event", string.Empty)), () => Close(ReadRows()), () => Close(null));
        DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar);
        foreach (var value in values) AddRow(value);
        root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new Border { Padding = new Thickness(10, 2, 10, 10), Child = _rows } });
        Content = root;
    }

    private void AddRow(AnimationEventPresentation value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("150,*,30"), ColumnSpacing = 4 };
        var name = new TextBox { Text = value.Name, Watermark = "event" }; var payload = new TextBox { Text = value.Payload, Watermark = "payload" };
        row.Children.Add(name); row.Children.Add(Place(payload, 1)); row.Children.Add(Place(Icon("×", "Remove", () => _rows.Children.Remove(row)), 2)); _rows.Children.Add(row);
    }

    private IReadOnlyList<AnimationEventPresentation> ReadRows() => _rows.Children.OfType<Grid>()
        .Select(row => new AnimationEventPresentation(((TextBox)row.Children[0]).Text?.Trim() ?? string.Empty, ((TextBox)row.Children[1]).Text ?? string.Empty))
        .Where(value => !string.IsNullOrWhiteSpace(value.Name)).ToArray();

    private static T Place<T>(T c, int column) where T : Control { Grid.SetColumn(c, column); return c; }
    private static Button Icon(string glyph, string tip, Action action) { var b = new Button { Content = glyph }; b.Classes.Add("small-icon"); b.Classes.Add("ghost"); ToolTip.SetTip(b, tip); b.Click += (_, _) => action(); return b; }
    private static Control BuildBar(Action add, Action accept, Action cancel) { var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(10, 8) }; row.Children.Add(Icon("＋", "Add", add)); row.Children.Add(Icon("×", "Cancel", cancel)); var yes = Icon("✓", "Apply", accept); yes.Classes.Add("primary"); row.Children.Add(yes); return row; }
}

public sealed class AnimationCyclesDialog : Window
{
    private readonly StackPanel _rows = new() { Spacing = 6 };
    private readonly IReadOnlyList<PaletteEditorPresentation> _palettes;

    public AnimationCyclesDialog(IReadOnlyList<PaletteEditorPresentation> palettes, IReadOnlyList<AnimationColorCyclePresentation> values)
    {
        _palettes = palettes;
        Title = "Color Cycles"; Width = 560; Height = 410; MinWidth = 460; MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = EditorThemeTokens.AppBackground;
        var root = new DockPanel();
        var bar = BuildBar(AddDefault, () => Close(ReadRows()), () => Close(null)); DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar);
        foreach (var value in values) AddRow(value);
        root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new Border { Padding = new Thickness(10, 2, 10, 10), Child = _rows } });
        Content = root;
    }

    private void AddDefault()
    {
        if (_palettes.Count == 0) return;
        var palette = _palettes[0];
        AddRow(new AnimationColorCyclePresentation(palette.Id, 0, checked((byte)Math.Max(0, palette.Colors.Count - 1)), 1));
    }

    private void AddRow(AnimationColorCyclePresentation value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,64,64,64,30"), ColumnSpacing = 4 };
        var palette = new ComboBox { ItemsSource = _palettes.Select(v => v.Id).ToArray(), SelectedItem = value.PaletteId };
        var max = _palettes.FirstOrDefault(v => v.Id == value.PaletteId)?.Colors.Count - 1 ?? 255;
        var start = Number(value.StartIndex, 0, Math.Max(0, max)); var end = Number(value.EndIndex, 0, Math.Max(0, max)); var offset = Number(value.Offset, -255, 255);
        row.Children.Add(palette); row.Children.Add(Place(start, 1)); row.Children.Add(Place(end, 2)); row.Children.Add(Place(offset, 3));
        row.Children.Add(Place(Icon("×", "Remove", () => _rows.Children.Remove(row)), 4)); _rows.Children.Add(row);
    }

    private IReadOnlyList<AnimationColorCyclePresentation> ReadRows() => _rows.Children.OfType<Grid>().Select(row =>
    {
        var palette = (ComboBox)row.Children[0];
        var id = palette.SelectedItem is PaletteId selected ? selected : _palettes[0].Id;
        return new AnimationColorCyclePresentation(id,
            (byte)(((NumericUpDown)row.Children[1]).Value ?? 0),
            (byte)(((NumericUpDown)row.Children[2]).Value ?? 0),
            (int)(((NumericUpDown)row.Children[3]).Value ?? 0));
    }).ToArray();

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new() { Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0" };
    private static T Place<T>(T c, int column) where T : Control { Grid.SetColumn(c, column); return c; }
    private static Button Icon(string glyph, string tip, Action action) { var b = new Button { Content = glyph }; b.Classes.Add("small-icon"); b.Classes.Add("ghost"); ToolTip.SetTip(b, tip); b.Click += (_, _) => action(); return b; }
    private static Control BuildBar(Action add, Action accept, Action cancel) { var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(10, 8) }; row.Children.Add(Icon("＋", "Add", add)); row.Children.Add(Icon("×", "Cancel", cancel)); var yes = Icon("✓", "Apply", accept); yes.Classes.Add("primary"); row.Children.Add(yes); return row; }
}
