using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using Xunit;

namespace MyLovePixel.Tilemap.Tests;

public sealed class GridTopologyTests
{
    [Fact]
    public void DefaultRegistry_ProvidesRectIsoAndHexWithoutChangingRectSemantics()
    {
        var registry = GridTopologyRegistry.CreateDefault();
        var tileSize = new IntSize(4, 4);

        Assert.Equal(new IntPoint(8, 12), registry.Resolve("rect").GetCellOrigin(new IntPoint(2, 3), tileSize));
        Assert.Equal(new IntPoint(2, 6), registry.Resolve("iso-diamond").GetCellOrigin(new IntPoint(2, 1), tileSize));
        Assert.Equal(new IntPoint(10, 3), registry.Resolve("hex-odd-r").GetCellOrigin(new IntPoint(2, 1), tileSize));
        Assert.Equal(4, registry.Resolve("rect").GetNeighborCoordinates(new IntPoint(0, 0)).Count);
        Assert.Equal(6, registry.Resolve("hex-odd-r").GetNeighborCoordinates(new IntPoint(0, 0)).Count);
    }
}

public sealed class AutoTileTests
{
    [Fact]
    public void NeighborMask_UsesExplicitFourAndEightDirectionBits()
    {
        var fixture = CreateFixture();
        fixture.Document.Resources.SetTileCell(fixture.TilemapId, new IntPoint(0, -1), new TileCell(fixture.TileA));
        fixture.Document.Resources.SetTileCell(fixture.TilemapId, new IntPoint(1, 0), new TileCell(fixture.TileA));
        fixture.Document.Resources.SetTileCell(fixture.TilemapId, new IntPoint(1, -1), new TileCell(fixture.TileA));
        var map = DocumentSnapshot.Capture(fixture.Document).GetTilemap(fixture.TilemapId);

        var four = TileNeighborMaskCalculator.Calculate(map, new IntPoint(0, 0), TileNeighborMode.Four);
        var eight = TileNeighborMaskCalculator.Calculate(map, new IntPoint(0, 0), TileNeighborMode.Eight);

        Assert.Equal(TileNeighborMask.North | TileNeighborMask.East, four);
        Assert.Equal(TileNeighborMask.North | TileNeighborMask.East | TileNeighborMask.NorthEast, eight);
    }

    [Fact]
    public void WeightedVariant_IsStableForDocumentSeedTilemapCoordinateAndRule()
    {
        var fixture = CreateFixture();
        var variants = new Dictionary<TileNeighborMask, IEnumerable<WeightedTileVariant>>
        {
            [TileNeighborMask.None] =
            [
                new WeightedTileVariant(fixture.TileA, 1),
                new WeightedTileVariant(fixture.TileB, 3, TileCellFlags.FlipX, 9),
            ],
        };
        var rule = new BitmaskAutoTileRule("terrain.ground", TileNeighborMode.Eight, variants);
        var firstSnapshot = DocumentSnapshot.Capture(fixture.Document);
        var secondSnapshot = DocumentSnapshot.Capture(fixture.Document);

        var first = Enumerable.Range(-32, 65)
            .Select(x => rule.Resolve(AutoTileContext.Create(firstSnapshot, fixture.TilemapId, new IntPoint(x, 7))))
            .ToArray();
        var second = Enumerable.Range(-32, 65)
            .Select(x => rule.Resolve(AutoTileContext.Create(secondSnapshot, fixture.TilemapId, new IntPoint(x, 7))))
            .ToArray();

        Assert.Equal(first, second);
        Assert.All(first, cell => Assert.True(cell.TileId == fixture.TileA || cell.TileId == fixture.TileB));
    }

    [Fact]
    public void AutoTileEngine_IsSnapshotOnlyAndProducesSortedUniquePatch()
    {
        var fixture = CreateFixture();
        var rule = new BitmaskAutoTileRule(
            "terrain.single",
            TileNeighborMode.Four,
            new Dictionary<TileNeighborMask, IEnumerable<WeightedTileVariant>>
            {
                [TileNeighborMask.None] = [new WeightedTileVariant(fixture.TileA, 1)],
            });
        var revisionBefore = fixture.Document.Resources.GetTilemap(fixture.TilemapId).Revision;
        var patch = AutoTileEngine.Resolve(
            DocumentSnapshot.Capture(fixture.Document),
            fixture.TilemapId,
            rule,
            [new IntPoint(5, 2), new IntPoint(-1, 3), new IntPoint(5, 2)]);

        Assert.Equal(2, patch.Writes.Count);
        Assert.Equal(new IntPoint(5, 2), patch.Writes[0].Coordinate);
        Assert.Equal(new IntPoint(-1, 3), patch.Writes[1].Coordinate);
        Assert.Equal(revisionBefore, fixture.Document.Resources.GetTilemap(fixture.TilemapId).Revision);
    }

    private static Fixture CreateFixture()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var surfaceA = document.Resources.AddSurface(new PixelSurface(new IntSize(2, 2)));
        var surfaceB = document.Resources.AddSurface(new PixelSurface(new IntSize(2, 2), new Rgba32(40, 50, 60, 255)));
        var tileset = new Tileset(TilesetId.New(), "Terrain", new IntSize(2, 2));
        document.Resources.AddTileset(tileset);
        var tileA = TileId.New();
        var tileB = TileId.New();
        document.Resources.AddTile(tileset.Id, new TileDefinition(tileA, surfaceA, "A"));
        document.Resources.AddTile(tileset.Id, new TileDefinition(tileB, surfaceB, "B"));
        var tilemap = new MyLovePixel.Core.Tiles.Tilemap(TilemapId.New(), "Ground", tileset.Id);
        document.Resources.AddTilemap(tilemap);
        return new Fixture(document, tilemap.Id, tileA, tileB);
    }

    private sealed record Fixture(
        PixelDocument Document,
        TilemapId TilemapId,
        TileId TileA,
        TileId TileB);
}
