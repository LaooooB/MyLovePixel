using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;

namespace MyLovePixel.Desktop;

public static class EditorStyles
{
    public static void Apply(Avalonia.Application app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Keep Fluent controls (checkboxes, expanders, scrollbars, etc.) on the
        // same mint accent as the custom editor chrome.
        app.Resources["SystemAccentColor"] = Color.FromRgb(91, 218, 176);
        app.Resources["SystemAccentColorLight1"] = Color.FromRgb(119, 232, 195);
        app.Resources["SystemAccentColorLight2"] = Color.FromRgb(143, 239, 207);
        app.Resources["SystemAccentColorDark1"] = Color.FromRgb(69, 181, 145);
        app.Resources["SystemAccentColorDark2"] = Color.FromRgb(52, 145, 116);

        app.Styles.Add(new Style(x => x.OfType<Button>())
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, EditorThemeTokens.SurfaceRaised),
                new Setter(Button.ForegroundProperty, EditorThemeTokens.TextPrimary),
                new Setter(Button.BorderBrushProperty, EditorThemeTokens.PanelBorder),
                new Setter(Button.BorderThicknessProperty, new Thickness(1)),
                new Setter(Button.CornerRadiusProperty, EditorThemeTokens.ControlRadius),
                new Setter(Button.PaddingProperty, new Thickness(9, 5)),
                new Setter(Button.FontSizeProperty, 12d),
                new Setter(Button.MinHeightProperty, 30d),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class(":pointerover"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, EditorThemeTokens.SurfaceHover),
                new Setter(Button.BorderBrushProperty, EditorThemeTokens.StrongBorder),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class(":focus"))
        {
            Setters = { new Setter(Button.BorderBrushProperty, EditorThemeTokens.Accent) },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class(":disabled"))
        {
            Setters = { new Setter(Button.OpacityProperty, 0.38d) },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("icon"))
        {
            Setters =
            {
                new Setter(Button.WidthProperty, 34d),
                new Setter(Button.HeightProperty, 34d),
                new Setter(Button.MinHeightProperty, 34d),
                new Setter(Button.PaddingProperty, new Thickness(0)),
                new Setter(Button.HorizontalContentAlignmentProperty, Avalonia.Layout.HorizontalAlignment.Center),
                new Setter(Button.VerticalContentAlignmentProperty, Avalonia.Layout.VerticalAlignment.Center),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("small-icon"))
        {
            Setters =
            {
                new Setter(Button.WidthProperty, 28d),
                new Setter(Button.HeightProperty, 28d),
                new Setter(Button.MinHeightProperty, 28d),
                new Setter(Button.PaddingProperty, new Thickness(0)),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("text-icon"))
        {
            Setters =
            {
                new Setter(Button.PaddingProperty, new Thickness(9, 5)),
                new Setter(Button.MinHeightProperty, 34d),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("text-action"))
        {
            Setters =
            {
                new Setter(Button.MinWidthProperty, 58d),
                new Setter(Button.PaddingProperty, new Thickness(8, 5)),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("small-text-action"))
        {
            Setters =
            {
                new Setter(Button.MinWidthProperty, 44d),
                new Setter(Button.MinHeightProperty, 28d),
                new Setter(Button.PaddingProperty, new Thickness(6, 3)),
                new Setter(Button.FontSizeProperty, 11d),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("ghost"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, Brushes.Transparent),
                new Setter(Button.BorderBrushProperty, Brushes.Transparent),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("ghost").Class(":pointerover"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, EditorThemeTokens.SurfaceHover),
                new Setter(Button.BorderBrushProperty, EditorThemeTokens.PanelBorder),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("selected"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, EditorThemeTokens.SurfaceSelected),
                new Setter(Button.ForegroundProperty, EditorThemeTokens.Accent),
                new Setter(Button.BorderBrushProperty, EditorThemeTokens.Accent),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("primary"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, EditorThemeTokens.Accent),
                new Setter(Button.ForegroundProperty, EditorThemeTokens.AccentForeground),
                new Setter(Button.BorderBrushProperty, EditorThemeTokens.Accent),
            },
        });

        app.Styles.Add(new Style(x => x.OfType<TextBlock>())
        {
            Setters =
            {
                new Setter(TextBlock.ForegroundProperty, EditorThemeTokens.TextPrimary),
                new Setter(TextBlock.FontSizeProperty, 12d),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<TextBlock>().Class("muted"))
        {
            Setters = { new Setter(TextBlock.ForegroundProperty, EditorThemeTokens.TextSecondary) },
        });
        app.Styles.Add(new Style(x => x.OfType<TextBlock>().Class("subtle"))
        {
            Setters =
            {
                new Setter(TextBlock.ForegroundProperty, EditorThemeTokens.TextMuted),
                new Setter(TextBlock.FontSizeProperty, 11d),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<TextBlock>().Class("accent"))
        {
            Setters = { new Setter(TextBlock.ForegroundProperty, EditorThemeTokens.Accent) },
        });
        app.Styles.Add(new Style(x => x.OfType<TextBlock>().Class("toolbar-label"))
        {
            Setters =
            {
                new Setter(TextBlock.ForegroundProperty, EditorThemeTokens.TextMuted),
                new Setter(TextBlock.FontSizeProperty, 10d),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<TextBlock>().Class("section-title"))
        {
            Setters =
            {
                new Setter(TextBlock.ForegroundProperty, EditorThemeTokens.TextPrimary),
                new Setter(TextBlock.FontSizeProperty, 13d),
                new Setter(TextBlock.FontWeightProperty, FontWeight.SemiBold),
            },
        });

        AddInputStyle<TextBox>(app, TextBox.BackgroundProperty, TextBox.ForegroundProperty, TextBox.BorderBrushProperty, TextBox.BorderThicknessProperty);
        AddInputStyle<ComboBox>(app, ComboBox.BackgroundProperty, ComboBox.ForegroundProperty, ComboBox.BorderBrushProperty, ComboBox.BorderThicknessProperty);
        AddInputStyle<NumericUpDown>(app, NumericUpDown.BackgroundProperty, NumericUpDown.ForegroundProperty, NumericUpDown.BorderBrushProperty, NumericUpDown.BorderThicknessProperty);

        app.Styles.Add(new Style(x => x.OfType<CheckBox>())
        {
            Setters =
            {
                new Setter(CheckBox.ForegroundProperty, EditorThemeTokens.TextPrimary),
                new Setter(CheckBox.FontSizeProperty, 12d),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<TabControl>())
        {
            Setters = { new Setter(TabControl.BackgroundProperty, EditorThemeTokens.Surface) },
        });
        app.Styles.Add(new Style(x => x.OfType<TabItem>())
        {
            Setters =
            {
                new Setter(TabItem.ForegroundProperty, EditorThemeTokens.TextSecondary),
                new Setter(TabItem.PaddingProperty, new Thickness(10, 7)),
                new Setter(TabItem.FontSizeProperty, 12d),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<TabItem>().Class(":selected"))
        {
            Setters = { new Setter(TabItem.ForegroundProperty, EditorThemeTokens.Accent) },
        });
    }

    private static void AddInputStyle<T>(Avalonia.Application app, AvaloniaProperty background, AvaloniaProperty foreground, AvaloniaProperty borderBrush, AvaloniaProperty borderThickness)
        where T : Control
    {
        app.Styles.Add(new Style(x => x.OfType<T>())
        {
            Setters =
            {
                new Setter(background, EditorThemeTokens.SurfaceRaised),
                new Setter(foreground, EditorThemeTokens.TextPrimary),
                new Setter(borderBrush, EditorThemeTokens.PanelBorder),
                new Setter(borderThickness, new Thickness(1)),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<T>().Class(":focus"))
        {
            Setters = { new Setter(borderBrush, EditorThemeTokens.Accent) },
        });
    }
}
