namespace MyLovePixel.Desktop;

internal static class Color
{
    public static Avalonia.Media.Color FromRgb(byte r, byte g, byte b) =>
        Avalonia.Media.Color.FromRgb(r, g, b);

    public static Avalonia.Media.Color FromArgb(byte a, byte r, byte g, byte b) =>
        Avalonia.Media.Color.FromArgb(a, r, g, b);
}
