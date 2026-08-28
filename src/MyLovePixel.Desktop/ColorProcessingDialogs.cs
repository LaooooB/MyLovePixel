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
        Title = "Quantize"; Width = 330; Height = 210; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = EditorThemeTokens.AppBackground;
        var max = Number(16, 1, 256); var transparent = new CheckBox { Content = "Transparent", IsChecked = true };
        Content = Form(Labeled("Colors", max), transparent, Actions(() => Close(null), () => Close(new QuantizeDialogResult((int)(max.Value ?? 16), transparent.IsChecked == true))));
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new() { Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0" };
    private static Control Labeled(string label, Control control) => new StackPanel { Spacing = 3, Children = { new TextBlock { Text = label }, control } };
    private static StackPanel Form(params Control[] controls) { var p = new StackPanel { Spacing = 10, Margin = new Thickness(14) }; foreach (var c in controls) p.Children.Add(c); return p; }
    private static Control Actions(Action cancel, Action apply) { var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right }; p.Children.Add(Button("×", "Cancel", cancel)); var ok = Button("✓", "Apply", apply); ok.Classes.Add("primary"); p.Children.Add(ok); return p; }
    private static Button Button(string glyph, string tip, Action action) { var b = new Button { Content = glyph }; b.Classes.Add("icon"); ToolTip.SetTip(b, tip); b.Click += (_, _) => action(); return b; }
}

public sealed class DitherDialog : Window
{
    public DitherDialog(IReadOnlyList<PaletteEditorPresentation> palettes)
    {
        Title = "Dither"; Width = 350; Height = 260; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = EditorThemeTokens.AppBackground;
        var palette = new ComboBox { ItemsSource = palettes.Select((v, i) => $"Palette {i + 1} · {v.Colors.Count}").ToArray(), SelectedIndex = palettes.Count > 0 ? 0 : -1 };
        var matrix = new ComboBox { ItemsSource = new[] { "Bayer 2×2", "Bayer 4×4" }, SelectedIndex = 1 };
        var strength = Number(64, 0, 255);
        Content = Form(Labeled("Palette", palette), Labeled("Matrix", matrix), Labeled("Strength", strength), Actions(() => Close(null), () =>
        {
            if ((uint)palette.SelectedIndex >= (uint)palettes.Count) return;
            Close(new DitherDialogResult(palettes[palette.SelectedIndex].Id, matrix.SelectedIndex == 1, (int)(strength.Value ?? 64)));
        }));
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new() { Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0" };
    private static Control Labeled(string label, Control control) => new StackPanel { Spacing = 3, Children = { new TextBlock { Text = label }, control } };
    private static StackPanel Form(params Control[] controls) { var p = new StackPanel { Spacing = 9, Margin = new Thickness(14) }; foreach (var c in controls) p.Children.Add(c); return p; }
    private static Control Actions(Action cancel, Action apply) { var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right }; p.Children.Add(Button("×", "Cancel", cancel)); var ok = Button("✓", "Apply", apply); ok.Classes.Add("primary"); p.Children.Add(ok); return p; }
    private static Button Button(string glyph, string tip, Action action) { var b = new Button { Content = glyph }; b.Classes.Add("icon"); ToolTip.SetTip(b, tip); b.Click += (_, _) => action(); return b; }
}

public sealed class ShadeDialog : Window
{
    public ShadeDialog(PaletteEditorPresentation palette)
    {
        Title = "Ramp"; Width = 390; Height = 230; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = EditorThemeTokens.AppBackground;
        var ramp = new TextBox { Text = string.Join(",", Enumerable.Range(0, Math.Min(8, palette.Colors.Count))), PlaceholderText = "0,1,2,3" };
        var step = Number(1, -255, 255);
        Content = Form(Labeled("Indices", ramp), Labeled("Step", step), Actions(() => Close(null), () =>
        {
            var values = Parse(ramp.Text);
            if (values.Count == 0) return;
            Close(new ShadeDialogResult(values, (int)(step.Value ?? 1)));
        }));
    }

    private static IReadOnlyList<byte> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var values = new List<byte>();
        foreach (var token in text.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (byte.TryParse(token, out var value) && !values.Contains(value)) values.Add(value);
        return values;
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new() { Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0" };
    private static Control Labeled(string label, Control control) => new StackPanel { Spacing = 3, Children = { new TextBlock { Text = label }, control } };
    private static StackPanel Form(params Control[] controls) { var p = new StackPanel { Spacing = 9, Margin = new Thickness(14) }; foreach (var c in controls) p.Children.Add(c); return p; }
    private static Control Actions(Action cancel, Action apply) { var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right }; p.Children.Add(Button("×", "Cancel", cancel)); var ok = Button("✓", "Apply", apply); ok.Classes.Add("primary"); p.Children.Add(ok); return p; }
    private static Button Button(string glyph, string tip, Action action) { var b = new Button { Content = glyph }; b.Classes.Add("icon"); ToolTip.SetTip(b, tip); b.Click += (_, _) => action(); return b; }
}
