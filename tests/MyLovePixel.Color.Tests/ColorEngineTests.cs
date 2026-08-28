using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Color.Tests;

public sealed class ColorEngineTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Green = new(0, 255, 0, 255);
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);
    private static readonly Rgba32 Black = new(0, 0, 0, 255);
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    [Fact]
    public void MedianCut_IsDeterministic_AndReservesTransparentIndex()
    {
        var source = CreateRgbaSnapshot([Red, Red, Green, Blue, Rgba32.Transparent]);

        var first = MedianCutQuantizationStrategy.Instance.Quantize(source, maxColors: 3);
        var second = MedianCutQuantizationStrategy.Instance.Quantize(source, maxColors: 3);

        Assert.Equal((byte)0, first.TransparentIndex);
        Assert.InRange(first.Colors.Count, 2, 3);
        Assert.Equal(first.Colors.ToArray(), second.Colors.ToArray());
        Assert.Equal(first.Indices.ToArray(), second.Indices.ToArray());
        Assert.Equal((byte)0, first.Indices.Span[^1]);
    }

    [Fact]
    public void MedianCut_RejectsOneColorWhenTransparencyMustBeReservedAlongsideVisiblePixels()
    {
        var source = CreateRgbaSnapshot([Red, Rgba32.Transparent]);

        Assert.Throws<ArgumentException>(() =>
            MedianCutQuantizationStrategy.Instance.Quantize(source, maxColors: 1));
    }

    [Fact]
    public void CustomOrderedDitherMatrix_ChangesIdenticalGrayPixelsInDifferentCells()
    {
        var gray = new Rgba32(128, 128, 128, 255);
        var source = new PixelSurface(new IntSize(2, 1), gray).Snapshot();
        var palette = new Palette([Black, White]).Snapshot();
        var matrix = new OrderedDitherMatrix(2, 1, [0, 255]);

        var result = OrderedPaletteDitherStrategy.Instance.Dither(
            source,
            palette,
            new DitherOptions(matrix, strength: 255));

        Assert.Equal(new byte[] { 0, 1 }, result.Indices.ToArray());
    }

    [Fact]
    public void PaletteRemap_PreservesTransparentIntentAndMapsNearestColors()
    {
        var source = new Palette([Rgba32.Transparent, Red, Green], transparentIndex: 0).Snapshot();
        var target = new Palette([Green, Rgba32.Transparent, Red], transparentIndex: 1).Snapshot();

        var remap = PaletteRemapper.Build(source, target);

        Assert.Equal((byte)1, remap.Apply(0));
        Assert.Equal((byte)2, remap.Apply(1));
        Assert.Equal((byte)0, remap.Apply(2));
        Assert.Equal(new byte[] { 1, 2, 0 }, remap.Apply([0, 1, 2]));
    }

    [Fact]
    public void ColorRampShadingInk_ClampsWithinRampAndLeavesUnrelatedIndexUntouched()
    {
        var ramp = new ColorRamp([2, 4, 6, 8]);
        var lighter = new ColorRampShadingInk(ramp, stepDelta: 1);
        var darkest = new ColorRampShadingInk(ramp, stepDelta: -100);

        Assert.Equal((byte)6, lighter.Apply(4));
        Assert.Equal((byte)8, lighter.Apply(8));
        Assert.Equal((byte)2, darkest.Apply(6));
        Assert.Equal((byte)99, lighter.Apply(99));
    }

    private static PixelSurfaceSnapshot CreateRgbaSnapshot(IReadOnlyList<Rgba32> colors)
    {
        var document = PixelDocumentFactory.CreateBlank(colors.Count, 1);
        var cel = document.Cels.Single();
        var writes = colors
            .Select((color, x) => new PixelWrite(x, 0, color))
            .ToArray();
        new CommandBus(document).Execute(new PixelPatchCommand(cel.SurfaceId, writes));
        return document.Resources.GetSurface(cel.SurfaceId).Snapshot();
    }
}
