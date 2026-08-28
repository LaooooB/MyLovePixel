using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using MyLovePixel.Application;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Desktop;

public sealed record QuantizeDialogResult(int MaxColors, bool ReserveTransparent);
public sealed record DitherDialogResult(PaletteId PaletteId, bool Bayer4x4, int Strength);
public sealed record ShadeDialogResult(IReadOnlyList<byte> RampIndices, int StepDelta);

public sealed class QuantizeDialog : Window
{
    public QuantizeDialog()
    {
        Title = "Quantize";
        Width = 370;
        Height = 270;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        var max = Number(16, 1, 256);
        var transparent = new CheckBox { Content = "Reserve transparent color", IsChecked = true };
        var root = Form();
        root.Children.Add(new TextBlock { Text = "Reduce color count", FontSize = 15, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        root.Children.Add(DialogChrome.Help("Create an indexed palette using at most the selected number of colors."));
        root.Children.Add(DialogChrome.Labeled("Maximum colors", max));
        root.Children.Add(transparent);
        root.Children.Add(DialogChrome.ConfirmCancel(
            () => Close(null),
            () => Close(new QuantizeDialogResult((int)(max.Value ?? 16), transparent.IsChecked == true)),
            "Quantize"));
        Content = root;
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new()
    {
        Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0"
    };
    private static StackPanel Form() => new() { Spacing = 10, Margin = new Thickness(16) };
}

public sealed class DitherDialog : Window
{
    public DitherDialog(IReadOnlyList<PaletteEditorPresentation> palettes)
    {
        Title = "Dither";
        Width = 390;
        Height = 330;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        var palette = new ComboBox
        {
            ItemsSource = palettes.Select((v, i) => $"Palette {i + 1} · {v.Colors.Count} colors").ToArray(),
            SelectedIndex = palettes.Count > 0 ? 0 : -1,
        };
        var matrix = new ComboBox { ItemsSource = new[] { "Bayer 2×2", "Bayer 4×4" }, SelectedIndex = 1 };
        var strength = Number(64, 0, 255);

        var root = Form();
        root.Children.Add(new TextBlock { Text = "Ordered dithering", FontSize = 15, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        root.Children.Add(DialogChrome.Help("Map RGBA pixels into a selected palette using an ordered Bayer matrix."));
        root.Children.Add(DialogChrome.Labeled("Palette", palette));
        root.Children.Add(DialogChrome.Labeled("Matrix", matrix));
        root.Children.Add(DialogChrome.Labeled("Strength", strength));
        root.Children.Add(DialogChrome.ConfirmCancel(
            () => Close(null),
            () =>
            {
                if ((uint)palette.SelectedIndex >= (uint)palettes.Count) return;
                Close(new DitherDialogResult(
                    palettes[palette.SelectedIndex].Id,
                    matrix.SelectedIndex == 1,
                    (int)(strength.Value ?? 64)));
            },
            "Dither"));
        Content = root;
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new()
    {
        Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0"
    };
    private static StackPanel Form() => new() { Spacing = 10, Margin = new Thickness(16) };
}

public sealed class ShadeDialog : Window
{
    public ShadeDialog(PaletteEditorPresentation palette)
    {
        Title = "Color Ramp / Shade";
        Width = 430;
        Height = 300;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        var ramp = new TextBox
        {
            Text = string.Join(",", Enumerable.Range(0, Math.Min(8, palette.Colors.Count))),
            PlaceholderText = "0,1,2,3",
        };
        var step = Number(1, -255, 255);

        var root = Form();
        root.Children.Add(new TextBlock { Text = "Palette ramp shading", FontSize = 15, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        root.Children.Add(DialogChrome.Help("List palette indices from dark to light, then choose how many ramp steps to shift each selected pixel."));
        root.Children.Add(DialogChrome.Labeled("Ramp indices", ramp));
        root.Children.Add(DialogChrome.Labeled("Step delta", step));
        root.Children.Add(DialogChrome.ConfirmCancel(
            () => Close(null),
            () =>
            {
                var values = Parse(ramp.Text);
                if (values.Count == 0) return;
                Close(new ShadeDialogResult(values, (int)(step.Value ?? 1)));
            },
            "Apply"));
        Content = root;
    }

    private static IReadOnlyList<byte> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var values = new List<byte>();
        foreach (var token in text.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (byte.TryParse(token, out var value) && !values.Contains(value)) values.Add(value);
        }
        return values;
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new()
    {
        Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0"
    };
    private static StackPanel Form() => new() { Spacing = 10, Margin = new Thickness(16) };
}
