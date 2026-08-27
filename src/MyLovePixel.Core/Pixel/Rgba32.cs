namespace MyLovePixel.Core.Pixel;

public readonly record struct Rgba32(byte R, byte G, byte B, byte A = byte.MaxValue)
{
    public static Rgba32 Transparent => new(0, 0, 0, 0);
}
