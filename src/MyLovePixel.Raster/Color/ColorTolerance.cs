using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Raster.Color;

public interface IColorToleranceStrategy
{
    bool Matches(Rgba32 reference, Rgba32 candidate);
}

public sealed class ExactColorTolerance : IColorToleranceStrategy
{
    public static ExactColorTolerance Instance { get; } = new();

    private ExactColorTolerance()
    {
    }

    public bool Matches(Rgba32 reference, Rgba32 candidate) => reference == candidate;
}

public sealed class MaxChannelColorTolerance(byte tolerance, bool includeAlpha = true) : IColorToleranceStrategy
{
    public byte Tolerance { get; } = tolerance;
    public bool IncludeAlpha { get; } = includeAlpha;

    public bool Matches(Rgba32 reference, Rgba32 candidate) =>
        Difference(reference.R, candidate.R) <= Tolerance &&
        Difference(reference.G, candidate.G) <= Tolerance &&
        Difference(reference.B, candidate.B) <= Tolerance &&
        (!IncludeAlpha || Difference(reference.A, candidate.A) <= Tolerance);

    private static int Difference(byte a, byte b) => Math.Abs(a - b);
}
