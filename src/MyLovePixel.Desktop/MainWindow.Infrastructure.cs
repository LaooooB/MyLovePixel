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
        RegisterActionControl(id, b);
        return b;
    }

    private Button ActionTextButton(ActionId id, string glyph, string label, string tip, bool primary = false)
    {
        var b = TextIconButton(glyph, label, tip, async () => await InvokeActionAsync(id));
        if (primary) b.Classes.Add("primary");
        RegisterActionControl(id, b);
        return b;
    }

    private void RegisterActionControl(ActionId id, Control control)
    {
        if (!_actionControls.TryGetValue(id, out var values))
        {
            values = [];
            _actionControls.Add(id, values);
        }
        values.Add(control);
    }

    private Button SelectionModeButton(string glyph, string tip, SelectionGestureMode mode)
    {
        var b = IconButton(glyph, tip, () =>
        {
            _selectionGesture = mode;
            _selectionStart = null;
            _selectionVertices.Clear();
            RefreshToolOptions();
        });
        if (_selectionGesture == mode) b.Classes.Add("selected");
        return b;
    }

    private Button ToggleFlag(string glyph, TileCellFlags flag) =>
        ToggleIcon(glyph, flag.ToString(), () => (_tileFlags & flag) != 0, value =>
        {
            if (value) _tileFlags |= flag;
            else _tileFlags &= ~flag;
            RefreshTiles();
        });

    private Button SwatchButton(Border swatch, string tip, bool primary)
    {
        var display = Swatch();
        display.Background = swatch.Background;
        var b = new Button { Content = display, Width = 38, Height = 38, Padding = new Thickness(4) };
        ToolTip.SetTip(b, $"{tip} · click to make this the active palette target");
        b.Click += (_, _) => SetStudioColorTarget(secondary: !primary);
        if ((primary && !_studioSecondaryTarget) || (!primary && _studioSecondaryTarget)) b.Classes.Add("selected");
        return b;
    }

    private static Button IconButton(string glyph, string tip, Action action)
    {
        var b = BuildCompactButton(glyph, tip);
        b.Click += (_, _) => action();
        return b;
    }

    private static Button IconButton(string glyph, string tip, Func<Task> action)
    {
        var b = BuildCompactButton(glyph, tip);
        b.Click += async (_, _) => await action();
        return b;
    }

    private static Button SmallIcon(string glyph, string tip, Action action)
    {
        var b = new Button();
        if (UiIcons.TryResolve(tip, glyph, out var kind))
        {
            b.Content = UiIcons.Create(kind, 15);
            b.Classes.Add("small-icon");
        }
        else
        {
            b.Content = UiIcons.TextFallback(tip);
            b.Classes.Add("small-text-action");
        }
        b.Classes.Add("ghost");
        ToolTip.SetTip(b, tip);
        b.Click += (_, _) => action();
        return b;
    }

    private static Button TextIconButton(string glyph, string label, string tip, Action action)
    {
        var b = BuildTextIconButton(glyph, label, tip);
        b.Click += (_, _) => action();
        return b;
    }

    private static Button TextIconButton(string glyph, string label, string tip, Func<Task> action)
    {
        var b = BuildTextIconButton(glyph, label, tip);
        b.Click += async (_, _) => await action();
        return b;
    }

    private static Button BuildCompactButton(string glyph, string tip)
    {
        var b = new Button();
        if (UiIcons.TryResolve(tip, glyph, out var kind))
        {
            b.Content = UiIcons.Create(kind, 18);
            b.Classes.Add("icon");
        }
        else
        {
            b.Content = UiIcons.TextFallback(tip);
            b.Classes.Add("text-action");
        }
        b.Classes.Add("ghost");
        ToolTip.SetTip(b, tip);
        return b;
    }

    private static Button BuildTextIconButton(string glyph, string label, string tip)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (UiIcons.TryResolve(tip, glyph, out var kind)) row.Children.Add(UiIcons.Create(kind, 16));
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        var b = new Button { Content = row };
        b.Classes.Add("text-icon");
        ToolTip.SetTip(b, tip);
        return b;
    }

    private static Button ToggleIcon(string glyph, string tip, Func<bool> get, Action<bool> set)
    {
        var b = IconButton(glyph, tip, () => set(!get()));
        if (get()) b.Classes.Add("selected");
        return b;
    }

    private static Button ToggleTextButton(string glyph, string label, string tip, Func<bool> get, Action<bool> set)
    {
        var b = TextIconButton(glyph, label, tip, () => set(!get()));
        if (get()) b.Classes.Add("selected");
        return b;
    }

    private static StackPanel Icons(params Control[] controls)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var control in controls) row.Children.Add(control);
        return row;
    }

    private static Control Labeled(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("96,*"), ColumnSpacing = 8 };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        text.Classes.Add("muted");
        grid.Children.Add(text);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }

    private static Control ListRow(string text, Control action)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 };
        grid.Children.Add(new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(action, 1);
        grid.Children.Add(action);
        return grid;
    }

    private static Border SectionCard(string title, string? description, Control content)
    {
        var body = new StackPanel { Spacing = 8 };
        var heading = new TextBlock { Text = title };
        heading.Classes.Add("section-title");
        body.Children.Add(heading);
        if (!string.IsNullOrWhiteSpace(description))
        {
            var detail = new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap };
            detail.Classes.Add("subtle");
            body.Children.Add(detail);
        }
        body.Children.Add(content);
        var border = new Border
        {
            Child = body,
            Padding = new Thickness(10),
            CornerRadius = EditorThemeTokens.CardRadius,
            Background = EditorThemeTokens.SurfaceRaised,
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(1),
        };
        border.Classes.Add("section-card");
        return border;
    }

    private static TabItem TextTab(string title, Control content) => new()
    {
        Header = new TextBlock { Text = title },
        Content = content,
    };

    private static Expander Expander(string header, Control content) => new() { Header = header, Content = content, IsExpanded = false };
    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new() { Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0", MinWidth = 54 };
    private static Border Swatch() => new() { Width = 24, Height = 24, CornerRadius = new CornerRadius(3), BorderBrush = EditorThemeTokens.StrongBorder, BorderThickness = new Thickness(1) };
    private static IBrush Brush(Rgba32 c) => new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
    private static Separator SeparatorH() => new() { Margin = new Thickness(3, 5) };
    private static Border SeparatorV() => new() { Width = 1, Margin = new Thickness(5, 4), Background = EditorThemeTokens.PanelBorder };
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

    private void SetError(string text)
    {
        _status.Text = text;
        _status.Foreground = EditorThemeTokens.Danger;
    }

    private static string GetRecoveryRootDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = AppContext.BaseDirectory;
        return Path.Combine(root, "MyLovePixel", "Recovery");
    }
}
