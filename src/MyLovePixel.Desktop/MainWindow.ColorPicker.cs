using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private Flyout? _colorPickerFlyout;

    private Button BuildColorPickerButton()
    {
        var button = new Button { Content = "Picker", MinWidth = 0, Padding = new Thickness(7, 5) };
        button.Classes.Add("text-action");
        AutomationProperties.SetAutomationId(button, "studio-color-picker");
        ToolTip.SetTip(button, "Drag the circle to choose a color. Done keeps it; Escape cancels.");
        AutomationProperties.SetName(button, "Open color picker for the active drawing color");
        var spectrum = new HsvSpectrumControl();
        var hue = new Slider { Minimum = 0, Maximum = 359.999, Value = 0 };
        var alpha = new Slider { Minimum = 0, Maximum = 255, Value = 255 };
        AutomationProperties.SetName(hue, "Color hue");
        AutomationProperties.SetName(alpha, "Color opacity");
        AutomationProperties.SetAutomationId(hue, "color-picker-hue");
        AutomationProperties.SetAutomationId(alpha, "color-picker-alpha");
        var preview = new Border { Width = 30, Height = 22, BorderBrush = EditorThemeTokens.PanelBorder, BorderThickness = new Thickness(1) };
        var value = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var body = new StackPanel { Spacing = 7, Width = 260 };
        var flyout = _colorPickerFlyout = new Flyout { Content = body, Placement = PlacementMode.Bottom };
        var original = new Rgba32(0, 0, 0);
        var syncing = false;
        var hueBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        };
        for (var index = 0; index <= 6; index++)
        {
            var color = ColorPickerMath.FromHsv(index * 60, 1, 1);
            hueBrush.GradientStops.Add(new GradientStop(Avalonia.Media.Color.FromRgb(color.R, color.G, color.B), index / 6d));
        }
        body.Children.Add(new TextBlock { Text = "Color picker", FontWeight = FontWeight.SemiBold });
        body.Children.Add(spectrum);
        body.Children.Add(new TextBlock { Text = "Live preview · Done / outside keeps · Esc cancels", FontSize = 11, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock { Text = "Hue" });
        body.Children.Add(new Border { Height = 9, Background = hueBrush, CornerRadius = new CornerRadius(3) });
        body.Children.Add(hue);
        body.Children.Add(new TextBlock { Text = "Opacity" });
        body.Children.Add(alpha);
        body.Children.Add(LibraryRow(new Border { Background = CanvasBackdrop.Create(new CanvasDisplaySettings()), Child = preview }, value));
        body.Children.Add(LibraryRow(
            LibraryButton("Cancel", () => { ApplyStudioColor(original); flyout.Hide(); button.Focus(); }),
            LibraryButton("Done", () => { flyout.Hide(); button.Focus(); })));
        spectrum.ColorChanged += color =>
        {
            preview.Background = Brush(color);
            value.Text = HexColor.Format(color);
            ApplyStudioColor(color);
        };
        hue.ValueChanged += (_, _) => { if (!syncing) spectrum.SetHue(hue.Value); };
        alpha.ValueChanged += (_, _) => { if (!syncing) spectrum.SetAlpha((byte)Math.Round(alpha.Value)); };
        body.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape) { ApplyStudioColor(original); flyout.Hide(); button.Focus(); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
        Closed += (_, _) => _colorPickerFlyout?.Hide();
        button.Click += (_, _) =>
        {
            if (!HexColor.TryParse(_studioHex.Text, out var entered)) { ApplyStudioHex(); _studioHex.Focus(); return; }
            ApplyStudioColor(entered);
            original = ActiveLibraryColor();
            syncing = true;
            try
            {
                spectrum.SetColor(original);
                hue.Value = spectrum.Hue;
                alpha.Value = original.A;
                preview.Background = Brush(original);
                value.Text = HexColor.Format(original);
            }
            finally { syncing = false; }
            flyout.ShowAt(button);
            Dispatcher.UIThread.Post(() => spectrum.Focus());
        };
        return button;
    }

    private Button BuildColorUpgradeImportButton()
    {
        var button = new Button { Content = "Import", Padding = new Thickness(9, 5) };
        button.Classes.Add("text-action");
        var body = new StackPanel { Spacing = 6, Width = 270 };
        var flyout = new Flyout { Content = body, Placement = PlacementMode.Bottom };
        var assets = new Button { Content = "PNG / Sprite JSON", HorizontalAlignment = HorizontalAlignment.Stretch };
        var photo = new Button { Content = "Photo → Pixel", HorizontalAlignment = HorizontalAlignment.Stretch };
        assets.Click += async (_, _) =>
        {
            flyout.Hide();
            try { await ImportAssetAsync(); }
            catch (Exception error) { SetError(error.Message); }
        };
        photo.Click += async (_, _) =>
        {
            flyout.Hide();
            try { await ChoosePhotoPixelAsync(); }
            catch (Exception error) { SetError(error.Message); }
        };
        body.Children.Add(assets);
        body.Children.Add(photo);
        body.Children.Add(new TextBlock { Text = "Sprite-sheet slicing is in Advanced → Animation.", TextWrapping = TextWrapping.Wrap, FontSize = 11 });
        button.Click += (_, _) => flyout.ShowAt(button);
        return button;
    }
}
