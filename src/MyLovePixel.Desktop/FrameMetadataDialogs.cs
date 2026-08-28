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
        Width = 600;
        Height = 470;
        MinWidth = 500;
        MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        var root = new DockPanel();
        var footer = DialogChrome.ConfirmCancel(() => Close(null), () => Close(ReadRows()), "Apply");
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(new Border { Padding = new Thickness(12), Child = footer });

        var body = new StackPanel { Spacing = 8, Margin = new Thickness(14) };
        body.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        body.Children.Add(DialogChrome.Help("Each box has a name, X/Y origin, width and height for the current frame."));
        body.Children.Add(BoxHeader());
        foreach (var value in values) AddRow(value);
        body.Children.Add(_rows);
        body.Children.Add(DialogChrome.TextButton("Add Box", () => AddRow(new AnimationBoxPresentation($"box{_rows.Children.Count + 1}", 0, 0, 1, 1))));
        root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body });
        Content = root;
    }

    private void AddRow(AnimationBoxPresentation value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,60,60,60,60,34"), ColumnSpacing = 5 };
        var name = new TextBox { Text = value.Name, PlaceholderText = "name" };
        var x = Number(value.X, -8192, 8192);
        var y = Number(value.Y, -8192, 8192);
        var w = Number(value.Width, 1, 8192);
        var h = Number(value.Height, 1, 8192);
        row.Children.Add(name);
        row.Children.Add(Place(x, 1));
        row.Children.Add(Place(y, 2));
        row.Children.Add(Place(w, 3));
        row.Children.Add(Place(h, 4));
        row.Children.Add(Place(DialogChrome.IconButton("×", "Remove box", () => _rows.Children.Remove(row)), 5));
        _rows.Children.Add(row);
    }

    private IReadOnlyList<AnimationBoxPresentation> ReadRows() => _rows.Children.OfType<Grid>().Select(row =>
    {
        var name = (TextBox)row.Children[0];
        var x = (NumericUpDown)row.Children[1];
        var y = (NumericUpDown)row.Children[2];
        var w = (NumericUpDown)row.Children[3];
        var h = (NumericUpDown)row.Children[4];
        return new AnimationBoxPresentation(
            string.IsNullOrWhiteSpace(name.Text) ? "box" : name.Text!.Trim(),
            (int)(x.Value ?? 0),
            (int)(y.Value ?? 0),
            (int)(w.Value ?? 1),
            (int)(h.Value ?? 1));
    }).ToArray();

    private static Grid BoxHeader()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,60,60,60,60,34"), ColumnSpacing = 5 };
        row.Children.Add(Header("Name"));
        row.Children.Add(Place(Header("X"), 1));
        row.Children.Add(Place(Header("Y"), 2));
        row.Children.Add(Place(Header("W"), 3));
        row.Children.Add(Place(Header("H"), 4));
        return row;
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new()
    {
        Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0"
    };
    private static TextBlock Header(string text) { var t = new TextBlock { Text = text }; t.Classes.Add("subtle"); return t; }
    private static T Place<T>(T control, int column) where T : Control { Grid.SetColumn(control, column); return control; }
}

public sealed class AnimationSocketsDialog : Window
{
    private readonly StackPanel _rows = new() { Spacing = 6 };

    public AnimationSocketsDialog(IReadOnlyList<AnimationSocketPresentation> values)
    {
        Title = "Sockets";
        Width = 520;
        Height = 430;
        MinWidth = 420;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        var root = new DockPanel();
        var footer = DialogChrome.ConfirmCancel(() => Close(null), () => Close(ReadRows()), "Apply");
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(new Border { Padding = new Thickness(12), Child = footer });

        var body = new StackPanel { Spacing = 8, Margin = new Thickness(14) };
        body.Children.Add(new TextBlock { Text = "Sockets", FontSize = 15, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        body.Children.Add(DialogChrome.Help("Sockets are named attachment points on the current frame."));
        body.Children.Add(SocketHeader());
        foreach (var value in values) AddRow(value);
        body.Children.Add(_rows);
        body.Children.Add(DialogChrome.TextButton("Add Socket", () => AddRow(new AnimationSocketPresentation($"socket{_rows.Children.Count + 1}", 0, 0))));
        root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body });
        Content = root;
    }

