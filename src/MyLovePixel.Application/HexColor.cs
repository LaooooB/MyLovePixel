using System.Globalization;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Application;

public static class HexColor
{
    public static bool TryParse(string? text, out Rgba32 color)
    {
        color = default;
        var value = (text ?? string.Empty).Trim();
        if (value.StartsWith('#')) value = value[1..];
        if (value.Length is not (6 or 8) || value.Any(c => !Uri.IsHexDigit(c))) return false;
        if (!uint.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var packed)) return false;
        color = value.Length == 6
            ? new Rgba32((byte)(packed >> 16), (byte)(packed >> 8), (byte)packed, 255)
            : new Rgba32((byte)(packed >> 24), (byte)(packed >> 16), (byte)(packed >> 8), (byte)packed);
        return true;
    }

    public static string Format(Rgba32 color) => color.A == 255
        ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
        : $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
}
