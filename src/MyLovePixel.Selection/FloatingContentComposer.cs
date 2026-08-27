using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Raster;
using MyLovePixel.Raster.Ink;

namespace MyLovePixel.Selection;

public static class FloatingContentComposer
{
    public static RasterPatch BuildMovePatch(
        PixelSurfaceSnapshot surface,
        SelectionMask selection,
        IntPoint delta,
        RasterWorkBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);
        if (delta == default || selection.IsEmpty) return RasterPatch.Empty;
        var floating = FloatingContent.Capture(surface, selection);
        var moved = FloatingContentTransforms.Translate(floating, delta);
        return BuildTransformPatch(surface, selection, moved, budget);
    }

    public static RasterPatch BuildTransformPatch(
        PixelSurfaceSnapshot surface,
        SelectionMask sourceSelection,
        FloatingContent transformed,
        RasterWorkBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(sourceSelection);
        ArgumentNullException.ThrowIfNull(transformed);
        if (surface.Size != sourceSelection.Size)
            throw new ArgumentException("Source selection size must match the surface.", nameof(sourceSelection));
        if (sourceSelection.IsEmpty) return RasterPatch.Empty;
        budget ??= RasterWorkBudget.Default;

        var final = new Dictionary<IntPoint, Rgba32>();
        var visited = 0;

        foreach (var point in sourceSelection.EnumerateSelected())
        {
            visited = checked(visited + 1);
            CheckVisited(budget, visited);
            var original = surface.GetPixel(point.X, point.Y);
            var coverage = sourceSelection.GetCoverage(point.X, point.Y);
            var remainderAlpha = ScaleByte(original.A, byte.MaxValue - coverage);
            final[point] = remainderAlpha == 0
                ? Rgba32.Transparent
                : new Rgba32(original.R, original.G, original.B, remainderAlpha);
        }

        for (var localY = 0; localY < transformed.Size.Height; localY++)
        for (var localX = 0; localX < transformed.Size.Width; localX++)
        {
            var coverage = transformed.Mask.GetCoverage(localX, localY);
            if (coverage == 0) continue;

            visited = checked(visited + 1);
            CheckVisited(budget, visited);
            var targetX = (long)transformed.Position.X + localX;
            var targetY = (long)transformed.Position.Y + localY;
            if ((ulong)targetX >= (ulong)surface.Size.Width || (ulong)targetY >= (ulong)surface.Size.Height) continue;

            var sourceColor = transformed.GetPixel(localX, localY);
            var selectedAlpha = ScaleByte(sourceColor.A, coverage);
            if (selectedAlpha == 0) continue;
            var selectedColor = new Rgba32(sourceColor.R, sourceColor.G, sourceColor.B, selectedAlpha);
            var target = new IntPoint((int)targetX, (int)targetY);
            var destination = final.TryGetValue(target, out var changed)
                ? changed
                : surface.GetPixel(target.X, target.Y);
            final[target] = AlphaCompositeInkStrategy.Instance.Apply(destination, selectedColor);
        }

        var writes = final
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Where(pair => surface.GetPixel(pair.Key.X, pair.Key.Y) != pair.Value)
            .Select(pair => new PixelWrite(pair.Key.X, pair.Key.Y, pair.Value))
            .ToArray();

        if (writes.Length > budget.MaxWrites)
            throw new RasterWorkBudgetExceededException(RasterBudgetKind.Writes, budget.MaxWrites, writes.Length);
        if (writes.Length == 0) return RasterPatch.Empty;

        var dirty = IntRect.FromPoint(writes[0].X, writes[0].Y);
        for (var index = 1; index < writes.Length; index++)
            dirty = IntRect.Union(dirty, IntRect.FromPoint(writes[index].X, writes[index].Y));
        return new RasterPatch(writes, dirty);
    }

    private static byte ScaleByte(int value, int factor) =>
        (byte)(((value * factor) + 127) / 255);

    private static void CheckVisited(RasterWorkBudget budget, int observed)
    {
        if (observed > budget.MaxVisitedPixels)
            throw new RasterWorkBudgetExceededException(RasterBudgetKind.VisitedPixels, budget.MaxVisitedPixels, observed);
    }
}
