using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Raster.Color;
using MyLovePixel.Raster.Ink;

namespace MyLovePixel.Raster.Fill;

public static class FloodFillRasterizer
{
    private const byte Unknown = 0;
    private const byte NoMatch = 1;
    private const byte Match = 2;
    private const byte Queued = 3;
    private const byte Filled = 4;

    public static RasterPatch BuildPatch(
        PixelSurfaceSnapshot surface,
        IntPoint seed,
        Rgba32 paint,
        IInkStrategy ink,
        IColorToleranceStrategy? tolerance = null,
        RasterWorkBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(ink);
        tolerance ??= ExactColorTolerance.Instance;
        budget ??= RasterWorkBudget.Default;

        if ((uint)seed.X >= (uint)surface.Size.Width || (uint)seed.Y >= (uint)surface.Size.Height)
            return RasterPatch.Empty;

        var width = surface.Size.Width;
        var height = surface.Size.Height;
        var states = new byte[checked(width * height)];
        var reference = surface.GetPixel(seed.X, seed.Y);
        var queue = new Queue<IntPoint>();
        var region = new List<IntPoint>();
        var visitedPixels = 0;

        bool IsMatch(int x, int y)
        {
            var index = (y * width) + x;
            switch (states[index])
            {
                case NoMatch:
                    return false;
                case Match:
                case Queued:
                case Filled:
                    return true;
            }

            visitedPixels = checked(visitedPixels + 1);
            RasterBudgetGuard.CheckVisited(budget, visitedPixels);
            var matches = tolerance.Matches(reference, surface.GetPixel(x, y));
            states[index] = matches ? Match : NoMatch;
            return matches;
        }

        void Enqueue(int x, int y)
        {
            var index = (y * width) + x;
            if (!IsMatch(x, y) || states[index] != Match) return;
            states[index] = Queued;
            queue.Enqueue(new IntPoint(x, y));
        }

        Enqueue(seed.X, seed.Y);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentIndex = (current.Y * width) + current.X;
            if (states[currentIndex] == Filled) continue;
            if (!IsMatch(current.X, current.Y)) continue;

            var left = current.X;
            while (left > 0)
            {
                var candidateIndex = (current.Y * width) + left - 1;
                if (states[candidateIndex] == Filled || !IsMatch(left - 1, current.Y)) break;
                left--;
            }

            var right = current.X;
            while (right < width - 1)
            {
                var candidateIndex = (current.Y * width) + right + 1;
                if (states[candidateIndex] == Filled || !IsMatch(right + 1, current.Y)) break;
                right++;
            }

            for (var x = left; x <= right; x++)
            {
                var index = (current.Y * width) + x;
                if (states[index] == Filled) continue;
                states[index] = Filled;
                region.Add(new IntPoint(x, current.Y));
            }

            ScanNeighborRow(current.Y - 1, left, right);
            ScanNeighborRow(current.Y + 1, left, right);
        }

        var patch = RasterPatchBuilder.Build(surface, region, paint, ink);
        RasterBudgetGuard.CheckWrites(budget, patch.Writes.Count);
        return patch;

        void ScanNeighborRow(int y, int left, int right)
        {
            if ((uint)y >= (uint)height) return;

            var runOpen = false;
            for (var x = left; x <= right; x++)
            {
                var index = (y * width) + x;
                if (states[index] == Filled)
                {
                    runOpen = false;
                    continue;
                }

                if (!IsMatch(x, y))
                {
                    runOpen = false;
                    continue;
                }

                if (!runOpen && states[index] == Match)
                {
                    states[index] = Queued;
                    queue.Enqueue(new IntPoint(x, y));
                }

                runOpen = true;
            }
        }
    }
}
