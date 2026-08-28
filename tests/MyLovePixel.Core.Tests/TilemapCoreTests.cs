using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using MyLovePixel.Core.Validation;
using Xunit;

namespace MyLovePixel.Core.Tests;

public sealed class TilemapCoreTests
{
    [Fact]
    public void SharedTileSurface_IsReferencedByMultipleCellsWithoutPixelCopies()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var surfaceId = document.Resources.AddSurface(new PixelSurface(new IntSize(2, 2)));
        var tileset = new Tileset(TilesetId.New(), "Terrain", new IntSize(2, 2));
        document.Resources.AddTileset(tileset);
        var tile = new TileDefinition(TileId.New(), surfaceId, "Grass");
        document.Resources.AddTile(tileset.Id, tile);
        var tilemap = new Tilemap(TilemapId.New(), "Ground", tileset.Id);
        document.Resources.AddTilemap(tilemap);

        document.Resources.SetTileCell(tilemap.Id, new IntPoint(0, 0), new TileCell(tile.Id));
        document.Resources.SetTileCell(tilemap.Id, new IntPoint(1, 0), new TileCell(tile.Id));
        document.Resources.GetSurface(surfaceId).SetPixel(0, 0, new Rgba32(10, 20, 30, 255));

        var snapshot = DocumentSnapshot.Capture(document);
        var mapSnapshot = snapshot.GetTilemap(tilemap.Id);
        var setSnapshot = snapshot.GetTileset(tileset.Id);

        Assert.Equal(tile.Id, mapSnapshot.GetCell(new IntPoint(0, 0))!.Value.TileId);
        Assert.Equal(tile.Id, mapSnapshot.GetCell(new IntPoint(1, 0))!.Value.TileId);
        Assert.Equal(surfaceId, setSnapshot.GetTile(tile.Id).SurfaceId);
        Assert.Equal(new Rgba32(10, 20, 30, 255), snapshot.GetSurface(surfaceId).GetPixel(0, 0));
        Assert.Empty(DocumentValidator.Validate(document));
    }

    [Fact]
    public void Tilemap_UsesSparseChunksAndSupportsNegativeCoordinates()
    {
        var fixture = CreateFixture();
        var map = fixture.Document.Resources.GetTilemap(fixture.TilemapId);

        fixture.Document.Resources.SetTileCell(fixture.TilemapId, new IntPoint(-1, -1), new TileCell(fixture.TileId));
        fixture.Document.Resources.SetTileCell(fixture.TilemapId, new IntPoint(32, 33), new TileCell(fixture.TileId));

        Assert.Equal(2, map.OccupiedCellCount);
        Assert.Equal(fixture.TileId, map.GetCell(new IntPoint(-1, -1))!.Value.TileId);
        Assert.Equal(fixture.TileId, map.GetCell(new IntPoint(32, 33))!.Value.TileId);
        Assert.Null(map.GetCell(new IntPoint(0, 0)));

        var snapshot = map.Snapshot();
        Assert.Equal(2, snapshot.OccupiedCellCount);
        Assert.Equal(2, snapshot.Chunks.Count);
    }

    [Fact]
    public void InvalidCellReference_DoesNotAdvanceTilemapRevision()
    {
        var fixture = CreateFixture();
        var map = fixture.Document.Resources.GetTilemap(fixture.TilemapId);
        var before = map.Revision;

        Assert.Throws<InvalidOperationException>(() =>
            fixture.Document.Resources.SetTileCell(
                fixture.TilemapId,
                new IntPoint(4, 5),
                new TileCell(TileId.New())));

        Assert.Equal(before, map.Revision);
        Assert.Null(map.GetCell(new IntPoint(4, 5)));
    }

    [Fact]
    public void ReferencedTileResources_CannotBeCollectedPrematurely()
    {
        var fixture = CreateFixture();
        fixture.Document.Resources.SetTileCell(
            fixture.TilemapId,
            new IntPoint(0, 0),
            new TileCell(fixture.TileId));

        Assert.Throws<InvalidOperationException>(() =>
            fixture.Document.Resources.RemoveTile(fixture.TilesetId, fixture.TileId));
        Assert.Throws<InvalidOperationException>(() =>
            fixture.Document.Resources.RemoveSurface(fixture.SurfaceId));
        Assert.Throws<InvalidOperationException>(() =>
            fixture.Document.Resources.RemoveTileset(fixture.TilesetId));
    }

    [Fact]
    public void TileSurfaceSize_IsValidatedBeforeTilesetMutation()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var wrongSurfaceId = document.Resources.AddSurface(new PixelSurface(new IntSize(1, 1)));
        var tileset = new Tileset(TilesetId.New(), "Terrain", new IntSize(2, 2));
        document.Resources.AddTileset(tileset);
        var tileId = TileId.New();

        Assert.Throws<InvalidOperationException>(() =>
            document.Resources.AddTile(tileset.Id, new TileDefinition(tileId, wrongSurfaceId)));

        Assert.Empty(tileset.TileOrder);
        Assert.Equal(0, tileset.Revision);
    }

    [Fact]
    public void DocumentSeed_IsStableForDocumentId_AndCapturedBySnapshot()
    {
        var id = new DocumentId(Guid.ParseExact("00112233445566778899aabbccddeeff", "N"));
        var first = new PixelDocument(id, new CanvasSpec(new IntSize(1, 1)));
        var second = new PixelDocument(id, new CanvasSpec(new IntSize(1, 1)));

        Assert.Equal(first.Seed, second.Seed);
        Assert.Equal(DocumentSeed.Derive(id), first.Seed);
        Assert.Equal(first.Seed, DocumentSnapshot.Capture(first).Seed);
    }

    private static Fixture CreateFixture()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var surfaceId = document.Resources.AddSurface(new PixelSurface(new IntSize(2, 2)));
        var tileset = new Tileset(TilesetId.New(), "Terrain", new IntSize(2, 2));
        document.Resources.AddTileset(tileset);
        var tile = new TileDefinition(TileId.New(), surfaceId, "Grass");
        document.Resources.AddTile(tileset.Id, tile);
        var tilemap = new Tilemap(TilemapId.New(), "Ground", tileset.Id);
        document.Resources.AddTilemap(tilemap);
        return new Fixture(document, tileset.Id, tilemap.Id, tile.Id, surfaceId);
    }

    private sealed record Fixture(
        PixelDocument Document,
        TilesetId TilesetId,
        TilemapId TilemapId,
        TileId TileId,
        ResourceId SurfaceId);
}
