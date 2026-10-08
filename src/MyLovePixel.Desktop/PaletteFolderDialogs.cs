using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

internal sealed class PaletteNameDialog : Window
{
    public PaletteNameDialog(string title, string name, Action<string> save)
    {
        Title = title; Width = 360; CanResize = false; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = EditorThemeTokens.Surface;
        AutomationProperties.SetAutomationId(this, "folder.dialog");
        var input = new TextBox { Text = name, PlaceholderText = "Folder name", MaxLength = UserPaletteStore.MaxNameLength };
        AutomationProperties.SetAutomationId(input, "folder.name"); AutomationProperties.SetName(input, "Folder name");
        var error = new TextBlock { IsVisible = false, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = EditorThemeTokens.Danger };
        var body = new StackPanel { Spacing = 10, Margin = new Thickness(16) };
        body.Children.Add(input); body.Children.Add(error);
        var accept = DialogChrome.TextButton("Save", () =>
        {
            try { save(input.Text ?? string.Empty); Close(true); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
            { error.Text = ex.Message; error.IsVisible = true; input.Focus(); }
        }, true);
        AutomationProperties.SetAutomationId(accept, "folder.save");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.Add(DialogChrome.TextButton("Cancel", () => Close(false))); row.Children.Add(accept);
        body.Children.Add(row); Content = body;
        Opened += (_, _) => { input.Focus(); input.SelectAll(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(false); e.Handled = true; } };
    }
}

internal sealed class ColorFolderDialog : Window
{
    private sealed record Choice(string? Id, string Name) { public override string ToString() => Name; }
    public ColorFolderDialog(IReadOnlyList<ColorFolder> folders, string? current, Action<string?> save)
    {
        Title = "Move color"; Width = 360; CanResize = false; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = EditorThemeTokens.Surface;
        var choices = new[] { new Choice(null, "Unfiled") }.Concat(folders.Select(f => new Choice(f.Id, f.Name))).ToArray();
        var select = new ComboBox { ItemsSource = choices, SelectedItem = choices.FirstOrDefault(f => f.Id == current), HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetAutomationId(select, "folder.move.target"); AutomationProperties.SetName(select, "Destination folder");
        var error = new TextBlock { IsVisible = false, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = EditorThemeTokens.Danger };
        var body = new StackPanel { Spacing = 10, Margin = new Thickness(16) }; body.Children.Add(select); body.Children.Add(error);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.Add(DialogChrome.TextButton("Cancel", () => Close(false)));
        var accept = DialogChrome.TextButton("Move", () =>
        {
            try { save((select.SelectedItem as Choice)?.Id); Close(true); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
            { error.Text = ex.Message; error.IsVisible = true; }
        }, true);
        AutomationProperties.SetAutomationId(accept, "folder.move.save"); row.Children.Add(accept); body.Children.Add(row); Content = body;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(false); e.Handled = true; } };
    }
}
