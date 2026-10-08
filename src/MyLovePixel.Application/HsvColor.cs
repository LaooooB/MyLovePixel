using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Application;

public readonly record struct HsvColor(double Hue, double Saturation, double Value)
{
    public static HsvColor FromColor(Rgba32 color)
    {
        var r = color.R / 255d; var g = color.G / 255d; var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b)); var delta = max - min;
        var hue = delta == 0 ? 0 : max == r ? 60 * ((g - b) / delta % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        return new HsvColor(hue < 0 ? hue + 360 : hue, max == 0 ? 0 : delta / max, max);
    }
    public Rgba32 ToColor(byte alpha = 255)
    {
        if (!double.IsFinite(Hue) || !double.IsFinite(Saturation) || !double.IsFinite(Value)) throw new ArgumentException("Color coordinates must be finite.");
        var hue = (Hue % 360 + 360) % 360 / 60;
        var value = Math.Clamp(Value, 0, 1); var c = value * Math.Clamp(Saturation, 0, 1);
        var x = c * (1 - Math.Abs(hue % 2 - 1)); var m = value - c;
        var (r, g, b) = hue switch { < 1 => (c, x, 0d), < 2 => (x, c, 0d), < 3 => (0d, c, x), < 4 => (0d, x, c), < 5 => (x, 0d, c), _ => (c, 0d, x) };
        static byte Channel(double v) => (byte)Math.Clamp(Math.Round(v * 255), 0, 255);
        return new Rgba32(Channel(r + m), Channel(g + m), Channel(b + m), alpha);
    }
}
