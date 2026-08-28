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

        app.Styles.Add(new Style(x => x.OfType<Button>())
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, EditorThemeTokens.SurfaceRaised),
                new Setter(Button.ForegroundProperty, EditorThemeTokens.TextPrimary),
                new Setter(Button.BorderBrushProperty, EditorThemeTokens.PanelBorder),
                new Setter(Button.BorderThicknessProperty, new Thickness(1)),
                new Setter(Button.CornerRadiusProperty, EditorThemeTokens.ControlRadius),
                new Setter(Button.PaddingProperty, new Thickness(8, 4)),
                new Setter(Button.FontSizeProperty, 12d),
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
                new Setter(Button.PaddingProperty, new Thickness(0)),
                new Setter(Button.FontSizeProperty, 17d),
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
                new Setter(Button.PaddingProperty, new Thickness(0)),
                new Setter(Button.FontSizeProperty, 14d),
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
            Setters = { new Setter(TextBlock.ForegroundProperty, EditorThemeTokens.TextMuted) },
        });
        app.Styles.Add(new Style(x => x.OfType<TextBlock>().Class("accent"))
        {
            Setters = { new Setter(TextBlock.ForegroundProperty, EditorThemeTokens.Accent) },
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
                new Setter(TabItem.PaddingProperty, new Thickness(8, 6)),
            },
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
    }
}
