using MyLovePixel.Commands;
using MyLovePixel.Commands.Tiles;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using Xunit;

namespace MyLovePixel.Core.Tests;

public sealed class TilemapCommandTests
{
    [Fact]
    public void SetTileCell_ChangesOnlyCellReference_AndUndoRestoresIt()
    {
        var fixture = CreateFixture();
        var bus = new CommandBus(fixture.Document);
        var surfaceCount = fixture.Document.Resources.SurfaceIds.Count;
        var coordinate = new IntPoint(7, -3);

        var change = bus.Execute(new SetTileCellCommand(
            fixture.TilemapId,
            coordinate,
            new TileCell(fixture.TileId, TileCellFlags.FlipX, 4)));

        Assert.Equal(surfaceCount, fixture.Document.Resources.SurfaceIds.Count);
        Assert.Equal(fixture.TileId, fixture.Map.GetCell(coordinate)!.Value.TileId);
        Assert.Single(change.DirtyTilemapCells);
        Assert.Empty(change.DirtySurfaces);
        Assert.Equal(1, bus.UndoCount);

        bus.Undo();
        Assert.Null(fixture.Map.GetCell(coordinate));
        Assert.Equal(surfaceCount, fixture.Document.Resources.SurfaceIds.Count);
    }

    [Fact]
    public void EditTilePixels_UpdatesSharedSurface_WithoutChangingCellReferences()
    {
        var fixture = CreateFixture();
        var bus = new CommandBus(fixture.Document);
        var first = new IntPoint(0, 0);
        var second = new IntPoint(1, 0);
        bus.Execute(new SetTileCellCommand(fixture.TilemapId, first, new TileCell(fixture.TileId)));
        bus.Execute(new SetTileCellCommand(fixture.TilemapId, second, new TileCell(fixture.TileId)));
        var beforeUndoCount = bus.UndoCount;

        var change = bus.Execute(new EditTilePixelsCommand(
            fixture.TilesetId,
            fixture.TileId,
            [new PixelWrite(1, 1, new Rgba32(200, 10, 20, 255))]));

        Assert.Equal(new Rgba32(200, 10, 20, 255), fixture.Document.Resources.GetSurface(fixture.SurfaceId).GetPixel(1, 1));
        Assert.Equal(fixture.TileId, fixture.Map.GetCell(first)!.Value.TileId);
        Assert.Equal(fixture.TileId, fixture.Map.GetCell(second)!.Value.TileId);
        Assert.Single(change.DirtySurfaces);
        Assert.Empty(change.DirtyTilemapCells);
        Assert.Equal(beforeUndoCount + 1, bus.UndoCount);

        bus.Undo();
        Assert.Equal(Rgba32.Transparent, fixture.Document.Resources.GetSurface(fixture.SurfaceId).GetPixel(1, 1));
    }

    [Fact]
    public void MakeUniqueTile_ClonesSurfaceForOneCell_AndUndoRestoresSharing()
    {
        var fixture = CreateFixture();
        var bus = new CommandBus(fixture.Document);
        var first = new IntPoint(0, 0);
        var second = new IntPoint(1, 0);
        bus.Execute(new SetTileCellCommand(fixture.TilemapId, first, new TileCell(fixture.TileId, TileCellFlags.FlipY, 2)));
        bus.Execute(new SetTileCellCommand(fixture.TilemapId, second, new TileCell(fixture.TileId)));
        fixture.Document.Resources.GetSurface(fixture.SurfaceId).SetPixel(0, 0, new Rgba32(1, 2, 3, 255));

        var command = new MakeUniqueTileCommand(fixture.TilemapId, first);
        bus.Execute(command);

        var uniqueCell = fixture.Map.GetCell(first)!.Value;
        Assert.Equal(command.NewTileId, uniqueCell.TileId);
        Assert.Equal(TileCellFlags.FlipY, uniqueCell.Flags);
        Assert.Equal((ushort)2, uniqueCell.Variant);
        Assert.Equal(fixture.TileId, fixture.Map.GetCell(second)!.Value.TileId);
        Assert.NotEqual(fixture.SurfaceId, command.NewSurfaceId);
        Assert.Equal(
            fixture.Document.Resources.GetSurface(fixture.SurfaceId).Snapshot().Bytes.ToArray(),
            fixture.Document.Resources.GetSurface(command.NewSurfaceId).Snapshot().Bytes.ToArray());

        bus.Undo();

        Assert.Equal(fixture.TileId, fixture.Map.GetCell(first)!.Value.TileId);
        Assert.False(fixture.Document.Resources.ContainsSurface(command.NewSurfaceId));
        Assert.False(fixture.Tileset.ContainsTile(command.NewTileId));
    }

    [Fact]
    public void CollectUnusedTiles_RemovesOnlyUnreferencedTilesAndSurfaces_AndUndoRestoresThem()
    {
        var fixture = CreateFixture();
        var bus = new CommandBus(fixture.Document);
        bus.Execute(new SetTileCellCommand(fixture.TilemapId, new IntPoint(0, 0), new TileCell(fixture.TileId)));

        var unusedSurfaceId = fixture.Document.Resources.AddSurface(new PixelSurface(new IntSize(2, 2), new Rgba32(9, 8, 7, 255)));
        var unusedTile = new TileDefinition(TileId.New(), unusedSurfaceId, "Unused");
        fixture.Document.Resources.AddTile(fixture.TilesetId, unusedTile);
        var originalIndex = fixture.Tileset.IndexOf(unusedTile.Id);

        bus.Execute(new CollectUnusedTilesCommand(fixture.TilesetId));

        Assert.True(fixture.Tileset.ContainsTile(fixture.TileId));
        Assert.False(fixture.Tileset.ContainsTile(unusedTile.Id));
        Assert.False(fixture.Document.Resources.ContainsSurface(unusedSurfaceId));
        Assert.True(fixture.Document.Resources.ContainsSurface(fixture.SurfaceId));

        bus.Undo();

        Assert.True(fixture.Tileset.ContainsTile(unusedTile.Id));
        Assert.True(fixture.Document.Resources.ContainsSurface(unusedSurfaceId));
        Assert.Equal(originalIndex, fixture.Tileset.IndexOf(unusedTile.Id));
    }

    private static Fixture CreateFixture()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var surfaceId = document.Resources.AddSurface(new PixelSurface(new IntSize(2, 2)));
        var tileset = new Tileset(TilesetId.New(), "Terrain", new IntSize(2, 2));
        document.Resources.AddTileset(tileset);
        var tile = new TileDefinition(TileId.New(), surfaceId, "Grass");
        document.Resources.AddTile(tileset.Id, tile);
        var tilemap = new MyLovePixel.Core.Tiles.Tilemap(TilemapId.New(), "Ground", tileset.Id);
        document.Resources.AddTilemap(tilemap);
        return new Fixture(document, tileset, tilemap, tileset.Id, tilemap.Id, tile.Id, surfaceId);
    }

    private sealed record Fixture(
        PixelDocument Document,
        Tileset Tileset,
        MyLovePixel.Core.Tiles.Tilemap Map,
        TilesetId TilesetId,
        TilemapId TilemapId,
        TileId TileId,
        ResourceId SurfaceId);
}
