using MyLovePixel.Commands;
using MyLovePixel.Commands.Tiles;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using Xunit;

namespace MyLovePixel.Tilemap.Tests;

public sealed class TilemapPatchCommandTests
{
    [Fact]
    public void MultiCellPatch_IsOneUndoEntry_AndUndoRestoresEveryCell()
    {
        var fixture = CreateSquareFixture();
        var bus = new CommandBus(fixture.Document);
        var patch = new TilemapCellPatch([
            new TileCellWrite(new IntPoint(1, 2), new TileCell(fixture.TileId)),
            new TileCellWrite(new IntPoint(-3, 4), new TileCell(fixture.TileId, TileCellFlags.FlipX, 7)),
        ]);

        var change = bus.Execute(new ApplyTilemapCellPatchCommand(fixture.TilemapId, patch));

        Assert.Equal(1, bus.UndoCount);
        Assert.Equal(2, change.DirtyTilemapCells.Count);
        Assert.Equal(fixture.TileId, fixture.Map.GetCell(new IntPoint(1, 2))!.Value.TileId);
        Assert.Equal(TileCellFlags.FlipX, fixture.Map.GetCell(new IntPoint(-3, 4))!.Value.Flags);

        bus.Undo();

        Assert.Null(fixture.Map.GetCell(new IntPoint(1, 2)));
        Assert.Null(fixture.Map.GetCell(new IntPoint(-3, 4)));
    }

    [Fact]
    public void PatchPrevalidation_PreventsHalfMutationWhenLaterWriteIsInvalid()
    {
        var fixture = CreateSquareFixture();
        var bus = new CommandBus(fixture.Document);
        var beforeRevision = fixture.Map.Revision;
        var validCoordinate = new IntPoint(0, 0);
        var invalidCoordinate = new IntPoint(1, 0);
        var patch = new TilemapCellPatch([
            new TileCellWrite(validCoordinate, new TileCell(fixture.TileId)),
            new TileCellWrite(invalidCoordinate, new TileCell(TileId.New())),
        ]);

        Assert.Throws<InvalidOperationException>(() =>
            bus.Execute(new ApplyTilemapCellPatchCommand(fixture.TilemapId, patch)));

        Assert.Null(fixture.Map.GetCell(validCoordinate));
        Assert.Null(fixture.Map.GetCell(invalidCoordinate));
        Assert.Equal(beforeRevision, fixture.Map.Revision);
        Assert.Equal(0, bus.UndoCount);
    }

    [Fact]
    public void Rotate90_IsRejectedForNonSquareTilesBeforeTilemapMutation()
    {
        var document = PixelDocumentFactory.CreateBlank(8, 8);
        var surfaceId = document.Resources.AddSurface(new PixelSurface(new IntSize(2, 3)));
        var tileset = new Tileset(TilesetId.New(), "Tall", new IntSize(2, 3));
        document.Resources.AddTileset(tileset);
        var tileId = TileId.New();
        document.Resources.AddTile(tileset.Id, new TileDefinition(tileId, surfaceId));
        var map = new MyLovePixel.Core.Tiles.Tilemap(TilemapId.New(), "Map", tileset.Id);
        document.Resources.AddTilemap(map);
        var beforeRevision = map.Revision;

        Assert.Throws<InvalidOperationException>(() =>
            document.Resources.SetTileCell(
                map.Id,
                new IntPoint(0, 0),
                new TileCell(tileId, TileCellFlags.Rotate90)));

        Assert.Null(map.GetCell(new IntPoint(0, 0)));
        Assert.Equal(beforeRevision, map.Revision);
    }

    private static Fixture CreateSquareFixture()
    {
        var document = PixelDocumentFactory.CreateBlank(8, 8);
        var surfaceId = document.Resources.AddSurface(new PixelSurface(new IntSize(2, 2)));
        var tileset = new Tileset(TilesetId.New(), "Square", new IntSize(2, 2));
        document.Resources.AddTileset(tileset);
        var tileId = TileId.New();
        document.Resources.AddTile(tileset.Id, new TileDefinition(tileId, surfaceId));
        var map = new MyLovePixel.Core.Tiles.Tilemap(TilemapId.New(), "Map", tileset.Id);
        document.Resources.AddTilemap(map);
        return new Fixture(document, map, map.Id, tileId);
    }

    private sealed record Fixture(
        PixelDocument Document,
        MyLovePixel.Core.Tiles.Tilemap Map,
        TilemapId TilemapId,
        TileId TileId);
}
