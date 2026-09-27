using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

internal sealed class PreviewWindow : Window
{
    private readonly PixelPreviewView _view = new();

    public PreviewWindow()
    {
        Title = "Preview · MyLovePixel";
        Width = 800; Height = 640;
        MinWidth = 360; MinHeight = 240;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.Surface;
        AutomationProperties.SetAutomationId(this, "preview.window");
        AutomationProperties.SetAutomationId(_view, "preview.large.viewport");
        AutomationProperties.SetName(_view, "Final image preview. Scroll to zoom, drag to pan.");
        var root = new DockPanel();
        var controls = new Border { Padding = new Thickness(10, 7), Child = PreviewControls.Build(_view, "preview.large") };
        DockPanel.SetDock(controls, Dock.Top);
        root.Children.Add(controls);
        root.Children.Add(_view);
        Content = root;
        Opened += (_, _) => _view.Focus();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }

    public void SetPresentation(CanvasPresentation? presentation) => _view.SetPresentation(presentation);
}
