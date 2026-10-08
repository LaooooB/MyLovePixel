using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;

namespace MyLovePixel.Desktop;

/// <summary>Short, reversible transitions on chrome; artwork and color swatches stay exact.</summary>
internal static class EditorMotion
{
    internal static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(140);
    private static bool _installed;
    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        Control.LoadedEvent.AddClassHandler<Control>((control, _) => Configure(control));
        HoverToolTips.Install();
    }

    private static void Configure(Control control)
    {
        switch (control)
        {
            case ContentPresenter:
                AddBrushes(control, ContentPresenter.BackgroundProperty, ContentPresenter.BorderBrushProperty);
                break;
            case Border border when border.TemplatedParent is not null || border is GestureRackParameterSlider:
                AddBrushes(control, Border.BackgroundProperty, Border.BorderBrushProperty);
                break;
            case Shape shape when shape.TemplatedParent is not null:
                AddBrushes(control, Shape.FillProperty, Shape.StrokeProperty);
                break;
        }
    }

    internal static DoubleTransition OpacityFade(AvaloniaProperty property) => new()
    {
        Property = property, Duration = FadeDuration, Easing = new CubicEaseOut(),
    };

    private static void AddBrushes(Control control, params AvaloniaProperty[] properties)
    {
        // Never share mutable transition collections between controls.
        // Button/tab templates already have their own pressed-state transitions.
        // Other templates receive a fresh collection, never a shared mutable one.
        if (control.Transitions is not null) return;
        var transitions = control.Transitions = new Transitions();
        foreach (var property in properties)
            if (!transitions.OfType<BrushTransition>().Any(t => t.Property == property))
                transitions.Add(new BrushTransition { Property = property, Duration = FadeDuration, Easing = new CubicEaseOut() });
    }
}
