using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Raster.Ink;

namespace MyLovePixel.Raster.Color;

public static class ReplaceColorRasterizer
{
    public static RasterPatch BuildPatch(
        PixelSurfaceSnapshot surface,
        Rgba32 reference,
        Rgba32 paint,
        IInkStrategy ink,
        IColorToleranceStrategy? tolerance = null,
        RasterWorkBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(ink);
        tolerance ??= ExactColorTolerance.Instance;
        budget ??= RasterWorkBudget.Default;

        var points = new List<IntPoint>();
        var visitedPixels = 0;

        for (var y = 0; y < surface.Size.Height; y++)
        for (var x = 0; x < surface.Size.Width; x++)
        {
            visitedPixels = checked(visitedPixels + 1);
            RasterBudgetGuard.CheckVisited(budget, visitedPixels);
            if (tolerance.Matches(reference, surface.GetPixel(x, y)))
                points.Add(new IntPoint(x, y));
        }

        var patch = RasterPatchBuilder.Build(surface, points, paint, ink);
        RasterBudgetGuard.CheckWrites(budget, patch.Writes.Count);
        return patch;
    }
}
