using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private readonly CanvasDisplaySettingsStore _displaySettingsStore = new();
    private CanvasDisplaySettings _displaySettings = new();
    private Border? _comfortCanvasFrame;
    private Border? _comfortPreviewFrame;
    private DispatcherTimer? _displaySaveTimer;
    private bool _displaySavePending;
    private readonly TextBlock _displaySaveStatus = new() { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, IsVisible = false };

    private Control BuildCanvasComfortBar()
    {
        _displaySettings = _displaySettingsStore.Current;
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = _displaySettings.BackgroundBrightness,
            SmallChange = 1, LargeChange = 10, MinWidth = 90, VerticalAlignment = VerticalAlignment.Center };
        var value = new TextBlock { Text = _displaySettings.BackgroundBrightness + "%", VerticalAlignment = VerticalAlignment.Center };
        var reset = new Button { Content = "Reset", MinWidth = 0, Padding = new Thickness(7, 4) };
        reset.Classes.Add("text-action");
        AutomationProperties.SetAutomationId(slider, "canvas-backdrop-brightness");
        AutomationProperties.SetName(slider, "Backdrop brightness. Left is softer, right is brighter. Artwork colors are unchanged.");
        AutomationProperties.SetAutomationId(reset, "canvas-backdrop-reset");
        ToolTip.SetTip(slider, "Adjust the light-gray transparency backdrop and preview. This does not change pixels, picked colors or exports.");
        ToolTip.SetTip(reset, "Restore the default backdrop brightness (60%).");
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,40,Auto"), ColumnSpacing = 8 };
        row.Children.Add(new TextBlock { Text = "Backdrop", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(Place(slider, 1)); row.Children.Add(Place(value, 2)); row.Children.Add(Place(reset, 3));
        _displaySaveStatus.Foreground = EditorThemeTokens.Warning;
        var body = new StackPanel { Spacing = 3 };
        body.Children.Add(row); body.Children.Add(_displaySaveStatus);
        _displaySaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _displaySaveTimer.Tick += (_, _) => SaveCanvasDisplaySettings();
        void Sync()
        {
            _displaySettings = new CanvasDisplaySettings((int)Math.Round(slider.Value));
            value.Text = _displaySettings.BackgroundBrightness + "%";
            reset.IsEnabled = _displaySettings.BackgroundBrightness != CanvasDisplaySettings.DefaultBrightness;
            ApplyCanvasDisplaySettings();
        }
        slider.ValueChanged += (_, _) =>
        {
            Sync();
            _displaySavePending = true;
            _displaySaveTimer.Stop(); _displaySaveTimer.Start();
        };
        reset.Click += (_, _) => slider.Value = CanvasDisplaySettings.DefaultBrightness;
        Closed += (_, _) =>
        {
            if (_displaySavePending) SaveCanvasDisplaySettings();
            _displaySaveTimer.Stop();
            _canvas.ReleaseDisplayResources();
            _quickPreview.ReleaseDisplayResources();
        };
        Sync();
        if (_displaySettingsStore.LoadError is { } error) ShowDisplaySaveError(error);
        return new Border { Padding = new Thickness(10, 5), Background = EditorThemeTokens.Surface, Child = body };
    }

    private void ApplyCanvasDisplaySettings()
    {
        _canvas.SetDisplaySettings(_displaySettings);
        _quickPreview.SetDisplaySettings(_displaySettings);
        var frame = CanvasBackdrop.Solid(_displaySettings.Frame);
        if (_comfortCanvasFrame is not null) _comfortCanvasFrame.Background = frame;
        if (_comfortPreviewFrame is not null) _comfortPreviewFrame.Background = frame;
    }

    private void SaveCanvasDisplaySettings()
    {
        _displaySaveTimer?.Stop();
        if (!_displaySavePending) return;
        try
        {
            _displaySettingsStore.Save(_displaySettings);
            _displaySaveStatus.IsVisible = false;
            _displaySavePending = false;
        }
        catch (Exception error) when (CanvasDisplaySettingsStore.IsStorageError(error))
        {
            ShowDisplaySaveError(error.Message);
            // Keep the preview usable, but never claim an unsuccessful write was saved.
        }
    }

    private void ShowDisplaySaveError(string error)
    {
        _displaySaveStatus.Text = "View changed for this session; settings could not be saved.";
        _displaySaveStatus.IsVisible = true;
        ToolTip.SetTip(_displaySaveStatus, error);
    }
}
