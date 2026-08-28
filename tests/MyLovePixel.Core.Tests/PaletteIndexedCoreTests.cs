using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Resources;
using MyLovePixel.Core.Validation;
using Xunit;

namespace MyLovePixel.Core.Tests;

public sealed class PaletteIndexedCoreTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Green = new(0, 255, 0, 255);

    [Fact]
    public void PaletteSnapshot_IsIsolatedFromLaterMutation()
    {
        var palette = new Palette([Red, Green], transparentIndex: 0);
        var snapshot = palette.Snapshot();

        palette.SetColor(1, Red);

        Assert.Equal(0, snapshot.Revision);
        Assert.Equal(1, palette.Revision);
        Assert.Equal(Green, snapshot.GetColor(1));
        Assert.Equal(Rgba32.Transparent, snapshot.ResolveColor(0));
    }

    [Fact]
    public void ResourceStore_RequiresPaletteBeforeIndexedSurface_AndValidIndices()
    {
        var store = new ResourceStore();
        var paletteId = PaletteId.New();
        var indexed = PixelSurface.CreateIndexed(new IntSize(2, 1), paletteId, fillIndex: 1);

        Assert.Throws<InvalidOperationException>(() => store.AddSurface(indexed));

        store.AddPalette(paletteId, new Palette([Red, Green]));
        var surfaceId = store.AddSurface(indexed);

        Assert.Equal(PixelFormat.Indexed8, store.GetSurface(surfaceId).Format);
        Assert.Equal(paletteId, store.GetSurface(surfaceId).PaletteId);
        Assert.Throws<InvalidOperationException>(() => store.RemovePalette(paletteId));

        var invalid = PixelSurface.CreateIndexed(new IntSize(1, 1), paletteId, fillIndex: 2);
        Assert.Throws<InvalidOperationException>(() => store.AddSurface(invalid));
    }

    [Fact]
    public void IndexedAndRgbaAccessors_AreExplicitlySeparated()
    {
        var rgba = new PixelSurface(new IntSize(1, 1), Red);
        var indexed = PixelSurface.CreateIndexed(new IntSize(1, 1), PaletteId.New(), fillIndex: 7);

        Assert.Equal(Red, rgba.GetPixel(0, 0));
        Assert.Throws<InvalidOperationException>(() => rgba.GetIndex(0, 0));
        Assert.Equal((byte)7, indexed.GetIndex(0, 0));
        Assert.Throws<InvalidOperationException>(() => indexed.GetPixel(0, 0));
    }

    [Fact]
    public void DocumentSnapshot_CapturesPaletteAndIndexedBytesWithoutLiveAliasing()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 1);
        var paletteId = PaletteId.New();
        var palette = new Palette([Red, Green], transparentIndex: 0);
        document.Resources.AddPalette(paletteId, palette);
        var indexed = PixelSurface.CreateIndexed(new IntSize(2, 1), paletteId, fillIndex: 1);
        var indexedId = document.Resources.AddSurface(indexed);

        var snapshot = DocumentSnapshot.Capture(document);
        palette.SetColor(1, Red);
        indexed.SetIndex(0, 0, 0);

        Assert.Equal(Green, snapshot.GetPalette(paletteId).GetColor(1));
        Assert.Equal((byte)1, snapshot.GetSurface(indexedId).GetIndex(0, 0));
        Assert.Equal(0, snapshot.GetPalette(paletteId).Revision);
        Assert.Equal(0, snapshot.GetSurface(indexedId).Revision);
    }

    [Fact]
    public void Validator_AcceptsValidIndexedResourceAlongsideRgbaCanvas()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var paletteId = PaletteId.New();
        document.Resources.AddPalette(
            paletteId,
            new Palette([Rgba32.Transparent, Red, Green], transparentIndex: 0));
        document.Resources.AddSurface(
            PixelSurface.CreateIndexed(new IntSize(2, 2), paletteId, fillIndex: 2));

        var issues = DocumentValidator.Validate(document);

        Assert.Empty(issues);
    }
}
