using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Raster;
using MyLovePixel.Raster.Brush;
using MyLovePixel.Raster.Color;
using MyLovePixel.Raster.Fill;
using MyLovePixel.Raster.Geometry;
using MyLovePixel.Raster.Ink;
using MyLovePixel.Raster.Strokes;
using Xunit;

namespace MyLovePixel.Raster.Tests;

public sealed class RasterCoreTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Green = new(0, 255, 0, 255);
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    [Fact]
    public void LineRasterizer_MatchesGoldenPixels()
    {
        var points = LineRasterizer.Rasterize(new IntPoint(0, 0), new IntPoint(4, 2));
        Assert.Equal("#....\n.##..\n...##", Render(points, 5, 3));
    }

    [Fact]
    public void RectangleRasterizer_MatchesGoldenPixels()
    {
        var points = RectangleRasterizer.Rasterize(new IntRect(0, 0, 5, 4), filled: false);
        Assert.Equal("#####\n#...#\n#...#\n#####", Render(points, 5, 4));
    }

    [Fact]
    public void EllipseRasterizer_MatchesGoldenPixels()
    {
        var points = EllipseRasterizer.Rasterize(new IntRect(0, 0, 5, 5), filled: false);
        Assert.Equal(".###.\n#...#\n#...#\n#...#\n.###.", Render(points, 5, 5));
    }

    [Fact]
    public void FilledPolygonRasterizer_MatchesGoldenPixels()
    {
        IntPoint[] vertices = [new(0, 4), new(2, 0), new(4, 4)];
        var points = PolygonRasterizer.Rasterize(vertices, filled: true);
        Assert.Equal("..#..\n.###.\n.###.\n#####\n#####", Render(points, 5, 5));
    }

    [Fact]
    public void BrushStroke_SpacingKeepsStartAndFinalEndpoint()
    {
        IntPoint[] samples = [new(0, 0), new(7, 0)];
        var points = BrushStrokeRasterizer.Rasterize(samples, BrushMask.SinglePixel, spacingPixels: 3);
        Assert.Equal(new[] { new IntPoint(0, 0), new IntPoint(3, 0), new IntPoint(6, 0), new IntPoint(7, 0) }, points);
    }

    [Fact]
    public void PixelPerfectFilter_RemovesRedundantOrthogonalCorner()
    {
        IntPoint[] input = [new(0, 0), new(1, 0), new(1, 1)];
        var result = PixelPerfectStrokeFilter.Instance.Filter(input);
        Assert.Equal(new[] { new IntPoint(0, 0), new IntPoint(1, 1) }, result);
    }

    [Fact]
    public void FloodFill_FillsOnlyConnectedRegion_AndReportsDirtyRegion()
    {
        var snapshot = CreateSnapshot(
            "..#..",
            "..#..",
            "#####",
            "..#..",
            "..#..");

        var patch = FloodFillRasterizer.BuildPatch(
            snapshot,
            new IntPoint(0, 0),
            Red,
            SimpleInkStrategy.Instance);

        Assert.Equal(4, patch.Writes.Count);
        Assert.Equal(new IntRect(0, 0, 2, 2), patch.DirtyRegion);
        Assert.Equal(
            new[] { new IntPoint(0, 0), new IntPoint(1, 0), new IntPoint(0, 1), new IntPoint(1, 1) },
            patch.Writes.Select(write => new IntPoint(write.X, write.Y)).OrderBy(p => p.Y).ThenBy(p => p.X));
    }

    [Fact]
    public void FloodFill_StopsWithStructuredBudgetError()
    {
        var snapshot = CreateSnapshot(
            "........",
            "........",
            "........",
            "........",
            "........",
            "........",
            "........",
            "........");

        var exception = Assert.Throws<RasterWorkBudgetExceededException>(() =>
            FloodFillRasterizer.BuildPatch(
                snapshot,
                new IntPoint(0, 0),
                Red,
                SimpleInkStrategy.Instance,
                budget: new RasterWorkBudget(maxVisitedPixels: 3, maxWrites: 64)));

        Assert.Equal(RasterBudgetKind.VisitedPixels, exception.Kind);
        Assert.Equal(3, exception.Limit);
    }

    [Fact]
    public void ReplaceColor_ReplacesAllMatchingPixels_NotOnlyConnectedPixels()
    {
        var snapshot = CreateSnapshot(
            "r.r",
            ".r.",
            "r.r");

        var patch = ReplaceColorRasterizer.BuildPatch(
            snapshot,
            Red,
            Green,
            SimpleInkStrategy.Instance);

        Assert.Equal(5, patch.Writes.Count);
        Assert.Equal(new IntRect(0, 0, 3, 3), patch.DirtyRegion);
        Assert.All(patch.Writes, write => Assert.Equal(Green, write.Color));
    }

    [Fact]
    public void ToleranceStrategy_CanIgnoreSmallChannelDifference()
    {
        var tolerance = new MaxChannelColorTolerance(2);
        Assert.True(tolerance.Matches(new Rgba32(100, 110, 120, 200), new Rgba32(102, 109, 118, 199)));
        Assert.False(tolerance.Matches(new Rgba32(100, 110, 120, 200), new Rgba32(103, 110, 120, 200)));
    }

    [Fact]
    public void AlphaCompositeInk_UsesDeterministicIntegerBlend()
    {
        var destination = new Rgba32(0, 0, 255, 255);
        var paint = new Rgba32(255, 0, 0, 128);
        Assert.Equal(new Rgba32(128, 0, 127, 255), AlphaCompositeInkStrategy.Instance.Apply(destination, paint));
    }

    [Fact]
    public void RasterPatchBuilder_DoesNotMutateLiveSurface_AndIntegratesWithCommandBus()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        var patch = RasterPatchBuilder.Build(
            surface.Snapshot(),
            [new IntPoint(2, 1)],
            Red,
            SimpleInkStrategy.Instance);

        Assert.Equal(Rgba32.Transparent, surface.GetPixel(2, 1));
        Assert.Equal(new IntRect(2, 1, 1, 1), patch.DirtyRegion);

        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(cel.SurfaceId, patch.Writes));
        Assert.Equal(Red, surface.GetPixel(2, 1));
        bus.Undo();
        Assert.Equal(Rgba32.Transparent, surface.GetPixel(2, 1));
    }

    private static PixelSurfaceSnapshot CreateSnapshot(params string[] rows)
    {
        if (rows.Length == 0) throw new ArgumentException("At least one row is required.", nameof(rows));
        var width = rows[0].Length;
        if (width == 0 || rows.Any(row => row.Length != width))
            throw new ArgumentException("Rows must be non-empty and have equal width.", nameof(rows));

        var document = PixelDocumentFactory.CreateBlank(width, rows.Length);
        var cel = document.Cels.Single();
        var writes = new List<PixelWrite>();
        for (var y = 0; y < rows.Length; y++)
        for (var x = 0; x < width; x++)
        {
            var color = rows[y][x] switch
            {
                '.' => Rgba32.Transparent,
                '#' => Black,
                'r' => Red,
                _ => throw new ArgumentException($"Unsupported fixture pixel '{rows[y][x]}'.", nameof(rows)),
            };
            if (color != Rgba32.Transparent) writes.Add(new PixelWrite(x, y, color));
        }

        if (writes.Count > 0)
        {
            var bus = new CommandBus(document);
            bus.Execute(new PixelPatchCommand(cel.SurfaceId, writes));
        }

        return document.Resources.GetSurface(cel.SurfaceId).Snapshot();
    }

    private static string Render(IEnumerable<IntPoint> points, int width, int height)
    {
        var set = points.ToHashSet();
        var rows = new string[height];
        for (var y = 0; y < height; y++)
        {
            var chars = new char[width];
            for (var x = 0; x < width; x++) chars[x] = set.Contains(new IntPoint(x, y)) ? '#' : '.';
            rows[y] = new string(chars);
        }
        return string.Join('\n', rows);
    }
}
