using MyLovePixel.Commands.Tiles;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using MyLovePixel.Tilemap;

namespace MyLovePixel.Application;

public enum AutoTileNeighborModePresentation
{
    Four = 4,
    Eight = 8,
}

public sealed record AutoTileMappingPresentation(
    byte Mask,
    TileId TileId,
    int Weight = 1,
    TileCellFlags Flags = TileCellFlags.None,
    ushort Variant = 0);

public static partial class AdvancedEditingExtensions
{
    public static void ApplyAutoTile(
        this DocumentSession session,
        TilemapId tilemapId,
        IntRect area,
        AutoTileNeighborModePresentation neighborMode,
        IReadOnlyList<AutoTileMappingPresentation> mappings,
        TileId? fallbackTileId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(mappings);
        if (area.Width <= 0 || area.Height <= 0) throw new ArgumentOutOfRangeException(nameof(area));

        var snapshot = session.CaptureSnapshot();
        var tilemap = snapshot.GetTilemap(tilemapId);
        var tileset = snapshot.GetTileset(tilemap.TilesetId);
        foreach (var mapping in mappings)
        {
            if (!tileset.Tiles.ContainsKey(mapping.TileId))
                throw new ArgumentException($"AutoTile mapping references tile '{mapping.TileId}' outside the selected tileset.", nameof(mappings));
            if (mapping.Weight <= 0) throw new ArgumentOutOfRangeException(nameof(mappings), "AutoTile weight must be positive.");
        }
        if (fallbackTileId is { } fallback && !tileset.Tiles.ContainsKey(fallback))
            throw new ArgumentException("AutoTile fallback tile is outside the selected tileset.", nameof(fallbackTileId));

        var variants = mappings
            .GroupBy(mapping => (TileNeighborMask)mapping.Mask)
            .ToDictionary(
                group => group.Key,
                group => (IEnumerable<WeightedTileVariant>)group.Select(mapping =>
                    new WeightedTileVariant(mapping.TileId, mapping.Weight, mapping.Flags, mapping.Variant)).ToArray());
        var fallbackVariants = fallbackTileId is { } fallbackId
            ? new[] { new WeightedTileVariant(fallbackId, 1) }
            : null;
        var mode = neighborMode == AutoTileNeighborModePresentation.Eight ? TileNeighborMode.Eight : TileNeighborMode.Four;
        var rule = new BitmaskAutoTileRule("desktop.autotile", mode, variants, fallbackVariants);

        var coordinates = new List<IntPoint>(checked(area.Width * area.Height));
        for (var y = area.Y; y < area.Bottom; y++)
        for (var x = area.X; x < area.Right; x++)
            coordinates.Add(new IntPoint(x, y));

        var patch = AutoTileEngine.Resolve(snapshot, tilemapId, rule, coordinates);
        if (patch.Writes.Count == 0) return;
        session.Execute(new ApplyTilemapCellPatchCommand(tilemapId, patch, "AutoTile"));
    }
}
