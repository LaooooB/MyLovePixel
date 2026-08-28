using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed class PluginPanelView : ScrollViewer
{
    private readonly StackPanel _content = new() { Spacing = 8, Margin = new Thickness(8) };

    public PluginPanelView()
    {
        Background = EditorThemeTokens.Surface;
        Content = _content;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
    }

    public void SetPanels(IReadOnlyList<PluginPanelPresentation> panels, Func<string, string, PluginPanelActionResult>? invoke = null)
    {
        _content.Children.Clear();
        foreach (var panel in panels)
        {
            foreach (var section in panel.Sections)
            {
                foreach (var field in section.Fields)
                {
                    var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
                    var label = new TextBlock { Text = field.Label, VerticalAlignment = VerticalAlignment.Center };
                    label.Classes.Add("subtle");
                    row.Children.Add(label);
                    var value = new TextBlock { Text = field.Value, TextWrapping = TextWrapping.Wrap };
                    Grid.SetColumn(value, 1);
                    row.Children.Add(value);
                    _content.Children.Add(row);
                }

                foreach (var action in section.Actions)
                {
                    var button = new Button { Content = action.Label, IsEnabled = action.Enabled && invoke is not null };
                    if (invoke is not null)
                    {
                        var panelId = panel.Id;
                        var actionId = action.Id;
                        button.Click += (_, _) => invoke(panelId, actionId);
                    }
                    _content.Children.Add(button);
                }
            }
        }
    }
}
