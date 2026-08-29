using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace MyLovePixel.Desktop;

internal static class DialogChrome
{
    public static Button TextButton(string label, Action action, bool primary = false)
    {
        var button = new Button { Content = label, MinWidth = 76 };
        if (primary) button.Classes.Add("primary");
        button.Click += (_, _) => action();
        return button;
    }

    public static Button IconButton(string legacyGlyph, string tip, Action action)
    {
        var button = new Button();
        if (UiIconSemantics.TryCreate(tip, legacyGlyph, 15, out var semanticIcon))
        {
            button.Content = semanticIcon;
            button.Classes.Add("small-icon");
        }
        else if (UiIcons.TryResolve(tip, legacyGlyph, out var kind))
        {
            button.Content = UiIcons.Create(kind, 15);
            button.Classes.Add("small-icon");
        }
        else
        {
            button.Content = UiIcons.TextFallback(tip);
            button.Classes.Add("small-text-action");
        }
        button.Classes.Add("ghost");
        ToolTip.SetTip(button, tip);
        button.Click += (_, _) => action();
        return button;
    }

    public static Control ConfirmCancel(Action cancel, Action accept, string acceptLabel = "Apply")
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 6, 0, 0),
        };
        row.Children.Add(TextButton("Cancel", cancel));
        row.Children.Add(TextButton(acceptLabel, accept, primary: true));
        return row;
    }

    public static Control Labeled(string label, Control control, double labelWidth = 96)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions($"{labelWidth},*"), ColumnSpacing = 8 };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        text.Classes.Add("muted");
        grid.Children.Add(text);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }

    public static TextBlock Help(string text)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        block.Classes.Add("subtle");
        return block;
    }
}
