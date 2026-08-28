using MyLovePixel.Commands;
using MyLovePixel.Commands.Color;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Render.Tests;

public sealed class ColorCycleTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Green = new(0, 255, 0, 255);
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    [Fact]
    public void PaletteCycle_RotatesBothDirectionsAndRejectsOverlappingRanges()
    {
        var paletteId = PaletteId.New();
        var forward = new PaletteCycle(paletteId, 1, 3, 1);
        var backward = new PaletteCycle(paletteId, 1, 3, -1);

        Assert.Equal((byte)2, forward.RemapIndex(1));
        Assert.Equal((byte)1, forward.RemapIndex(3));
        Assert.Equal((byte)3, backward.RemapIndex(1));
        Assert.Equal((byte)2, backward.RemapIndex(3));
        Assert.Equal((byte)0, forward.RemapIndex(0));

        Assert.Throws<ArgumentException>(() => new ColorCycleFrameValue([
            new PaletteCycle(paletteId, 1, 3, 1),
            new PaletteCycle(paletteId, 3, 4, 1),
        ]));
    }

    [Fact]
    public void SetColorCycleCommand_ValidatesPaletteRangeAndTransparentIndex_AndUndoRestoresTrack()
    {
        var fixture = CreateIndexedDocument();
        var valid = new ColorCycleFrameValue([
            new PaletteCycle(fixture.PaletteId, 1, 3, 1),
        ]);

        fixture.Bus.Execute(new SetColorCyclesKeyframeCommand(fixture.FrameId, valid));
        Assert.True(fixture.Document.Animation.ColorCycleTrack.TryGetValue(fixture.FrameId, out var stored));
        Assert.Equal(valid, stored);

        fixture.Bus.Undo();
        Assert.False(fixture.Document.Animation.ColorCycleTrack.TryGetValue(fixture.FrameId, out _));

        var undoCount = fixture.Bus.UndoCount;
        Assert.Throws<ArgumentException>(() => fixture.Bus.Execute(
            new SetColorCyclesKeyframeCommand(
                fixture.FrameId,
                new ColorCycleFrameValue([
                    new PaletteCycle(fixture.PaletteId, 0, 2, 1),
                ]))));
        Assert.Equal(undoCount, fixture.Bus.UndoCount);

        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Bus.Execute(
            new SetColorCyclesKeyframeCommand(
                fixture.FrameId,
                new ColorCycleFrameValue([
                    new PaletteCycle(fixture.PaletteId, 1, 4, 1),
                ]))));
        Assert.Equal(undoCount, fixture.Bus.UndoCount);
    }

    [Fact]
    public void CopyAndRemoveFrame_PreserveColorCycleTrackLifecycle()
    {
        var fixture = CreateIndexedDocument();
        var cycles = new ColorCycleFrameValue([
            new PaletteCycle(fixture.PaletteId, 1, 3, 1),
        ]);
        fixture.Bus.Execute(new SetColorCyclesKeyframeCommand(fixture.FrameId, cycles));

        var copy = new CopyFrameCommand(fixture.FrameId, FrameCopyMode.Linked);
        fixture.Bus.Execute(copy);
        Assert.True(fixture.Document.Animation.ColorCycleTrack.TryGetValue(copy.NewFrameId, out var copied));
        Assert.Equal(cycles, copied);

        fixture.Bus.Execute(new RemoveFrameCommand(copy.NewFrameId));
        Assert.False(fixture.Document.Animation.ColorCycleTrack.TryGetValue(copy.NewFrameId, out _));

        fixture.Bus.Undo();
        Assert.True(fixture.Document.Animation.ColorCycleTrack.TryGetValue(copy.NewFrameId, out var restored));
        Assert.Equal(cycles, restored);
    }

    [Fact]
    public void Renderer_AppliesColorCycleWithoutMutatingPaletteOrSurface_AndInvalidatesFrameCache()
    {
        var fixture = CreateIndexedDocument();
        var renderer = new FrameRenderer();
        var surfaceBefore = fixture.Surface.Snapshot().Bytes.ToArray();
        var surfaceRevision = fixture.Surface.Revision;
        var paletteRevision = fixture.Document.Resources.GetPalette(fixture.PaletteId).Revision;

        var first = renderer.Render(
            DocumentSnapshot.Capture(fixture.Document),
            new FrameRenderRequest(fixture.FrameId));
        Assert.Equal(Red, first.Surface.GetPixel(0, 0));
        Assert.Equal(Green, first.Surface.GetPixel(1, 0));
        Assert.Equal(Blue, first.Surface.GetPixel(2, 0));

        fixture.Bus.Execute(new SetColorCyclesKeyframeCommand(
            fixture.FrameId,
            new ColorCycleFrameValue([
                new PaletteCycle(fixture.PaletteId, 1, 3, 1),
            ])));

        var cycled = renderer.Render(
            DocumentSnapshot.Capture(fixture.Document),
            new FrameRenderRequest(fixture.FrameId));

        Assert.Equal(RenderCacheOutcome.FullRecompose, cycled.CacheOutcome);
        Assert.Equal(Green, cycled.Surface.GetPixel(0, 0));
        Assert.Equal(Blue, cycled.Surface.GetPixel(1, 0));
        Assert.Equal(Red, cycled.Surface.GetPixel(2, 0));
        Assert.Equal(surfaceBefore, fixture.Surface.Snapshot().Bytes.ToArray());
        Assert.Equal(surfaceRevision, fixture.Surface.Revision);
        Assert.Equal(paletteRevision, fixture.Document.Resources.GetPalette(fixture.PaletteId).Revision);
    }

    [Fact]
    public void PaletteReorder_IsRejectedWhileColorCycleReferencesPalette()
    {
        var fixture = CreateIndexedDocument();
        fixture.Bus.Execute(new SetColorCyclesKeyframeCommand(
            fixture.FrameId,
            new ColorCycleFrameValue([
                new PaletteCycle(fixture.PaletteId, 1, 3, 1),
            ])));
        var before = fixture.Surface.Snapshot().Bytes.ToArray();
        var paletteBefore = fixture.Document.Resources.GetPalette(fixture.PaletteId).Snapshot().Colors.ToArray();
        var undoCount = fixture.Bus.UndoCount;

        Assert.Throws<InvalidOperationException>(() => fixture.Bus.Execute(
            new ReorderPaletteCommand(fixture.PaletteId, [0, 3, 2, 1])));

        Assert.Equal(before, fixture.Surface.Snapshot().Bytes.ToArray());
        Assert.Equal(paletteBefore, fixture.Document.Resources.GetPalette(fixture.PaletteId).Snapshot().Colors.ToArray());
        Assert.Equal(undoCount, fixture.Bus.UndoCount);
    }

    private static IndexedFixture CreateIndexedDocument()
    {
        var document = PixelDocumentFactory.CreateBlank(3, 1);
        var frameId = document.FrameOrder[0];
        var cel = document.Cels.Single();
        var oldSurfaceId = cel.SurfaceId;
        var paletteId = PaletteId.New();
        document.Resources.AddPalette(
            paletteId,
            new Palette([
                Rgba32.Transparent,
                Red,
                Green,
                Blue,
            ], transparentIndex: 0));
        var surface = PixelSurface.CreateIndexed(new IntSize(3, 1), paletteId);
        surface.ReplaceIndices([1, 2, 3]);
        var surfaceId = document.Resources.AddSurface(surface);
        cel.SurfaceId = surfaceId;
        document.Resources.RemoveSurface(oldSurfaceId);
        return new IndexedFixture(document, new CommandBus(document), frameId, paletteId, surface);
    }

    private sealed record IndexedFixture(
        PixelDocument Document,
        CommandBus Bus,
        FrameId FrameId,
        PaletteId PaletteId,
        PixelSurface Surface);
}
