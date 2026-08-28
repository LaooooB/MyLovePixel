using MyLovePixel.Commands;
using MyLovePixel.Commands.Tiles;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using Xunit;

namespace MyLovePixel.Tilemap.Tests;

public sealed class LargeSparseTilemapTests
{
    [Fact]
    public void LargeSparseMap_RemainsChunkedAndReferenceBased()
    {
        var document = PixelDocumentFactory.CreateBlank(8, 8);
        var surfaceId = document.Resources.AddSurface(new PixelSurface(new IntSize(2, 2)));
        var tileset = new Tileset(TilesetId.New(), "LargeMap", new IntSize(2, 2));
        document.Resources.AddTileset(tileset);
        var tileId = TileId.New();
        document.Resources.AddTile(tileset.Id, new TileDefinition(tileId, surfaceId));
        var map = new Tilemap(TilemapId.New(), "Map", tileset.Id);
        document.Resources.AddTilemap(map);
        var surfaceCount = document.Resources.SurfaceIds.Count;

        var writes = new List<TileCellWrite>(10_000);
        for (var chunkY = 0; chunkY < 25; chunkY++)
        for (var chunkX = 0; chunkX < 25; chunkX++)
        for (var localY = 0; localY < 4; localY++)
        for (var localX = 0; localX < 4; localX++)
            writes.Add(new TileCellWrite(
                new IntPoint(chunkX * Tilemap.ChunkSize + localX, chunkY * Tilemap.ChunkSize + localY),
                new TileCell(tileId)));

        var bus = new CommandBus(document);
        bus.Execute(new ApplyTilemapCellPatchCommand(map.Id, new TilemapCellPatch(writes)));

        Assert.Equal(10_000, map.OccupiedCellCount);
        Assert.Equal(625, map.Snapshot().Chunks.Count);
        Assert.Equal(surfaceCount, document.Resources.SurfaceIds.Count);
        Assert.Equal(surfaceId, tileset.GetTile(tileId).SurfaceId);
        Assert.Equal(1, bus.UndoCount);

        bus.Undo();

        Assert.Equal(0, map.OccupiedCellCount);
        Assert.Empty(map.Snapshot().Chunks);
        Assert.Equal(surfaceCount, document.Resources.SurfaceIds.Count);
    }
}
