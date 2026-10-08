using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Application;

/// <summary>Picker coordinates: hue in degrees, saturation/value in [0,1], straight alpha.</summary>
public readonly record struct HsvColor(double Hue, double Saturation, double Value, byte Alpha = 255)
{
    public static HsvColor FromRgba(Rgba32 color)
    {
        var r = color.R / 255d; var g = color.G / 255d; var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var hue = delta == 0 ? 0 : max == r ? 60 * ((g - b) / delta % 6) :
            max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        return new((hue + 360) % 360, max == 0 ? 0 : delta / max, max, color.A);
    }

    public Rgba32 ToRgba()
    {
        if (!double.IsFinite(Hue) || !double.IsFinite(Saturation) || !double.IsFinite(Value))
            throw new ArgumentOutOfRangeException(nameof(Hue), "Picker coordinates must be finite.");
        var h = ((Hue % 360) + 360) % 360 / 60;
        var value = Math.Clamp(Value, 0, 1);
        var c = value * Math.Clamp(Saturation, 0, 1);
        var x = c * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = h switch
        {
            < 1 => (c, x, 0d), < 2 => (x, c, 0d), < 3 => (0d, c, x),
            < 4 => (0d, x, c), < 5 => (x, 0d, c), _ => (c, 0d, x),
        };
        var m = value - c;
        return new((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255), Alpha);
    }
}
