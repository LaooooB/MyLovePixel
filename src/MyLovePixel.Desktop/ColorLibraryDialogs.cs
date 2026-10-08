using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

internal sealed record ColorFolderChoice(string? Id, string Name, bool UnfiledOnly = false)
{
    public override string ToString() => Name.Length > 27 ? Name[..24] + "…" : Name;
}
internal sealed record ColorLibraryEdit(string Name, string? FolderId);

internal sealed class ColorLibraryNameDialog : Window
{
    public ColorLibraryNameDialog(string title, string initial)
    {
        Title = title; Width = 360; SizeToContent = SizeToContent.Height;
        CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;
        var text = new TextBox { Text = initial, MaxLength = UserPaletteStore.MaxNameLength };
        AutomationProperties.SetAutomationId(text, "folder-name-input");
        AutomationProperties.SetName(text, "Folder name");
        var error = DialogChrome.Help("Choose a short, descriptive folder name.");
        void Accept()
        {
            if (string.IsNullOrWhiteSpace(text.Text)) { error.Text = "Enter a folder name."; return; }
            Close(text.Text.Trim());
        }
        var root = new StackPanel { Margin = new Thickness(16), Spacing = 10 };
        root.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeight.SemiBold });
        root.Children.Add(text); root.Children.Add(error);
        root.Children.Add(DialogChrome.ConfirmCancel(() => Close(null), Accept, "Save"));
        Content = root;
        Opened += (_, _) => { text.Focus(); text.SelectAll(); };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(null); e.Handled = true; }
            else if (e.Key == Key.Enter) { Accept(); e.Handled = true; }
        };
    }
}

internal sealed class ColorLibraryEditDialog : Window
{
    public ColorLibraryEditDialog(UserPaletteColor color, IReadOnlyList<UserPaletteFolder> folders)
    {
        Title = "Edit saved color"; Width = 360; SizeToContent = SizeToContent.Height;
        CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;
        var name = new TextBox { Text = color.Name, PlaceholderText = "Optional name", MaxLength = UserPaletteStore.MaxNameLength };
        AutomationProperties.SetAutomationId(name, "saved-color-name");
        var choices = new[] { new ColorFolderChoice(null, "Unfiled") }
            .Concat(folders.Select(folder => new ColorFolderChoice(folder.Id, folder.Name))).ToArray();
        var folder = new ComboBox { ItemsSource = choices, SelectedItem = choices.FirstOrDefault(item => item.Id == color.FolderId) ?? choices[0], HorizontalAlignment = HorizontalAlignment.Stretch };
        var root = new StackPanel { Margin = new Thickness(16), Spacing = 10 };
        root.Children.Add(new TextBlock { Text = HexColor.Format(color.Color), FontSize = 16, FontWeight = FontWeight.SemiBold });
        root.Children.Add(DialogChrome.Labeled("Name", name, 50));
        root.Children.Add(DialogChrome.Labeled("Folder", folder, 50));
        root.Children.Add(DialogChrome.Help("Changes rename or move this saved color. Artwork stays unchanged."));
        root.Children.Add(DialogChrome.ConfirmCancel(() => Close(null), () => Close(new ColorLibraryEdit(name.Text ?? "", (folder.SelectedItem as ColorFolderChoice)?.Id)), "Save"));
        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(null); e.Handled = true; } };
    }
}

internal sealed class ColorLibraryConfirmDialog : Window
{
    public ColorLibraryConfirmDialog(string title, string message, string action)
    {
        Title = title; Width = 370; SizeToContent = SizeToContent.Height;
        CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;
        var root = new StackPanel { Margin = new Thickness(16), Spacing = 10 };
        root.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeight.SemiBold });
        root.Children.Add(DialogChrome.Help(message));
        root.Children.Add(DialogChrome.ConfirmCancel(() => Close(false), () => Close(true), action));
        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(false); e.Handled = true; } };
    }
}
