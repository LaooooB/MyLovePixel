using MyLovePixel.Commands;
using MyLovePixel.Commands.Color;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Render.Tests;

public sealed class IndexedRenderTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Green = new(0, 255, 0, 255);
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    [Fact]
    public void CpuCompositor_ResolvesIndexedPixelsThroughPaletteAndTransparentIndex()
    {
        var fixture = CreateIndexedDocument(
            [Blue, Red, Green],
            [0, 1, 2],
            transparentIndex: 0);
        var renderer = new FrameRenderer();

        var result = renderer.Render(
            DocumentSnapshot.Capture(fixture.Document),
            new FrameRenderRequest(fixture.FrameId));

        Assert.Equal(RenderCacheOutcome.FullRecompose, result.CacheOutcome);
        Assert.Equal(Rgba32.Transparent, result.Surface.GetPixel(0, 0));
        Assert.Equal(Red, result.Surface.GetPixel(1, 0));
        Assert.Equal(Green, result.Surface.GetPixel(2, 0));
    }

    [Fact]
    public void PaletteRevisionChange_ForcesFullRecomposeWithoutSurfaceRevisionChange()
    {
        var fixture = CreateIndexedDocument([Red, Green], [0, 1]);
        var renderer = new FrameRenderer();
        var beforeSurfaceRevision = fixture.Surface.Revision;
        var first = renderer.Render(
            DocumentSnapshot.Capture(fixture.Document),
            new FrameRenderRequest(fixture.FrameId));

        fixture.Bus.Execute(new SetPaletteColorCommand(fixture.PaletteId, 0, Blue));
        var second = renderer.Render(
            DocumentSnapshot.Capture(fixture.Document),
            new FrameRenderRequest(fixture.FrameId));

        Assert.Equal(beforeSurfaceRevision, fixture.Surface.Revision);
        Assert.Equal(RenderCacheOutcome.FullRecompose, first.CacheOutcome);
        Assert.Equal(RenderCacheOutcome.FullRecompose, second.CacheOutcome);
        Assert.Equal(Blue, second.Surface.GetPixel(0, 0));
        Assert.Equal(Green, second.Surface.GetPixel(1, 0));
        Assert.Equal(2, renderer.Diagnostics.FullRecomposeCount);
    }

    [Fact]
    public void PaletteReorder_PreservesRenderedBytesExactly()
    {
        var fixture = CreateIndexedDocument(
            [Rgba32.Transparent, Red, Green, Blue],
            [0, 1, 2, 3, 2, 1],
            transparentIndex: 0);
        var renderer = new FrameRenderer();
        var before = renderer.Render(
            DocumentSnapshot.Capture(fixture.Document),
            new FrameRenderRequest(fixture.FrameId));
        var rawBefore = fixture.Surface.Snapshot().Bytes.ToArray();

        fixture.Bus.Execute(new ReorderPaletteCommand(fixture.PaletteId, [3, 1, 0, 2]));
        var after = renderer.Render(
            DocumentSnapshot.Capture(fixture.Document),
            new FrameRenderRequest(fixture.FrameId));

        Assert.NotEqual(rawBefore, fixture.Surface.Snapshot().Bytes.ToArray());
        Assert.Equal(before.Surface.Bytes.ToArray(), after.Surface.Bytes.ToArray());
        Assert.Equal(RenderCacheOutcome.FullRecompose, after.CacheOutcome);
    }

    private static IndexedFixture CreateIndexedDocument(
        Rgba32[] colors,
        byte[] indices,
        byte? transparentIndex = null)
    {
        var document = PixelDocumentFactory.CreateBlank(indices.Length, 1);
        var frameId = document.FrameOrder[0];
        var cel = document.Cels.Single();
        var oldSurfaceId = cel.SurfaceId;
        var paletteId = PaletteId.New();
        var palette = new Palette(colors, transparentIndex);
        document.Resources.AddPalette(paletteId, palette);
        var surface = PixelSurface.CreateIndexed(new IntSize(indices.Length, 1), paletteId);
        surface.ReplaceIndices(indices);
        var surfaceId = document.Resources.AddSurface(surface);
        cel.SurfaceId = surfaceId;
        document.Resources.RemoveSurface(oldSurfaceId);
        return new IndexedFixture(
            document,
            new CommandBus(document),
            frameId,
            paletteId,
            surface);
    }

    private sealed record IndexedFixture(
        PixelDocument Document,
        CommandBus Bus,
        FrameId FrameId,
        PaletteId PaletteId,
        PixelSurface Surface);
}
