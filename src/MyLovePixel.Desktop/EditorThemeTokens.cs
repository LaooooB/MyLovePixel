using Avalonia.Media;

namespace MyLovePixel.Desktop;

public static class EditorThemeTokens
{
    public static IBrush CanvasBackground { get; } = new SolidColorBrush(Color.FromRgb(28, 28, 31));
    public static IBrush CheckerLight { get; } = new SolidColorBrush(Color.FromRgb(92, 92, 98));
    public static IBrush CheckerDark { get; } = new SolidColorBrush(Color.FromRgb(66, 66, 72));
    public static IBrush PanelBorder { get; } = new SolidColorBrush(Color.FromRgb(70, 70, 76));
    public static IBrush PreviewOutline { get; } = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
    public static IBrush DirtyRegionOutline { get; } = new SolidColorBrush(Color.FromArgb(235, 255, 84, 84));

    public const double PanelSpacing = 8d;
    public const double CompactSpacing = 4d;
    public const double LeftPanelWidth = 220d;
    public const double RightPanelWidth = 280d;
    public const double TimelineHeight = 128d;
}
