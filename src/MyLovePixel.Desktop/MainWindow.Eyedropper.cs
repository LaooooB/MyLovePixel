using Avalonia.Controls;
using Avalonia.Input;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private bool _eyedropperMode;
    private bool _sampleCurrentLayer;

    private void ActivateEyedropper()
    {
        if (Current() is not { } session) return;
        CancelSelectionTransformGesture();
        _selectionMode = false;
        _plugins.CancelTool(session);
        _canvasPointerActive = false;
        _eyedropperMode = true;
        _canvas.Cursor = new Cursor(StandardCursorType.Cross);
        RefreshAll();
    }

    private void LeaveEyedropper()
    {
        _eyedropperMode = false;
        _canvas.Cursor = Cursor.Default;
    }

    private void PickCanvasColor(int x, int y, bool secondary)
    {
        if (Current() is not { } session) return;
        Safe(() =>
        {
            var sample = PixelSampling.ReadPixel(session, x, y, _sampleCurrentLayer);
            if (sample is null) return;
            PixelSampling.ApplySample(session, sample, secondary);
            _studioSecondaryTarget = secondary;
            SyncStudioColor(sample.Color);
            RefreshConvenienceUi();
            _status.Text = $"Picked {HexColor.Format(sample.Color)}. + Temp keeps this color; I continues sampling, B returns to pencil.";
        });
    }

    private Control BuildEyedropperOptions()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Eyedropper · I", FontWeight = Avalonia.Media.FontWeight.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Click artwork to pick a color. Right-click picks the secondary color. + Temp keeps each sample. B returns to pencil.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var source = new CheckBox { Content = "Sample current layer only", IsChecked = _sampleCurrentLayer };
        source.IsCheckedChanged += (_, _) => _sampleCurrentLayer = source.IsChecked == true;
        panel.Children.Add(source);
        return panel;
    }
}
