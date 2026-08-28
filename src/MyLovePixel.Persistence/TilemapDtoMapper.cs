using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;

namespace MyLovePixel.Persistence;

internal static class TilemapDtoMapper
{
    public static void ToDto(PixelDocument document, DocumentDto dto, DocumentDto? template)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(dto);

        var tilesetTemplates = IndexById(template?.Tilesets, value => value.Id);
        foreach (var tilesetId in document.Resources.TilesetIds.OrderBy(id => id.Value))
        {
            var tileset = document.Resources.GetTileset(tilesetId);
            tilesetTemplates.TryGetValue(tilesetId.ToString(), out var previous);
            var tileTemplates = IndexById(previous?.Tiles, value => value.Id);
            var item = new TilesetDto
            {
                Id = tileset.Id.ToString(),
                Name = tileset.Name,
                TileWidth = tileset.TileSize.Width,
                TileHeight = tileset.TileSize.Height,
                ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
            };

            foreach (var tileId in tileset.TileOrder)
            {
                var tile = tileset.GetTile(tileId);
                tileTemplates.TryGetValue(tileId.ToString(), out var previousTile);
                item.Tiles.Add(new TileDto
                {
                    Id = tile.Id.ToString(),
                    SurfaceId = tile.SurfaceId.ToString(),
                    Name = tile.Name,
                    ExtensionData = ExtensionData.Clone(previousTile?.ExtensionData),
                });
            }
            dto.Tilesets.Add(item);
        }

        var tilemapTemplates = IndexById(template?.Tilemaps, value => value.Id);
        foreach (var tilemapId in document.Resources.TilemapIds.OrderBy(id => id.Value))
        {
            var tilemap = document.Resources.GetTilemap(tilemapId);
            tilemapTemplates.TryGetValue(tilemapId.ToString(), out var previous);
            var cellTemplates = IndexCells(previous?.Cells);
            var item = new TilemapDto
            {
                Id = tilemap.Id.ToString(),
                Name = tilemap.Name,
                TilesetId = tilemap.TilesetId.ToString(),
                TopologyId = tilemap.TopologyId,
                ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
            };

            foreach (var pair in tilemap.EnumerateCells())
            {
                cellTemplates.TryGetValue(pair.Key, out var previousCell);
                item.Cells.Add(new TileCellDto
                {
                    X = pair.Key.X,
                    Y = pair.Key.Y,
                    TileId = pair.Value.TileId.ToString(),
                    Flags = (byte)pair.Value.Flags,
                    Variant = pair.Value.Variant,
                    ExtensionData = ExtensionData.Clone(previousCell?.ExtensionData),
                });
            }
            dto.Tilemaps.Add(item);
        }
    }

    public static void Populate(
        PixelDocument document,
        DocumentDto dto,
        IReadOnlySet<ResourceId> surfaceIds)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(surfaceIds);

        var tilesetIds = new HashSet<TilesetId>();
        foreach (var item in dto.Tilesets)
        {
            var id = new TilesetId(ParseGuid(item.Id, "tileset.id"));
            if (!tilesetIds.Add(id)) throw InvalidReference($"Duplicate tileset id '{item.Id}'.");
            var tileset = new Tileset(id, item.Name, new IntSize(item.TileWidth, item.TileHeight));
            document.Resources.AddTileset(tileset);

            var tileIds = new HashSet<TileId>();
            foreach (var tileItem in item.Tiles)
            {
                var tileId = new TileId(ParseGuid(tileItem.Id, "tileset.tile.id"));
                if (!tileIds.Add(tileId))
                    throw InvalidReference($"Tileset '{item.Id}' contains duplicate tile id '{tileItem.Id}'.");
                var surfaceId = new ResourceId(ParseGuid(tileItem.SurfaceId, "tileset.tile.surfaceId"));
                if (!surfaceIds.Contains(surfaceId))
                    throw InvalidReference($"Tile '{tileItem.Id}' references missing surface '{tileItem.SurfaceId}'.");
                document.Resources.AddTile(
                    id,
                    new TileDefinition(tileId, surfaceId, tileItem.Name));
            }
        }

        var tilemapIds = new HashSet<TilemapId>();
        foreach (var item in dto.Tilemaps)
        {
            var id = new TilemapId(ParseGuid(item.Id, "tilemap.id"));
            if (!tilemapIds.Add(id)) throw InvalidReference($"Duplicate tilemap id '{item.Id}'.");
            var tilesetId = new TilesetId(ParseGuid(item.TilesetId, "tilemap.tilesetId"));
            if (!tilesetIds.Contains(tilesetId))
                throw InvalidReference($"Tilemap '{item.Id}' references missing tileset '{item.TilesetId}'.");
            var tilemap = new Tilemap(id, item.Name, tilesetId, item.TopologyId);
            document.Resources.AddTilemap(tilemap);

            var seenCoordinates = new HashSet<IntPoint>();
            foreach (var cellItem in item.Cells)
            {
                var coordinate = new IntPoint(cellItem.X, cellItem.Y);
                if (!seenCoordinates.Add(coordinate))
                    throw InvalidReference($"Tilemap '{item.Id}' contains duplicate cell coordinate {coordinate}.");
                var tileId = new TileId(ParseGuid(cellItem.TileId, "tilemap.cell.tileId"));
                document.Resources.SetTileCell(
                    id,
                    coordinate,
                    new TileCell(tileId, (TileCellFlags)cellItem.Flags, cellItem.Variant));
            }
        }
    }

    private static Dictionary<string, T> IndexById<T>(
        IEnumerable<T>? values,
        Func<T, string> getId) where T : class
    {
        if (values is null) return new Dictionary<string, T>(StringComparer.Ordinal);
        return values.ToDictionary(getId, StringComparer.Ordinal);
    }

    private static Dictionary<IntPoint, TileCellDto> IndexCells(IEnumerable<TileCellDto>? cells)
    {
        if (cells is null) return [];
        return cells.ToDictionary(cell => new IntPoint(cell.X, cell.Y));
    }

    private static Guid ParseGuid(string value, string field)
    {
        if (!Guid.TryParseExact(value, "N", out var id) || id == Guid.Empty)
            throw InvalidJson($"Field '{field}' must be a non-empty 32-digit Guid.");
        return id;
    }

    private static PixelProjectException InvalidJson(string message) =>
        new(PixelProjectErrorCode.InvalidJson, message, PixelProjectFormat.DocumentEntry);

    private static PixelProjectException InvalidReference(string message) =>
        new(PixelProjectErrorCode.InvalidReference, message, PixelProjectFormat.DocumentEntry);
}
