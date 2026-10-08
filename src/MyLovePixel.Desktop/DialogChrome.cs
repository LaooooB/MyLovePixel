using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace MyLovePixel.Desktop;

internal static class DialogChrome
{
    public static Button TextButton(string label, Action action, bool primary = false)
    {
        var button = new Button { Content = label, MinWidth = 76, IsDefault = primary, IsCancel = label == "Cancel" };
        AutomationProperties.SetName(button, label);
        if (primary) button.Classes.Add("primary");
        button.Click += (_, _) => action(); return button;
    }
    public static Button IconButton(string glyph, string tip, Action action)
    {
        var button = TextButton(tip, action); button.MinWidth = 48;
        button.Content = new TextBlock { Text = tip, TextWrapping = TextWrapping.Wrap };
        return button;
    }
    public static Control ConfirmCancel(Action cancel, Action accept, string acceptLabel = "Apply")
    {
        var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var cancelButton = TextButton("Cancel", cancel); cancelButton.Margin = new Thickness(0, 0, 8, 0);
        row.Children.Add(cancelButton); row.Children.Add(TextButton(acceptLabel, accept, true)); return row;
    }
    public static Control Labeled(string label, Control control, double labelWidth = 96)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions($"{labelWidth},*"), ColumnSpacing = 8 };
        var text = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Foreground = EditorThemeTokens.TextSecondary };
        AutomationProperties.SetName(control, label); AutomationProperties.SetLabeledBy(control, text);
        grid.Children.Add(text); Grid.SetColumn(control, 1); grid.Children.Add(control); return grid;
    }
    public static TextBlock Help(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = EditorThemeTokens.TextSecondary };
    public static void SetContent(Window window, Panel fields)
    {
        var layout = new DockPanel();
        if (fields.Children.LastOrDefault() is Panel footer && footer.Children.OfType<Button>().Any(b => b.IsCancel))
        {
            fields.Children.Remove(footer); footer.Margin = new Thickness(16, 10, 16, 14);
            DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
        }
        fields.Margin = new Thickness(16, 12);
        layout.Children.Add(new ScrollViewer { Content = fields, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        window.Content = layout; window.SizeToContent = SizeToContent.Height; window.MaxHeight = 550; window.MinHeight = 180;
        window.Opened += (_, _) =>
        {
            var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
            if (screen is not null) window.MaxHeight = Math.Max(180, Math.Min(550, screen.WorkingArea.Height / screen.Scaling - 48));
        };
    }
}
