using Avalonia;
using Avalonia.Media;

namespace MyLovePixel.Desktop;

public static class EditorThemeTokens
{
    public static IBrush AppBackground { get; } = Rgb(12, 13, 16);
    public static IBrush Surface { get; } = Rgb(18, 20, 25);
    public static IBrush SurfaceRaised { get; } = Rgb(24, 27, 34);
    public static IBrush SurfaceHover { get; } = Rgb(31, 36, 44);
    public static IBrush SurfaceSelected { get; } = Rgb(23, 50, 47);
    public static IBrush PanelBorder { get; } = Rgb(43, 48, 57);
    public static IBrush StrongBorder { get; } = Rgb(68, 77, 89);

    public static IBrush TextPrimary { get; } = Rgb(236, 241, 247);
    public static IBrush TextSecondary { get; } = Rgb(162, 172, 185);
    public static IBrush TextMuted { get; } = Rgb(111, 120, 132);

    public static IBrush Accent { get; } = Rgb(99, 230, 205);
    public static IBrush AccentHover { get; } = Rgb(129, 239, 219);
    public static IBrush AccentForeground { get; } = Rgb(6, 24, 22);
    public static IBrush Danger { get; } = Rgb(255, 105, 121);
    public static IBrush Warning { get; } = Rgb(241, 196, 95);

    public static IBrush CanvasWorkspace { get; } = Rgb(92, 95, 101);
    public static IBrush CanvasFrame { get; } = Rgb(226, 228, 231);
    public static IBrush CheckerLight { get; } = Rgb(246, 246, 246);
    public static IBrush CheckerDark { get; } = Rgb(216, 218, 221);
    public static IBrush GridLine { get; } = Rgba(92, 98, 105, 68);
    public static IBrush HoverCell { get; } = Rgba(99, 230, 205, 62);
    public static IBrush HoverCellOutline { get; } = Accent;
    public static IBrush SelectionFill { get; } = Rgba(99, 230, 205, 34);
    public static IBrush SelectionOutline { get; } = Accent;
    public static IBrush DirtyRegionOutline { get; } = Danger;

    public static CornerRadius ControlRadius { get; } = new(5);
    public static CornerRadius CardRadius { get; } = new(7);

    public const double CompactSpacing = 4d;
    public const double ControlSpacing = 6d;
    public const double PanelSpacing = 8d;
    public const double ShellPadding = 8d;
    public const double ToolRailWidth = 52d;
    public const double RightPanelWidth = 310d;
    public const double TimelineHeight = 124d;

    private static IBrush Rgb(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));
    private static IBrush Rgba(byte r, byte g, byte b, byte a) => new SolidColorBrush(Color.FromArgb(a, r, g, b));
}
