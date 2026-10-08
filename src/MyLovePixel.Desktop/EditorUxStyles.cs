using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

internal static class EditorUxStyles
{
    public static void Apply(Avalonia.Application app)
    {
        // A full dwell is required for every control. No instant follow-on popups.
        app.Styles.Add(new Style(x => x.OfType<Control>())
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
        app.Styles.Add(new Style(x => x.OfType<Button>())
        {
            Setters = { new Setter(Button.TransitionsProperty, new Transitions
            {
                new BrushTransition { Property = Button.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(120) },
                new BrushTransition { Property = Button.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(120) },
            }) },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class(":focus-visible"))
        {
            Setters = { new Setter(Button.BorderBrushProperty, EditorThemeTokens.Accent), new Setter(Button.BorderThicknessProperty, new Thickness(2)) },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("danger"))
        {
            Setters = { new Setter(Button.ForegroundProperty, EditorThemeTokens.Danger) },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("selected").Class(":pointerover"))
        {
            Setters = { new Setter(Button.BackgroundProperty, EditorThemeTokens.SurfaceSelected), new Setter(Button.BorderBrushProperty, EditorThemeTokens.Accent) },
        });
        app.Styles.Add(new Style(x => x.OfType<TabItem>().Class(":pointerover"))
        {
            Setters = { new Setter(TabItem.ForegroundProperty, EditorThemeTokens.TextPrimary) },
        });
        app.Styles.Add(new Style(x => x.OfType<TabItem>().Class(":selected"))
        {
            Setters = { new Setter(TabItem.ForegroundProperty, EditorThemeTokens.Accent) },
        });
    }
}
