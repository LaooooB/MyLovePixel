using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Application;

public static class ColorPickerMath
{
    public static (double Hue, double Saturation, double Value) ToHsv(Rgba32 color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var hue = delta == 0 ? 0 : max == r ? 60 * (((g - b) / delta) % 6)
            : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        return ((hue + 360) % 360, max == 0 ? 0 : delta / max, max);
    }

    public static Rgba32 FromHsv(double hue, double saturation, double value, byte alpha = 255)
    {
        if (!double.IsFinite(hue) || !double.IsFinite(saturation) || !double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(hue), "HSV components must be finite.");
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);
        var chroma = value * saturation;
        var h = hue / 60;
        var x = chroma * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = h switch
        {
            < 1 => (chroma, x, 0d),
            < 2 => (x, chroma, 0d),
            < 3 => (0d, chroma, x),
            < 4 => (0d, x, chroma),
            < 5 => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };
        var m = value - chroma;
        return new Rgba32((byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255), alpha);
    }
}
