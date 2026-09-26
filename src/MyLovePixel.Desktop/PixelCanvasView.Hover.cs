using Avalonia;
using Avalonia.Controls;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Desktop;

public sealed partial class PixelCanvasView
{
    public static readonly StyledProperty<double> HoverOpacityProperty =
        AvaloniaProperty.Register<PixelCanvasView, double>(nameof(HoverOpacity));
    private IntPoint? _hoverVisualPixel;
    private bool _hoverVisible;
    public double HoverOpacity => GetValue(HoverOpacityProperty);

    static PixelCanvasView() => AffectsRender<PixelCanvasView>(HoverOpacityProperty);

    private void UpdateHoverAppearance(IntPoint? pixel)
    {
        // Position follows the pointer immediately; only entrance/exit opacity fades.
        if (pixel is not null) _hoverVisualPixel = pixel;
        var visible = pixel is not null;
        if (_hoverVisible == visible) return;
        _hoverVisible = visible;
        SetValue(HoverOpacityProperty, visible ? 1d : 0d);
    }
}
