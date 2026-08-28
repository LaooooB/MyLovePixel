using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using Xunit;

namespace MyLovePixel.Render.Tests;

public sealed class TilemapRendererTests
{
    [Fact]
    public void SharedTileSurfaceChange_RecomposesEveryReferencingCellPartially()
    {
        var fixture = CreateFixture();
        fixture.Document.Resources.SetTileCell(fixture.TilemapId, new IntPoint(0, 0), new TileCell(fixture.TileA));
        fixture.Document.Resources.SetTileCell(fixture.TilemapId, new IntPoint(2, 0), new TileCell(fixture.TileA));
        var renderer = new TilemapRenderer();
        var region = new IntRect(0, 0, 4, 1);
        renderer.Render(DocumentSnapshot.Capture(fixture.Document), new TilemapRenderRequest(fixture.TilemapId, region));

        var surface = fixture.Document.Resources.GetSurface(fixture.SurfaceA);
        var fromRevision = surface.Revision;
        var changedColor = new Rgba32(20, 200, 40, 255);
        surface.SetPixel(0, 0, changedColor);
        var toRevision = surface.Revision;

        var result = renderer.Render(
            DocumentSnapshot.Capture(fixture.Document),
            new TilemapRenderRequest(
                fixture.TilemapId,
                region,
                surfaceInvalidations:
                [new SurfaceInvalidation(fixture.SurfaceA, fromRevision, toRevision, new IntRect(0, 0, 1, 1))]));

        Assert.Equal(RenderCacheOutcome.PartialRecompose, result.CacheOutcome);
        Assert.Equal(TextureUploadMode.Partial, result.UploadPlan.Mode);
        Assert.Equal(8, result.UploadPlan.PixelCount);
        Assert.Equal(changedColor, result.Surface.GetPixel(0, 0));
        Assert.Equal(changedColor, result.Surface.GetPixel(4, 0));
    }

    [Fact]
    public void SingleCellReferenceChange_RecomposesOnlyThatCellWhenHistoryIsComplete()
    {
        var fixture = CreateFixture();
        fixture.Document.Resources.SetTileCell(fixture.TilemapId, new IntPoint(0, 0), new TileCell(fixture.TileA));
        var renderer = new TilemapRenderer();
        var region = new IntRect(0, 0, 4, 1);
        renderer.Render(DocumentSnapshot.Capture(fixture.Document), new TilemapRenderRequest(fixture.TilemapId, region));

        var map = fixture.Document.Resources.GetTilemap(fixture.TilemapId);
        var fromRevision = map.Revision;
        fixture.Document.Resources.SetTileCell(fixture.TilemapId, new IntPoint(1, 0), new TileCell(fixture.TileB));
        var toRevision = map.Revision;

        var result = renderer.Render(
            DocumentSnapshot.Capture(fixture.Document),
            new TilemapRenderRequest(
                fixture.TilemapId,
                region,
                tilemapInvalidations:
                [new TilemapInvalidation(fixture.TilemapId, fromRevision, toRevision, [new IntPoint(1, 0)])]));

        Assert.Equal(RenderCacheOutcome.PartialRecompose, result.CacheOutcome);
        Assert.Equal(4, result.UploadPlan.PixelCount);
        Assert.Equal(fixture.ColorB, result.Surface.GetPixel(2, 0));
    }

    [Fact]
    public void MissingTilemapRevisionHistory_FallsBackToFullRecompose()
    {
        var fixture = CreateFixture();
        var renderer = new TilemapRenderer();
        var region = new IntRect(0, 0, 4, 1);
        renderer.Render(DocumentSnapshot.Capture(fixture.Document), new TilemapRenderRequest(fixture.TilemapId, region));

        fixture.Document.Resources.SetTileCell(fixture.TilemapId, new IntPoint(1, 0), new TileCell(fixture.TileB));
        var result = renderer.Render(
            DocumentSnapshot.Capture(fixture.Document),
            new TilemapRenderRequest(fixture.TilemapId, region));

        Assert.Equal(RenderCacheOutcome.FullRecompose, result.CacheOutcome);
        Assert.Equal(TextureUploadMode.Full, result.UploadPlan.Mode);
    }