    private void AddRow(AnimationSocketPresentation value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,72,72,34"), ColumnSpacing = 5 };
        var name = new TextBox { Text = value.Name, PlaceholderText = "name" };
        var x = Number(value.X);
        var y = Number(value.Y);
        row.Children.Add(name);
        row.Children.Add(Place(x, 1));
        row.Children.Add(Place(y, 2));
        row.Children.Add(Place(DialogChrome.IconButton("×", "Remove socket", () => _rows.Children.Remove(row)), 3));
        _rows.Children.Add(row);
    }

    private IReadOnlyList<AnimationSocketPresentation> ReadRows() => _rows.Children.OfType<Grid>()
        .Select(row => new AnimationSocketPresentation(
            string.IsNullOrWhiteSpace(((TextBox)row.Children[0]).Text) ? "socket" : ((TextBox)row.Children[0]).Text!.Trim(),
            (int)(((NumericUpDown)row.Children[1]).Value ?? 0),
            (int)(((NumericUpDown)row.Children[2]).Value ?? 0)))
        .ToArray();

    private static Grid SocketHeader()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,72,72,34"), ColumnSpacing = 5 };
        row.Children.Add(Header("Name"));
        row.Children.Add(Place(Header("X"), 1));
        row.Children.Add(Place(Header("Y"), 2));
        return row;
    }

    private static NumericUpDown Number(decimal value) => new() { Value = value, Minimum = -8192, Maximum = 8192, Increment = 1, FormatString = "0" };
    private static TextBlock Header(string text) { var t = new TextBlock { Text = text }; t.Classes.Add("subtle"); return t; }
    private static T Place<T>(T c, int column) where T : Control { Grid.SetColumn(c, column); return c; }
}

public sealed class AnimationEventsDialog : Window
{
    private readonly StackPanel _rows = new() { Spacing = 6 };

    public AnimationEventsDialog(IReadOnlyList<AnimationEventPresentation> values)
    {
        Title = "Events";
        Width = 600;
        Height = 440;
        MinWidth = 480;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        var root = new DockPanel();
        var footer = DialogChrome.ConfirmCancel(() => Close(null), () => Close(ReadRows()), "Apply");
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(new Border { Padding = new Thickness(12), Child = footer });

        var body = new StackPanel { Spacing = 8, Margin = new Thickness(14) };
        body.Children.Add(new TextBlock { Text = "Frame events", FontSize = 15, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        body.Children.Add(DialogChrome.Help("Add named events and optional payload text for the current animation frame."));
        body.Children.Add(EventHeader());
        foreach (var value in values) AddRow(value);
        body.Children.Add(_rows);
        body.Children.Add(DialogChrome.TextButton("Add Event", () => AddRow(new AnimationEventPresentation("event", string.Empty))));
        root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body });
        Content = root;
    }

    private void AddRow(AnimationEventPresentation value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*,34"), ColumnSpacing = 5 };
        var name = new TextBox { Text = value.Name, PlaceholderText = "event name" };
        var payload = new TextBox { Text = value.Payload, PlaceholderText = "optional payload" };
        row.Children.Add(name);
        row.Children.Add(Place(payload, 1));
        row.Children.Add(Place(DialogChrome.IconButton("×", "Remove event", () => _rows.Children.Remove(row)), 2));
        _rows.Children.Add(row);
    }

    private IReadOnlyList<AnimationEventPresentation> ReadRows() => _rows.Children.OfType<Grid>()
        .Select(row => new AnimationEventPresentation(
            ((TextBox)row.Children[0]).Text?.Trim() ?? string.Empty,
            ((TextBox)row.Children[1]).Text ?? string.Empty))
        .Where(value => !string.IsNullOrWhiteSpace(value.Name))
        .ToArray();

