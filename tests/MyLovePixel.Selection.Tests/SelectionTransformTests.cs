using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Raster.Color;
using MyLovePixel.Selection;
using Xunit;

namespace MyLovePixel.Selection.Tests;

public sealed class SelectionTransformTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Green = new(0, 255, 0, 255);

    [Fact]
    public void Bit1Mask_IsActuallyBinary_AndTracksBounds()
    {
        var mask = SelectionMask.FromCoverage(
            new IntSize(4, 1),
            SelectionMaskFormat.Bit1,
            new byte[] { 1, 0, 7, 0 });

        Assert.Equal(2, mask.SelectedPixelCount);
        Assert.Equal(byte.MaxValue, mask.GetCoverage(0, 0));
        Assert.Equal((byte)0, mask.GetCoverage(1, 0));
        Assert.Equal(byte.MaxValue, mask.GetCoverage(2, 0));
        Assert.Equal(new IntRect(0, 0, 3, 1), mask.Bounds);
    }

    [Fact]
    public void Alpha8Mask_PreservesCoverage_AndCombinesWithAlphaMath()
    {
        var size = new IntSize(1, 1);
        var a = SelectionMask.FromCoverage(size, SelectionMaskFormat.Alpha8, new byte[] { 128 });
        var b = SelectionMask.FromCoverage(size, SelectionMaskFormat.Alpha8, new byte[] { 128 });

        Assert.Equal((byte)192, SelectionMaskOperations.Combine(a, b, SelectionCombineMode.Add).GetCoverage(0, 0));
        Assert.Equal((byte)64, SelectionMaskOperations.Combine(a, b, SelectionCombineMode.Intersect).GetCoverage(0, 0));
        Assert.Equal((byte)64, SelectionMaskOperations.Combine(a, b, SelectionCombineMode.Subtract).GetCoverage(0, 0));
        Assert.Equal((byte)127, SelectionMaskOperations.Invert(a).GetCoverage(0, 0));
    }

    [Fact]
    public void ShapeFactories_ClipToCanvas_AndRemainTransient()
    {
        var document = PixelDocumentFactory.CreateBlank(5, 5);
        var surface = document.Resources.GetSurface(document.Cels.Single().SurfaceId);
        var revision = surface.Revision;

        var rectangle = SelectionFactory.Rectangle(new IntSize(5, 5), new IntRect(-1, 1, 3, 2));
        var ellipse = SelectionFactory.Ellipse(new IntSize(5, 5), new IntRect(1, 1, 3, 3));
        IntPoint[] lassoVertices = [new(0, 4), new(2, 0), new(4, 4)];
        var lasso = SelectionFactory.Lasso(new IntSize(5, 5), lassoVertices);

        Assert.Equal(4, rectangle.SelectedPixelCount);
        Assert.False(ellipse.IsEmpty);
        Assert.False(lasso.IsEmpty);
        Assert.Equal(revision, surface.Revision);
    }

    [Fact]
    public void SelectByColor_FindsDisconnectedMatches()
    {
        var document = PixelDocumentFactory.CreateBlank(3, 1);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(cel.SurfaceId,
        [
            new PixelWrite(0, 0, Red),
            new PixelWrite(2, 0, Red),
        ]));

        var mask = SelectionFactory.ByColor(
            document.Resources.GetSurface(cel.SurfaceId).Snapshot(),
            Red,
            ExactColorTolerance.Instance);

        Assert.Equal(2, mask.SelectedPixelCount);
        Assert.True(mask.IsSelected(0, 0));
        Assert.False(mask.IsSelected(1, 0));
        Assert.True(mask.IsSelected(2, 0));
    }

    [Fact]
    public void MoveSelection_ChangesMaskOnly_NotPixels()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 2);
        var surface = document.Resources.GetSurface(document.Cels.Single().SurfaceId);
        var originalRevision = surface.Revision;
        var mask = SelectionFactory.Rectangle(surface.Size, new IntRect(0, 0, 1, 1));

        var moved = SelectionTransforms.Translate(mask, new IntPoint(2, 1));

        Assert.False(moved.IsSelected(0, 0));
        Assert.True(moved.IsSelected(2, 1));
        Assert.Equal(originalRevision, surface.Revision);
        Assert.Equal(Rgba32.Transparent, surface.GetPixel(0, 0));
    }

    [Fact]
    public void MoveContent_IsPreviewOnlyUntilCommand_AndUndoRestoresSource()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 1);
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, Red)]));
        var undoCountBeforeTransform = bus.UndoCount;
        var selection = SelectionFactory.Rectangle(surface.Size, new IntRect(0, 0, 1, 1));

        var patch = FloatingContentComposer.BuildMovePatch(surface.Snapshot(), selection, new IntPoint(1, 0));

        Assert.Equal(Red, surface.GetPixel(0, 0));
        Assert.Equal(Rgba32.Transparent, surface.GetPixel(1, 0));
        Assert.Equal(undoCountBeforeTransform, bus.UndoCount);

        bus.Execute(new PixelPatchCommand(cel.SurfaceId, patch.Writes, "Move Content"));
        Assert.Equal(Rgba32.Transparent, surface.GetPixel(0, 0));
        Assert.Equal(Red, surface.GetPixel(1, 0));

        bus.Undo();
        Assert.Equal(Red, surface.GetPixel(0, 0));
        Assert.Equal(Rgba32.Transparent, surface.GetPixel(1, 0));
    }

    [Fact]
    public void FloatingContent_FlipRotateAndScaleUseNearestPixelSemantics()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 1);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(cel.SurfaceId,
        [
            new PixelWrite(0, 0, Red),
            new PixelWrite(1, 0, Green),
        ]));
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        var selection = SelectionFactory.Rectangle(surface.Size, new IntRect(0, 0, 2, 1));
        var content = FloatingContent.Capture(surface.Snapshot(), selection);

        var flipped = FloatingContentTransforms.FlipHorizontal(content);
        Assert.Equal(Green, flipped.GetPixel(0, 0));
        Assert.Equal(Red, flipped.GetPixel(1, 0));

        var rotated = FloatingContentTransforms.Rotate90(content, QuarterTurn.Clockwise);
        Assert.Equal(new IntSize(1, 2), rotated.Size);
        Assert.Equal(Red, rotated.GetPixel(0, 0));
        Assert.Equal(Green, rotated.GetPixel(0, 1));

        var scaled = FloatingContentTransforms.ScaleNearest(content, new IntSize(4, 1));
        Assert.Equal(Red, scaled.GetPixel(0, 0));
        Assert.Equal(Red, scaled.GetPixel(1, 0));
        Assert.Equal(Green, scaled.GetPixel(2, 0));
        Assert.Equal(Green, scaled.GetPixel(3, 0));
    }

    [Fact]
    public void MultiTargetPixelPatchCommand_UpdatesAndUndoesMultipleSurfacesAtomically()
    {
        var fixture = CreateTwoSurfaceDocument();
        var bus = new CommandBus(fixture.Document);
        var command = new MultiTargetPixelPatchCommand(
        [
            new SurfacePixelPatch(fixture.FirstSurfaceId, [new PixelWrite(0, 0, Red)]),
            new SurfacePixelPatch(fixture.SecondSurfaceId, [new PixelWrite(1, 0, Green)]),
        ], "Transform Multiple Targets");

        bus.Execute(command);
        Assert.Equal(Red, fixture.Document.Resources.GetSurface(fixture.FirstSurfaceId).GetPixel(0, 0));
        Assert.Equal(Green, fixture.Document.Resources.GetSurface(fixture.SecondSurfaceId).GetPixel(1, 0));
        Assert.Equal(1, bus.UndoCount);

        bus.Undo();
        Assert.Equal(Rgba32.Transparent, fixture.Document.Resources.GetSurface(fixture.FirstSurfaceId).GetPixel(0, 0));
        Assert.Equal(Rgba32.Transparent, fixture.Document.Resources.GetSurface(fixture.SecondSurfaceId).GetPixel(1, 0));
    }

    [Fact]
    public void MultiTargetPixelPatchCommand_InvalidLaterTargetDoesNotPartiallyApplyEarlierTarget()
    {
        var fixture = CreateTwoSurfaceDocument();
        var bus = new CommandBus(fixture.Document);
        var command = new MultiTargetPixelPatchCommand(
        [
            new SurfacePixelPatch(fixture.FirstSurfaceId, [new PixelWrite(0, 0, Red)]),
            new SurfacePixelPatch(fixture.SecondSurfaceId, [new PixelWrite(99, 0, Green)]),
        ]);

        Assert.Throws<ArgumentOutOfRangeException>(() => bus.Execute(command));
        Assert.Equal(Rgba32.Transparent, fixture.Document.Resources.GetSurface(fixture.FirstSurfaceId).GetPixel(0, 0));
        Assert.Equal(0, bus.UndoCount);
    }

    private static TwoSurfaceFixture CreateTwoSurfaceDocument()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 1);
        var firstCel = document.Cels.Single();
        var secondFrame = new Frame(FrameId.New());
        document.AddFrame(secondFrame);
        var secondSurfaceId = document.Resources.AddSurface(new PixelSurface(new IntSize(2, 1)));
        document.AddCel(new Cel(CelId.New(), firstCel.LayerId, secondFrame.Id, secondSurfaceId));
        return new TwoSurfaceFixture(document, firstCel.SurfaceId, secondSurfaceId);
    }

    private sealed record TwoSurfaceFixture(PixelDocument Document, ResourceId FirstSurfaceId, ResourceId SecondSurfaceId);
}