    [Fact]
    public void Rotate90_UsesStableClockwiseSampling()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var surface = new PixelSurface(new IntSize(2, 2));
        var a = new Rgba32(255, 0, 0, 255);
        var b = new Rgba32(0, 255, 0, 255);
        var c = new Rgba32(0, 0, 255, 255);
        var d = new Rgba32(255, 255, 0, 255);
        surface.SetPixels([
            new PixelWrite(0, 0, a),
            new PixelWrite(1, 0, b),
            new PixelWrite(0, 1, c),
            new PixelWrite(1, 1, d),
        ]);
        var surfaceId = document.Resources.AddSurface(surface);
        var tileset = new Tileset(TilesetId.New(), "Square", new IntSize(2, 2));
        document.Resources.AddTileset(tileset);
        var tileId = TileId.New();
        document.Resources.AddTile(tileset.Id, new TileDefinition(tileId, surfaceId));
        var map = new MyLovePixel.Core.Tiles.Tilemap(TilemapId.New(), "Map", tileset.Id);
        document.Resources.AddTilemap(map);
        document.Resources.SetTileCell(map.Id, new IntPoint(0, 0), new TileCell(tileId, TileCellFlags.Rotate90));

        var result = new TilemapRenderer().Render(
            DocumentSnapshot.Capture(document),
            new TilemapRenderRequest(map.Id, new IntRect(0, 0, 1, 1)));

        Assert.Equal(c, result.Surface.GetPixel(0, 0));
        Assert.Equal(a, result.Surface.GetPixel(1, 0));
        Assert.Equal(d, result.Surface.GetPixel(0, 1));
        Assert.Equal(b, result.Surface.GetPixel(1, 1));
    }

    [Fact]
    public void IndexedTileAndCacheClear_RebuildToIdenticalRgbaOutput()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var paletteId = PaletteId.New();
        var expected = new Rgba32(80, 90, 100, 255);
        document.Resources.AddPalette(paletteId, new Palette([Rgba32.Transparent, expected], transparentIndex: 0));
        var surfaceId = ResourceId.New();
        document.Resources.AddSurface(surfaceId, PixelSurface.FromIndexedBytes(new IntSize(2, 2), paletteId, [1, 0, 0, 1]));
        var tileset = new Tileset(TilesetId.New(), "Indexed", new IntSize(2, 2));
        document.Resources.AddTileset(tileset);
        var tileId = TileId.New();
        document.Resources.AddTile(tileset.Id, new TileDefinition(tileId, surfaceId));
        var map = new MyLovePixel.Core.Tiles.Tilemap(TilemapId.New(), "Map", tileset.Id);
        document.Resources.AddTilemap(map);
        document.Resources.SetTileCell(map.Id, new IntPoint(0, 0), new TileCell(tileId));
        var renderer = new TilemapRenderer();
        var snapshot = DocumentSnapshot.Capture(document);
        var request = new TilemapRenderRequest(map.Id, new IntRect(0, 0, 1, 1));

        var first = renderer.Render(snapshot, request);
        renderer.ClearCaches();
        var rebuilt = renderer.Render(snapshot, request);

        Assert.Equal(expected, first.Surface.GetPixel(0, 0));
        Assert.Equal(Rgba32.Transparent, first.Surface.GetPixel(1, 0));
        Assert.Equal(first.Surface.Bytes.ToArray(), rebuilt.Surface.Bytes.ToArray());
        Assert.Equal(RenderCacheOutcome.FullRecompose, rebuilt.CacheOutcome);
    }

    private static Fixture CreateFixture()
    {
        var document = PixelDocumentFactory.CreateBlank(16, 16);
        var colorA = new Rgba32(180, 30, 40, 255);
        var colorB = new Rgba32(30, 40, 180, 255);
        var surfaceA = document.Resources.AddSurface(new PixelSurface(new IntSize(2, 2), colorA));
        var surfaceB = document.Resources.AddSurface(new PixelSurface(new IntSize(2, 2), colorB));
        var tileset = new Tileset(TilesetId.New(), "Terrain", new IntSize(2, 2));
        document.Resources.AddTileset(tileset);
        var tileA = TileId.New();
        var tileB = TileId.New();
        document.Resources.AddTile(tileset.Id, new TileDefinition(tileA, surfaceA, "A"));
        document.Resources.AddTile(tileset.Id, new TileDefinition(tileB, surfaceB, "B"));
        var map = new MyLovePixel.Core.Tiles.Tilemap(TilemapId.New(), "Map", tileset.Id);
        document.Resources.AddTilemap(map);
        return new Fixture(document, map.Id, tileA, tileB, surfaceA, colorB);
    }

    private sealed record Fixture(
        PixelDocument Document,
        TilemapId TilemapId,
        TileId TileA,
        TileId TileB,
        ResourceId SurfaceA,
        Rgba32 ColorB);
}
