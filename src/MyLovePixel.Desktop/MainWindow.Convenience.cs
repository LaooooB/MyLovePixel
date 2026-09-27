using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private readonly PixelPreviewView _quickPreview = new();
    private readonly WrapPanel _studioPaletteSwatches = new() { ItemWidth = 20, ItemHeight = 20 };
    private readonly NumericUpDown _studioR = ChannelInput();
    private readonly NumericUpDown _studioG = ChannelInput();
    private readonly NumericUpDown _studioB = ChannelInput();
    private readonly TextBox _studioHex = new() { Text = "#000000", PlaceholderText = "#654321", MinWidth = 84, MaxLength = 32 };
    private readonly Border _studioColorPreview = Swatch();
    private bool _convenienceInstalled;
    private bool _syncingStudioColor;
    private bool _studioSecondaryTarget;
    private Rgba32 _studioColor = new(0, 0, 0, 255);






    private static NumericUpDown ChannelInput() => new()
    {
        Value = 0,
        Minimum = 0,
        Maximum = 255,
        Increment = 1,
        FormatString = "0",
        MinWidth = 54,
    };

    private static TextBlock ChannelLabel(string text)
    {
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.Classes.Add("muted");
        return label;
    }







    private static bool TryParseHex(string? text, out Rgba32 color) => HexColor.TryParse(text, out color);

    private static IReadOnlyList<Rgba32> BuildStudioPaletteColors()
    {
        var colors = new List<Rgba32>(512);
        var variants = new List<(double Saturation, double Value)>(31);

        var saturations = new[] { 0.22, 0.38, 0.54, 0.70, 0.86, 1.00 };
        var values = new[] { 0.30, 0.46, 0.62, 0.78, 0.94 };
        foreach (var value in values)
        foreach (var saturation in saturations)
            variants.Add((saturation, value));
        variants.Add((0.12, 0.99));

        foreach (var variant in variants)
        for (var hueIndex = 0; hueIndex < 16; hueIndex++)
            colors.Add(HsvToRgba(hueIndex * 360d / 16d, variant.Saturation, variant.Value));

        for (var i = 0; i < 16; i++)
        {
            var value = (byte)Math.Round(i * 255d / 15d);
            colors.Add(new Rgba32(value, value, value, 255));
        }

        return colors;
    }

    private static Rgba32 HsvToRgba(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var h = (hue % 360d) / 60d;
        var x = c * (1d - Math.Abs((h % 2d) - 1d));
        var (r1, g1, b1) = h switch
        {
            < 1d => (c, x, 0d),
            < 2d => (x, c, 0d),
            < 3d => (0d, c, x),
            < 4d => (0d, x, c),
            < 5d => (x, 0d, c),
            _ => (c, 0d, x),
        };
        var m = value - c;
        return new Rgba32(
            (byte)Math.Round((r1 + m) * 255d),
            (byte)Math.Round((g1 + m) * 255d),
            (byte)Math.Round((b1 + m) * 255d),
            255);
    }





    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_convenienceInstalled) return;
        _convenienceInstalled = true;
        FitWindowToScreen();
        RefreshAll();
        Dispatcher.UIThread.Post(FitCanvas, DispatcherPriority.Background);
    }
}
