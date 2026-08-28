using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed class PluginPanelView : ScrollViewer
{
    private readonly StackPanel _content = new() { Spacing = EditorThemeTokens.PanelSpacing };

    public PluginPanelView()
    {
        Content = _content;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
    }

    public void SetPanels(
        IReadOnlyList<PluginPanelPresentation> panels,
        Func<string, string, PluginPanelActionResult>? invoke = null)
    {
        ArgumentNullException.ThrowIfNull(panels);
        _content.Children.Clear();
        foreach (var panel in panels)
        {
            var group = new StackPanel { Spacing = EditorThemeTokens.CompactSpacing };
            group.Children.Add(new TextBlock { Text = panel.Title, FontWeight = Avalonia.Media.FontWeight.SemiBold });
            foreach (var section in panel.Sections)
            {
                group.Children.Add(new TextBlock { Text = section.Title });
                foreach (var field in section.Fields)
                {
                    var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
                    row.Children.Add(new TextBlock { Text = field.Label, Margin = new Avalonia.Thickness(0, 0, 8, 0) });
                    var value = new TextBlock { Text = field.Value };
                    Grid.SetColumn(value, 1);
                    row.Children.Add(value);
                    group.Children.Add(row);
                }
                foreach (var action in section.Actions)
                {
                    var button = new Button
                    {
                        Content = action.Label,
                        IsEnabled = action.Enabled && invoke is not null,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                    };
                    if (invoke is not null)
                    {
                        var capturedPanel = panel.Id;
                        var capturedAction = action.Id;
                        button.Click += (_, _) => invoke(capturedPanel, capturedAction);
                    }
                    group.Children.Add(button);
                }
            }
            _content.Children.Add(group);
        }
    }
}
