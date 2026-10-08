using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Application;

/// <summary>View preferences only. Never applied to a document, palette or export.</summary>
public sealed record CanvasDisplaySettings
{
    public const int DefaultBrightness = 60;
    public const int TooltipDelayMilliseconds = 750;
    public const int TooltipBetweenShowDelayMilliseconds = 0;
    public const double CheckerCellSize = 12d;

    public CanvasDisplaySettings(int backgroundBrightness = DefaultBrightness)
    {
        if (backgroundBrightness is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(backgroundBrightness));
        BackgroundBrightness = backgroundBrightness;
    }

    public int BackgroundBrightness { get; }
    public Rgba32 CheckerLight => Gray((byte)(190 + (BackgroundBrightness + 1) / 2));
    public Rgba32 CheckerDark => Gray((byte)(CheckerLight.R - 12));
    public Rgba32 Frame => Gray((byte)(CheckerLight.R - 6));

    private static Rgba32 Gray(byte value) => new(value, value, value, 255);

    public static byte[] CopyRgbaForDisplay(ReadOnlySpan<byte> rgba, bool invert = false)
    {
        if (rgba.Length % 4 != 0) throw new ArgumentException("Expected RGBA pixels.", nameof(rgba));
        var copy = rgba.ToArray();
        if (invert)
            for (var i = 0; i < copy.Length; i += 4)
            {
                copy[i] = (byte)(255 - copy[i]);
                copy[i + 1] = (byte)(255 - copy[i + 1]);
                copy[i + 2] = (byte)(255 - copy[i + 2]);
            }
        return copy;
    }
}