    private static Grid EventHeader()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*,34"), ColumnSpacing = 5 };
        row.Children.Add(Header("Event name"));
        row.Children.Add(Place(Header("Payload"), 1));
        return row;
    }

    private static TextBlock Header(string text) { var t = new TextBlock { Text = text }; t.Classes.Add("subtle"); return t; }
    private static T Place<T>(T c, int column) where T : Control { Grid.SetColumn(c, column); return c; }
}

public sealed class AnimationCyclesDialog : Window
{
    private readonly StackPanel _rows = new() { Spacing = 6 };
    private readonly IReadOnlyList<PaletteEditorPresentation> _palettes;

    public AnimationCyclesDialog(
        IReadOnlyList<PaletteEditorPresentation> palettes,
        IReadOnlyList<AnimationColorCyclePresentation> values)
    {
        _palettes = palettes;
        Title = "Color Cycles";
        Width = 640;
        Height = 470;
        MinWidth = 520;
        MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        var root = new DockPanel();
        var footer = DialogChrome.ConfirmCancel(() => Close(null), () => Close(ReadRows()), "Apply");
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(new Border { Padding = new Thickness(12), Child = footer });

        var body = new StackPanel { Spacing = 8, Margin = new Thickness(14) };
        body.Children.Add(new TextBlock { Text = "Palette color cycles", FontSize = 15, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        body.Children.Add(DialogChrome.Help("Define palette ranges that shift by an index offset on the current frame."));
        body.Children.Add(CycleHeader());
        foreach (var value in values) AddRow(value);
        body.Children.Add(_rows);
        var add = DialogChrome.TextButton("Add Color Cycle", AddDefault);
        add.IsEnabled = _palettes.Count > 0;
        body.Children.Add(add);
        root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body });
        Content = root;
    }

    private void AddDefault()
    {
        if (_palettes.Count == 0) return;
        var palette = _palettes[0];
        AddRow(new AnimationColorCyclePresentation(
            palette.Id,
            0,
            checked((byte)Math.Max(0, palette.Colors.Count - 1)),
            1));
    }

    private void AddRow(AnimationColorCyclePresentation value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,72,72,72,34"), ColumnSpacing = 5 };
        var palette = new ComboBox { ItemsSource = _palettes.Select(v => v.Id).ToArray(), SelectedItem = value.PaletteId };
        var max = _palettes.FirstOrDefault(v => v.Id == value.PaletteId)?.Colors.Count - 1 ?? 255;
        var start = Number(value.StartIndex, 0, Math.Max(0, max));
        var end = Number(value.EndIndex, 0, Math.Max(0, max));
        var offset = Number(value.Offset, -255, 255);
        row.Children.Add(palette);
        row.Children.Add(Place(start, 1));
        row.Children.Add(Place(end, 2));
        row.Children.Add(Place(offset, 3));
        row.Children.Add(Place(DialogChrome.IconButton("×", "Remove color cycle", () => _rows.Children.Remove(row)), 4));
        _rows.Children.Add(row);
    }

    private IReadOnlyList<AnimationColorCyclePresentation> ReadRows()
    {
        if (_palettes.Count == 0) return [];
        return _rows.Children.OfType<Grid>().Select(row =>
        {
            var palette = (ComboBox)row.Children[0];
            var id = palette.SelectedItem is PaletteId selected ? selected : _palettes[0].Id;
            return new AnimationColorCyclePresentation(
                id,
                (byte)(((NumericUpDown)row.Children[1]).Value ?? 0),
                (byte)(((NumericUpDown)row.Children[2]).Value ?? 0),
                (int)(((NumericUpDown)row.Children[3]).Value ?? 0));
        }).ToArray();
    }

    private static Grid CycleHeader()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,72,72,72,34"), ColumnSpacing = 5 };
        row.Children.Add(Header("Palette"));
        row.Children.Add(Place(Header("Start"), 1));
        row.Children.Add(Place(Header("End"), 2));
        row.Children.Add(Place(Header("Offset"), 3));
        return row;
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new()
    {
        Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0"
    };
    private static TextBlock Header(string text) { var t = new TextBlock { Text = text }; t.Classes.Add("subtle"); return t; }
    private static T Place<T>(T c, int column) where T : Control { Grid.SetColumn(c, column); return c; }
}
