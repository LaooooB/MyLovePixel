using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace MyLovePixel.Desktop;

internal enum CloseDocumentDecision { Cancel, Save, Discard }

internal sealed class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog(string name)
    {
        Title = "Unsaved changes";
        Width = 450;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.Surface;
        AutomationProperties.SetAutomationId(this, "dialog.unsaved");
        var body = new StackPanel { Spacing = 16, Margin = new Thickness(20) };
        body.Children.Add(new TextBlock { Text = "Save changes to " + name + "?", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.Add(Choice("Cancel", CloseDocumentDecision.Cancel, "dialog.cancel"));
        row.Children.Add(Choice("Discard", CloseDocumentDecision.Discard, "dialog.discard"));
        row.Children.Add(Choice("Save", CloseDocumentDecision.Save, "dialog.save"));
        body.Children.Add(row);
        Content = body;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(CloseDocumentDecision.Cancel); e.Handled = true; } };
    }

    private Button Choice(string text, CloseDocumentDecision choice, string id)
    {
        var button = new Button { Content = text, MinWidth = 84, Margin = new Thickness(5, 0, 0, 0), IsDefault = choice == CloseDocumentDecision.Save, IsCancel = choice == CloseDocumentDecision.Cancel };
        AutomationProperties.SetAutomationId(button, id);
        AutomationProperties.SetName(button, text);
        if (choice == CloseDocumentDecision.Save) button.Classes.Add("primary");
        button.Click += (_, _) => Close(choice);
        return button;
    }
}

internal sealed class RenameItemDialog : Window
{
    public RenameItemDialog(string title, string name)
    {
        Title = title;
        Width = 400;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.Surface;
        var input = new TextBox { Text = name };
        var error = new TextBlock { IsVisible = false, Foreground = EditorThemeTokens.Danger, Text = "Enter a name." };
        AutomationProperties.SetName(input, "Name");
        void Accept()
        {
            if (string.IsNullOrWhiteSpace(input.Text)) { error.IsVisible = true; input.Focus(); return; }
            Close(input.Text.Trim());
        }
        var body = new StackPanel { Spacing = 10, Margin = new Thickness(18) };
        body.Children.Add(input);
        body.Children.Add(error);
        body.Children.Add(DialogChrome.ConfirmCancel(() => Close(null), Accept, "Rename"));
        Content = body;
        Opened += (_, _) => Dispatcher.UIThread.Post(() => { input.Focus(); input.SelectAll(); });
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(null); e.Handled = true; } };
    }
}
