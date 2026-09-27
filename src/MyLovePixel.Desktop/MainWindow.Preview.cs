using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private PreviewWindow? _previewWindow;
    private DocumentSession? _previewSession;
    private Control? _previewZoomControls;

    private Control BuildInspectorPreviewBox()
    {
        _quickPreview.Height = 128;
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8, HorizontalAlignment = HorizontalAlignment.Stretch };
        header.Children.Add(new TextBlock { Text = "Preview", VerticalAlignment = VerticalAlignment.Center });
        var enlarge = Named(new Button { Content = "Enlarge", Padding = new Thickness(7, 3), MinHeight = 26, FontSize = 12 },
            "preview.enlarge", "Open larger live preview");
        enlarge.Click += (_, _) => OpenPreviewWindow();
        ToolTip.SetTip(enlarge, "Open larger live preview");
        Grid.SetColumn(enlarge, 1); header.Children.Add(enlarge);
        _previewExpander.Header = header;
        _quickPreview.EnlargeRequested = OpenPreviewWindow;
        AutomationProperties.SetAutomationId(_quickPreview, "preview.viewport");
        AutomationProperties.SetName(_quickPreview, "Final image preview. Scroll to zoom, double-click to enlarge.");
        var body = new StackPanel { Spacing = 5 };
        body.Children.Add(_quickPreview);
        body.Children.Add(_previewZoomControls = PreviewControls.Build(_quickPreview, "preview"));
        return new Border { Margin = new Thickness(8, 0, 8, 8), Child = body, ClipToBounds = true };
    }

    private void OpenPreviewWindow()
    {
        if (_previewWindow is { } existing)
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }
        var window = new PreviewWindow();
        _previewWindow = window;
        window.SetPresentation(_quickPreview.Presentation);
        window.Closed += (_, _) => { if (ReferenceEquals(_previewWindow, window)) _previewWindow = null; };
        var screen = Screens.ScreenFromWindow(this);
        if (screen is not null)
        {
            window.Width = Math.Min(window.Width, screen.WorkingArea.Width / screen.Scaling * .9);
            window.Height = Math.Min(window.Height, screen.WorkingArea.Height / screen.Scaling * .9);
        }
        window.Show(this);
    }

    private void RefreshFinalPreview(DocumentSession? session)
    {
        var preview = session is null ? null : _plugins.RenderFramePreview(session, session.CurrentFrameId);
        if (!ReferenceEquals(_previewSession, session))
        {
            _quickPreview.Fit();
            _previewWindow?.SetPresentation(null);
            _previewSession = session;
        }
        _quickPreview.SetPresentation(preview);
        _previewWindow?.SetPresentation(preview);
    }
}
