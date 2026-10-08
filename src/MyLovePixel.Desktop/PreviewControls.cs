using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace MyLovePixel.Desktop;

internal static class PreviewControls
{
    public static Control Build(PixelPreviewView view, string prefix)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 3 };
        var outButton = Button("− Out", prefix + ".zoom.out", "Zoom out", () => view.ZoomBy(.5));
        var actual = Button("100%", prefix + ".zoom.actual", "Actual size · 1", view.ActualSize);
        actual.HorizontalAlignment = HorizontalAlignment.Stretch;
        var inButton = Button("+ In", prefix + ".zoom.in", "Zoom in", () => view.ZoomBy(2));
        var fit = Button("Fit", prefix + ".zoom.fit", "Fit whole image · F", view.Fit);
        Add(row, outButton, 0); Add(row, actual, 1); Add(row, inButton, 2); Add(row, fit, 3);
        void Update()
        {
            actual.Content = (view.EffectiveZoom * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
            AutomationProperties.SetName(actual, $"Preview zoom {actual.Content}. Reset to 100%.");
            outButton.IsEnabled = view.Presentation is not null && view.EffectiveZoom > 1d / 16;
            inButton.IsEnabled = view.Presentation is not null && view.EffectiveZoom < 64;
            actual.IsEnabled = fit.IsEnabled = view.Presentation is not null;
            fit.Classes.Set("selected", view.IsFit);
        }
        view.ViewChanged += Update;
        Update();
        return row;
    }

    private static Button Button(string text, string id, string help, Action action)
    {
        var button = new Button
        {
            Content = text, Padding = new Thickness(5, 4), FontSize = 12,
            MinHeight = 30, MinWidth = 0, HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        AutomationProperties.SetAutomationId(button, id);
        AutomationProperties.SetName(button, help);
        ToolTip.SetTip(button, help);
        button.Click += (_, _) => action();
        return button;
    }

    private static void Add(Grid grid, Control child, int column)
    {
        Grid.SetColumn(child, column);
        grid.Children.Add(child);
    }
}
