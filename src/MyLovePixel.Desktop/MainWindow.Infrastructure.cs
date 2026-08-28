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
    private Button ActionIcon(ActionId id, string glyph, string tip, bool primary = false)
    {
        var b = IconButton(glyph, tip, async () => await InvokeActionAsync(id));
        if (primary) b.Classes.Add("primary");
        RegisterActionControl(id, b); return b;
    }

    private void RegisterActionControl(ActionId id, Control control)
    {
        if (!_actionControls.TryGetValue(id, out var values)) { values = []; _actionControls.Add(id, values); }
        values.Add(control);
    }

    private Button SelectionModeButton(string glyph, string tip, SelectionGestureMode mode)
    {
        var b = IconButton(glyph, tip, () => { _selectionGesture = mode; _selectionStart = null; _selectionVertices.Clear(); RefreshToolOptions(); });
        if (_selectionGesture == mode) b.Classes.Add("selected");
        return b;
    }

    private Button ToggleFlag(string glyph, TileCellFlags flag) => ToggleIcon(glyph, flag.ToString(), () => (_tileFlags & flag) != 0, value => { if (value) _tileFlags |= flag; else _tileFlags &= ~flag; RefreshTiles(); });

    private Button SwatchButton(Border swatch, string tip, bool primary)
    {
        var display = Swatch();
        display.Background = swatch.Background;
        var b = new Button { Content = display, Width = 34, Height = 34, Padding = new Thickness(3) };
        ToolTip.SetTip(b, tip); b.Click += async (_, _) => await EditColorAsync(primary); return b;
    }

    private static Button IconButton(string glyph, string tip, Action action)
    {
        var b = new Button { Content = glyph }; b.Classes.Add("icon"); b.Classes.Add("ghost"); ToolTip.SetTip(b, tip); b.Click += (_, _) => action(); return b;
    }

    private static Button IconButton(string glyph, string tip, Func<Task> action)
    {
        var b = new Button { Content = glyph }; b.Classes.Add("icon"); b.Classes.Add("ghost"); ToolTip.SetTip(b, tip); b.Click += async (_, _) => await action(); return b;
    }

    private static Button SmallIcon(string glyph, string tip, Action action)
    {
        var b = new Button { Content = glyph }; b.Classes.Add("small-icon"); b.Classes.Add("ghost"); ToolTip.SetTip(b, tip); b.Click += (_, _) => action(); return b;
    }

    private static Button ToggleIcon(string glyph, string tip, Func<bool> get, Action<bool> set)
    {
        var b = IconButton(glyph, tip, () => { set(!get()); });
        if (get()) b.Classes.Add("selected"); return b;
    }

    private static StackPanel Icons(params Control[] controls)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var control in controls) row.Children.Add(control); return row;
    }

    private static Control Labeled(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("72,*"), ColumnSpacing = 6 };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }; text.Classes.Add("muted"); grid.Children.Add(text); Grid.SetColumn(control, 1); grid.Children.Add(control); return grid;
    }

    private static Control ListRow(string text, Control action)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4 };
        grid.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }); Grid.SetColumn(action, 1); grid.Children.Add(action); return grid;
    }

    private static Expander Expander(string header, Control content) => new() { Header = header, Content = content, IsExpanded = false };
    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new() { Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0", MinWidth = 54 };
    private static Border Swatch() => new() { Width = 24, Height = 24, CornerRadius = new CornerRadius(3), BorderBrush = EditorThemeTokens.StrongBorder, BorderThickness = new Thickness(1) };
    private static IBrush Brush(Rgba32 c) => new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
    private static Separator SeparatorH() => new() { Margin = new Thickness(3, 4) };
    private static Border SeparatorV() => new() { Width = 1, Margin = new Thickness(3, 5), Background = EditorThemeTokens.PanelBorder };
    private static TextBlock ErrorText(string text) { var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }; t.Foreground = EditorThemeTokens.Danger; return t; }
    private static T Place<T>(T control, int column) where T : Control { Grid.SetColumn(control, column); return control; }

    private static string ToolGlyph(string id) => id switch
    {
        "core.pencil" => "✎",
        "core.eraser" => "⌫",
        "core.line" => "╱",
        "core.shape" => "□",
        "core.fill" => "▣",
        _ => "◆",
    };

    private static string ShortOption(string value) => value switch
    {
        "Brush Size" => "Size",
        "Spacing" => "Gap",
        "Pixel Perfect" => "Perfect",
        "Tolerance" => "Tolerance",
        _ => value,
    };

    private void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex) { SetError(ex.Message); }
    }

    private void Safe(Func<object?> action)
    {
        try { _ = action(); }
        catch (Exception ex) { SetError(ex.Message); }
    }

    private void SetError(string text) { _status.Text = text; _status.Foreground = EditorThemeTokens.Danger; }

    private static string GetRecoveryRootDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = AppContext.BaseDirectory;
        return Path.Combine(root, "MyLovePixel", "Recovery");
    }
}
