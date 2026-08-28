using MyLovePixel.Commands;
using MyLovePixel.Commands.Color;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Core.Tests;

public sealed class PaletteCommandTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Green = new(0, 255, 0, 255);
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    [Fact]
    public void IndexedPixelPatch_IsAtomic_AndUndoRestoresIndices()
    {
        var fixture = CreateIndexedDocument([Red, Green], [0, 1]);
        var initialRevision = fixture.Surface.Revision;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.Bus.Execute(new IndexedPixelPatchCommand(
                fixture.SurfaceId,
                [
                    new IndexedPixelWrite(0, 0, 1),
                    new IndexedPixelWrite(1, 0, 2),
                ])));

        Assert.Equal(initialRevision, fixture.Surface.Revision);
        Assert.Equal((byte)0, fixture.Surface.GetIndex(0, 0));
        Assert.Equal((byte)1, fixture.Surface.GetIndex(1, 0));

        fixture.Bus.Execute(new IndexedPixelPatchCommand(
            fixture.SurfaceId,
            [new IndexedPixelWrite(0, 0, 1)]));

        Assert.Equal((byte)1, fixture.Surface.GetIndex(0, 0));
        Assert.Equal(initialRevision + 1, fixture.Surface.Revision);

        fixture.Bus.Undo();
        Assert.Equal((byte)0, fixture.Surface.GetIndex(0, 0));
    }

    [Fact]
    public void SetPaletteColor_DirtiesEveryReferencedIndexedSurface_WithoutChangingSurfaceRevision()
    {
        var fixture = CreateIndexedDocument([Red, Green], [0, 1]);
        var second = PixelSurface.CreateIndexed(new IntSize(1, 1), fixture.PaletteId, fillIndex: 0);
        var secondId = fixture.Document.Resources.AddSurface(second);
        var surfaceRevision = fixture.Surface.Revision;
        var secondRevision = second.Revision;

        var change = fixture.Bus.Execute(new SetPaletteColorCommand(fixture.PaletteId, 0, Blue));

        Assert.Equal(Blue, fixture.Palette.GetColor(0));
        Assert.Equal(surfaceRevision, fixture.Surface.Revision);
        Assert.Equal(secondRevision, second.Revision);
        Assert.Equal(2, change.DirtySurfaces.Count);
        Assert.Contains(change.DirtySurfaces, item => item.SurfaceId == fixture.SurfaceId);
        Assert.Contains(change.DirtySurfaces, item => item.SurfaceId == secondId);

        fixture.Bus.Undo();
        Assert.Equal(Red, fixture.Palette.GetColor(0));
    }

    [Fact]
    public void ReorderPalette_RemapsEveryReferencedSurfaceAndTransparentIndex_ThenUndoRestores()
    {
        var fixture = CreateIndexedDocument(
            [Rgba32.Transparent, Red, Green],
            [0, 1, 2],
            transparentIndex: 0);
        var beforeIndices = fixture.Surface.Snapshot().Bytes.ToArray();

        fixture.Bus.Execute(new ReorderPaletteCommand(fixture.PaletteId, [2, 0, 1]));

        Assert.Equal(Green, fixture.Palette.GetColor(0));
        Assert.Equal(Rgba32.Transparent, fixture.Palette.GetColor(1));
        Assert.Equal(Red, fixture.Palette.GetColor(2));
        Assert.Equal((byte)1, fixture.Palette.TransparentIndex);
        Assert.Equal(new byte[] { 1, 2, 0 }, fixture.Surface.Snapshot().Bytes.ToArray());

        fixture.Bus.Undo();

        Assert.Equal(beforeIndices, fixture.Surface.Snapshot().Bytes.ToArray());
        Assert.Equal((byte)0, fixture.Palette.TransparentIndex);
        Assert.Equal(Red, fixture.Palette.GetColor(1));
        Assert.Equal(Green, fixture.Palette.GetColor(2));
    }

    private static IndexedFixture CreateIndexedDocument(
        Rgba32[] colors,
        byte[] indices,
        byte? transparentIndex = null)
    {
        var document = PixelDocumentFactory.CreateBlank(indices.Length, 1);
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
        var bus = new CommandBus(document);
        return new IndexedFixture(document, bus, paletteId, palette, surfaceId, surface);
    }

    private sealed record IndexedFixture(
        PixelDocument Document,
        CommandBus Bus,
        PaletteId PaletteId,
        Palette Palette,
        ResourceId SurfaceId,
        PixelSurface Surface);
}
