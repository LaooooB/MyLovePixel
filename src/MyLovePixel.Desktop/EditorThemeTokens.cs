using Avalonia;
using Avalonia.Media;

namespace MyLovePixel.Desktop;

public static class EditorThemeTokens
{
    // Gesture-rack inspired palette: near-black graphite surfaces, restrained
    // olive/green undertones, and a single mint accent for active interaction.
    public static IBrush AppBackground { get; } = Rgb(12, 15, 13);
    public static IBrush Surface { get; } = Rgb(17, 21, 19);
    public static IBrush SurfaceRaised { get; } = Rgb(23, 28, 25);
    public static IBrush SurfaceHover { get; } = Rgb(30, 37, 33);
    public static IBrush SurfaceSelected { get; } = Rgb(22, 50, 42);
    public static IBrush PanelBorder { get; } = Rgb(42, 50, 45);
    public static IBrush StrongBorder { get; } = Rgb(68, 80, 73);

    public static IBrush TextPrimary { get; } = Rgb(235, 240, 236);
    public static IBrush TextSecondary { get; } = Rgb(165, 176, 168);
    public static IBrush TextMuted { get; } = Rgb(108, 120, 112);

    public static IBrush Accent { get; } = Rgb(91, 218, 176);
    public static IBrush AccentHover { get; } = Rgb(119, 232, 195);
    public static IBrush AccentForeground { get; } = Rgb(8, 25, 19);
    public static IBrush Danger { get; } = Rgb(232, 101, 111);
    public static IBrush Warning { get; } = Rgb(215, 183, 104);

    public static IBrush CanvasWorkspace { get; } = Rgb(67, 72, 68);
    public static IBrush CanvasFrame { get; } = Rgb(222, 226, 221);
    public static IBrush PreviewBackground { get; } = Rgb(255, 255, 255);
    public static IBrush CheckerLight { get; } = Rgb(242, 244, 241);
    public static IBrush CheckerDark { get; } = Rgb(211, 216, 212);
    public static IBrush GridLine { get; } = Rgba(82, 91, 85, 70);
    public static IBrush HoverCell { get; } = Rgba(91, 218, 176, 58);
    public static IBrush HoverCellOutline { get; } = Accent;
    public static IBrush SelectionFill { get; } = Rgba(91, 218, 176, 32);
    public static IBrush SelectionOutline { get; } = Accent;
    public static IBrush DirtyRegionOutline { get; } = Danger;

    public static CornerRadius ControlRadius { get; } = new(5);
    public static CornerRadius CardRadius { get; } = new(7);

    public const double CompactSpacing = 4d;
    public const double ControlSpacing = 6d;
    public const double PanelSpacing = 10d;
    public const double ShellPadding = 10d;
    public const double ToolRailWidth = 64d;
    public const double RightPanelWidth = 380d;
    public const double TimelineHeight = 148d;

    private static IBrush Rgb(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));
    private static IBrush Rgba(byte r, byte g, byte b, byte a) => new SolidColorBrush(Color.FromArgb(a, r, g, b));
}
