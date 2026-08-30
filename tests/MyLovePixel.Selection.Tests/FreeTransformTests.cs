using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Selection.Tests;

public sealed class FreeTransformTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Green = new(0, 255, 0, 255);

    [Fact]
    public void RotateNearest_NinetyDegrees_PreservesPixelOrder()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 1);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(cel.SurfaceId,
        [
            new PixelWrite(0, 0, Red),
            new PixelWrite(1, 0, Green),
        ]));
        var surface = document.Resources.GetSurface(cel.SurfaceId).Snapshot();
        var selection = SelectionFactory.Rectangle(surface.Size, new IntRect(0, 0, 2, 1));
        var floating = FloatingContent.Capture(surface, selection);

        var rotated = FloatingContentTransforms.RotateNearest(floating, 90d);

        Assert.Equal(new IntSize(1, 2), rotated.Size);
        Assert.Equal(Red, rotated.GetPixel(0, 0));
        Assert.Equal(Green, rotated.GetPixel(0, 1));
        Assert.True(rotated.Mask.IsSelected(0, 0));
        Assert.True(rotated.Mask.IsSelected(0, 1));
    }

    [Fact]
    public void RotateNearest_ArbitraryAngle_ExpandsAndKeepsSelectedPixels()
    {
        var document = PixelDocumentFactory.CreateBlank(3, 3);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(cel.SurfaceId,
        [
            new PixelWrite(1, 0, Red),
            new PixelWrite(0, 1, Red),
            new PixelWrite(1, 1, Red),
            new PixelWrite(2, 1, Red),
            new PixelWrite(1, 2, Red),
        ]));
        var surface = document.Resources.GetSurface(cel.SurfaceId).Snapshot();
        var selection = SelectionFactory.Rectangle(surface.Size, new IntRect(0, 0, 3, 3));
        var floating = FloatingContent.Capture(surface, selection);

        var rotated = FloatingContentTransforms.RotateNearest(floating, 45d);

        Assert.True(rotated.Size.Width > floating.Size.Width);
        Assert.True(rotated.Size.Height > floating.Size.Height);
        Assert.False(rotated.Mask.IsEmpty);
        Assert.Contains(
            Enumerable.Range(0, rotated.Size.Height)
                .SelectMany(y => Enumerable.Range(0, rotated.Size.Width).Select(x => rotated.GetPixel(x, y))),
            color => color == Red);
    }

    [Fact]
    public void Place_ChangesOnlyFloatingPosition()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, Red)]));
        var surface = document.Resources.GetSurface(cel.SurfaceId).Snapshot();
        var selection = SelectionFactory.Rectangle(surface.Size, new IntRect(0, 0, 1, 1));
        var floating = FloatingContent.Capture(surface, selection);

        var placed = FloatingContentTransforms.Place(floating, new IntPoint(7, -3));

        Assert.Equal(new IntPoint(7, -3), placed.Position);
        Assert.Equal(floating.Size, placed.Size);
        Assert.Equal(Red, placed.GetPixel(0, 0));
        Assert.Equal(floating.Mask.GetCoverage(0, 0), placed.Mask.GetCoverage(0, 0));
    }
}
