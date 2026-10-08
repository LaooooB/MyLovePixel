using HsvColor = MyLovePixel.Application.HsvColor;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

internal sealed class ColorPickerDialog : Window
{
    private Rgba32 _color;
    private HsvColor _hsv;
    private bool _sync;
    private readonly ColorSpectrum _spectrum = new() { Height = 224, MinWidth = 220 };
    private readonly ColorSpectrum _hue = new() { HueStrip = true, Height = 34 };
    private readonly Slider _alpha = new() { Minimum = 0, Maximum = 255, TickFrequency = 1, IsSnapToTickEnabled = true };
    private readonly TextBox _hex = new() { MinWidth = 130, MaxLength = 9 };
    private readonly ColorSwatchView _preview = new() { Width = 40, Height = 36 };
    private readonly TextBlock _error = new() { Foreground = EditorThemeTokens.Danger, IsVisible = false };
    public ColorPickerDialog(Rgba32 initial)
    {
        _color = initial; _hsv = HsvColor.FromColor(initial);
        Title = "Pick color"; Width = 408; CanResize = true; MinWidth = 300; MinHeight = 330;
        SizeToContent = SizeToContent.Height; MaxHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = EditorThemeTokens.Surface;
        AutomationProperties.SetAutomationId(this, "color.picker.dialog");
        Identify(_spectrum, "color.picker.spectrum", "Saturation and brightness. Drag the circle or use arrow keys.");
        Identify(_hue, "color.picker.hue", "Hue. Drag or use arrow keys.");
        Identify(_alpha, "color.picker.alpha", "Opacity"); Identify(_hex, "color.picker.hex", "Picker HEX color");
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(_spectrum); body.Children.Add(_hue);
        body.Children.Add(DialogChrome.Labeled("Opacity", _alpha, 58));
        var entry = new Grid { ColumnDefinitions = new ColumnDefinitions("40,Auto,*"), ColumnSpacing = 8 };
        entry.Children.Add(_preview); entry.Children.Add(Put(new TextBlock { Text = "HEX", VerticalAlignment = VerticalAlignment.Center }, 1)); entry.Children.Add(Put(_hex, 2)); body.Children.Add(entry);
        body.Children.Add(_error);
        var accept = DialogChrome.TextButton("Use color", () => { if (ApplyHex()) Close((Rgba32?)_color); }, true);
        Identify(accept, "color.picker.apply", "Use selected color");
        var cancel = DialogChrome.TextButton("Cancel", () => Close(null)); Identify(cancel, "color.picker.cancel", "Cancel color picker");
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel); buttons.Children.Add(accept); body.Children.Add(buttons);
        DialogChrome.SetContent(this, body);
        _spectrum.ColorChanged += hsv => { _hsv = hsv; FromHsv(); };
        _hue.ColorChanged += hsv => { _hsv = _hsv with { Hue = hsv.Hue }; FromHsv(); };
        _alpha.PropertyChanged += (_, e) => { if (!_sync && e.Property == Slider.ValueProperty) { _color = _color with { A = (byte)Math.Round(_alpha.Value) }; RefreshColor(); } };
        _hex.KeyDown += (_, e) => { if (e.Key == Key.Enter) { ApplyHex(); e.Handled = true; } };
        _hex.LostFocus += (_, _) => { if (!_sync) ApplyHex(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(null); e.Handled = true; } };
        RefreshColor();
    }
    private void FromHsv() { _color = _hsv.ToColor(_color.A); RefreshColor(); }
    private bool ApplyHex()
    {
        if (_sync) return true;
        if (!HexColor.TryParse(_hex.Text, out var color)) { _error.Text = "Use #RRGGBB or #RRGGBBAA."; _error.IsVisible = true; return false; }
        _color = color;
        var hsv = HsvColor.FromColor(color);
        _hsv = hsv.Saturation == 0 ? hsv with { Hue = _hsv.Hue } : hsv;
        RefreshColor(); return true;
    }
    private void RefreshColor()
    {
        _sync = true;
        try { _spectrum.Hsv = _hue.Hsv = _hsv; _preview.Color = _color; _alpha.Value = _color.A; _hex.Text = HexColor.Format(_color); _error.IsVisible = false; }
        finally { _sync = false; }
    }
    private static void Identify(Control c, string id, string name) { AutomationProperties.SetAutomationId(c, id); AutomationProperties.SetName(c, name); }
    private static T Put<T>(T c, int col) where T : Control { Grid.SetColumn(c, col); return c; }
}

public sealed partial class MainWindow
{
    private async Task OpenColorPickerAsync()
    {
        if (Current() is null || _busy) return;
        var initial = HexColor.TryParse(_studioHex.Text, out var color) ? color : _studioColor;
        if (await new ColorPickerDialog(initial).ShowDialog<Rgba32?>(this) is { } chosen) ApplyStudioColor(chosen);
    }
}
