using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Raster.Ink;

public interface IInkStrategy
{
    Rgba32 Apply(Rgba32 destination, Rgba32 paint);
}

public sealed class SimpleInkStrategy : IInkStrategy
{
    public static SimpleInkStrategy Instance { get; } = new();

    private SimpleInkStrategy()
    {
    }

    public Rgba32 Apply(Rgba32 destination, Rgba32 paint) => paint;
}

public sealed class AlphaCompositeInkStrategy : IInkStrategy
{
    public static AlphaCompositeInkStrategy Instance { get; } = new();

    private AlphaCompositeInkStrategy()
    {
    }

    public Rgba32 Apply(Rgba32 destination, Rgba32 paint)
    {
        if (paint.A == 0) return destination;
        if (paint.A == byte.MaxValue || destination.A == 0) return paint;

        var inverseSourceAlpha = byte.MaxValue - paint.A;
        var alphaNumerator = (paint.A * 255) + (destination.A * inverseSourceAlpha);
        if (alphaNumerator == 0) return Rgba32.Transparent;

        var alpha = DivideRounded(alphaNumerator, 255);
        return new Rgba32(
            BlendStraightChannel(destination.R, destination.A, paint.R, paint.A, inverseSourceAlpha, alphaNumerator),
            BlendStraightChannel(destination.G, destination.A, paint.G, paint.A, inverseSourceAlpha, alphaNumerator),
            BlendStraightChannel(destination.B, destination.A, paint.B, paint.A, inverseSourceAlpha, alphaNumerator),
            (byte)alpha);
    }

    private static byte BlendStraightChannel(
        byte destinationChannel,
        byte destinationAlpha,
        byte sourceChannel,
        byte sourceAlpha,
        int inverseSourceAlpha,
        int alphaNumerator)
    {
        var numerator = ((long)sourceChannel * sourceAlpha * 255) +
                        ((long)destinationChannel * destinationAlpha * inverseSourceAlpha);
        return (byte)DivideRounded(numerator, alphaNumerator);
    }

    private static int DivideRounded(long numerator, long denominator) =>
        checked((int)((numerator + (denominator / 2)) / denominator));
}

public sealed class LockAlphaInkStrategy : IInkStrategy
{
    public static LockAlphaInkStrategy Instance { get; } = new();

    private LockAlphaInkStrategy()
    {
    }

    public Rgba32 Apply(Rgba32 destination, Rgba32 paint)
    {
        if (destination.A == 0 || paint.A == 0) return destination;
        if (paint.A == byte.MaxValue) return new Rgba32(paint.R, paint.G, paint.B, destination.A);

        var inverse = byte.MaxValue - paint.A;
        return new Rgba32(
            Blend(destination.R, paint.R, paint.A, inverse),
            Blend(destination.G, paint.G, paint.A, inverse),
            Blend(destination.B, paint.B, paint.A, inverse),
            destination.A);
    }

    private static byte Blend(byte destination, byte source, byte sourceAlpha, int inverseSourceAlpha) =>
        (byte)(((source * sourceAlpha) + (destination * inverseSourceAlpha) + 127) / 255);
}
