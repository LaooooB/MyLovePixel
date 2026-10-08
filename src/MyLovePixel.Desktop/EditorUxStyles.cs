using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Styling;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

internal static class EditorUxStyles
{
    private static BrushTransition Fade(AvaloniaProperty property) => new()
    {
        Property = property, Duration = TimeSpan.FromMilliseconds(160), Easing = new CubicEaseOut(),
    };

    public static void Apply(Avalonia.Application app)
    {
        app.Styles.Add(new Style(x => x.Is<Control>())
        {
            Setters =
            {
                new Setter(ToolTip.ShowDelayProperty, CanvasDisplaySettings.TooltipDelayMilliseconds),
                new Setter(ToolTip.BetweenShowDelayProperty, CanvasDisplaySettings.TooltipBetweenShowDelayMilliseconds),
                new Setter(ToolTip.PlacementProperty, PlacementMode.Bottom),
                new Setter(ToolTip.VerticalOffsetProperty, 8d),
                new Setter(ToolTip.ShowOnDisabledProperty, true),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<ToolTip>())
        {
            Setters = { new Setter(ToolTip.MaxWidthProperty, 340d), new Setter(ToolTip.FontSizeProperty, 12d) },
        });
        app.Styles.Add(new Style(x => x.Is<Button>().Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters = { new Setter(ContentPresenter.TransitionsProperty, new Transitions
            {
                Fade(ContentPresenter.BackgroundProperty), Fade(ContentPresenter.BorderBrushProperty), Fade(ContentPresenter.ForegroundProperty),
            }) },
        });
        // Explicit resting template states prevent the leave transition from
        // being replaced by a different TemplateBinding as pointer styles detach.
        RestingButton(app, null, EditorThemeTokens.SurfaceRaised, EditorThemeTokens.PanelBorder, EditorThemeTokens.TextPrimary);
        RestingButton(app, "ghost", Brushes.Transparent, Brushes.Transparent, EditorThemeTokens.TextPrimary);
        RestingButton(app, "selected", EditorThemeTokens.SurfaceSelected, EditorThemeTokens.Accent, EditorThemeTokens.Accent);
        RestingButton(app, "primary", EditorThemeTokens.Accent, EditorThemeTokens.Accent, EditorThemeTokens.AccentForeground);
        app.Styles.Add(new Style(x => x.Is<Button>().Class(":pointerover").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, EditorThemeTokens.SurfaceHover),
                new Setter(ContentPresenter.BorderBrushProperty, EditorThemeTokens.StrongBorder),
                new Setter(ContentPresenter.ForegroundProperty, EditorThemeTokens.TextPrimary),
            },
        });
        app.Styles.Add(new Style(x => x.Is<Button>().Class("selected").Class(":pointerover").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, EditorThemeTokens.SurfaceSelected),
                new Setter(ContentPresenter.BorderBrushProperty, EditorThemeTokens.Accent),
                new Setter(ContentPresenter.ForegroundProperty, EditorThemeTokens.Accent),
            },
        });
        app.Styles.Add(new Style(x => x.Is<Button>().Class("primary").Class(":pointerover").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, EditorThemeTokens.AccentHover),
                new Setter(ContentPresenter.BorderBrushProperty, EditorThemeTokens.Accent),
                new Setter(ContentPresenter.ForegroundProperty, EditorThemeTokens.AccentForeground),
            },
        });
        app.Styles.Add(new Style(x => x.Is<Button>().Class(":pressed").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, EditorThemeTokens.SurfaceSelected),
                new Setter(ContentPresenter.BorderBrushProperty, EditorThemeTokens.Accent),
                new Setter(ContentPresenter.TransitionsProperty, new Transitions()),
            },
        });
        app.Styles.Add(new Style(x => x.Is<Button>().Class(":pressed"))
        {
            Setters = { new Setter(Button.RenderTransformProperty, null) },
        });
        app.Styles.Add(new Style(x => x.Is<Button>().Class(":focus-visible"))
        {
            Setters = { new Setter(Button.BorderBrushProperty, EditorThemeTokens.Accent) },
        });
        app.Styles.Add(new Style(x => x.Is<Button>().Class("danger"))
        {
            Setters = { new Setter(Button.ForegroundProperty, EditorThemeTokens.Danger) },
        });
        app.Styles.Add(new Style(x => x.OfType<TabItem>().Template().OfType<Border>().Name("PART_LayoutRoot"))
        {
            Setters = { new Setter(Border.TransitionsProperty, new Transitions
            {
                Fade(Border.BackgroundProperty), Fade(Border.BorderBrushProperty),
            }) },
        });
        app.Styles.Add(new Style(x => x.OfType<TabItem>().Class(":pointerover").Template().OfType<Border>().Name("PART_LayoutRoot"))
        {
            Setters = { new Setter(Border.BackgroundProperty, EditorThemeTokens.SurfaceHover) },
        });
        app.Styles.Add(new Style(x => x.OfType<TabItem>().Class(":selected").Class(":pointerover").Template().OfType<Border>().Name("PART_LayoutRoot"))
        {
            Setters = { new Setter(Border.BackgroundProperty, EditorThemeTokens.SurfaceSelected) },
        });
        app.Styles.Add(new Style(x => x.OfType<TabItem>().Class(":selected"))
        {
            Setters = { new Setter(TabItem.ForegroundProperty, EditorThemeTokens.Accent) },
        });
    }

    private static void RestingButton(Avalonia.Application app, string? @class, IBrush background, IBrush border, IBrush foreground)
    {
        app.Styles.Add(new Style(x =>
        {
            var selector = x.Is<Button>().Not(s => s.Class(":pointerover"));
            if (@class is not null) selector = selector.Class(@class);
            return selector.Template().OfType<ContentPresenter>().Name("PART_ContentPresenter");
        })
        {
            Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, background),
                new Setter(ContentPresenter.BorderBrushProperty, border),
                new Setter(ContentPresenter.ForegroundProperty, foreground),
            },
        });
    }
}
