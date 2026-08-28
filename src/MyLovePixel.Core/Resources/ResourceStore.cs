using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;

namespace MyLovePixel.Core.Resources;

public sealed class ResourceStore
{
    private readonly Dictionary<ResourceId, PixelSurface> _surfaces = [];
    private readonly Dictionary<PaletteId, Palette> _palettes = [];
    private readonly Dictionary<TilesetId, Tileset> _tilesets = [];
    private readonly Dictionary<TilemapId, Tilemap> _tilemaps = [];

    public IReadOnlyCollection<ResourceId> SurfaceIds => _surfaces.Keys;
    public IReadOnlyCollection<PaletteId> PaletteIds => _palettes.Keys;
    public IReadOnlyCollection<TilesetId> TilesetIds => _tilesets.Keys;
    public IReadOnlyCollection<TilemapId> TilemapIds => _tilemaps.Keys;

    public PixelSurface GetSurface(ResourceId id) =>
        _surfaces.TryGetValue(id, out var surface)
            ? surface
            : throw new KeyNotFoundException($"PixelSurface '{id}' does not exist.");

    public Palette GetPalette(PaletteId id) =>
        _palettes.TryGetValue(id, out var palette)
            ? palette
            : throw new KeyNotFoundException($"Palette '{id}' does not exist.");

    public Tileset GetTileset(TilesetId id) =>
        _tilesets.TryGetValue(id, out var tileset)
            ? tileset
            : throw new KeyNotFoundException($"Tileset '{id}' does not exist.");

    public Tilemap GetTilemap(TilemapId id) =>
        _tilemaps.TryGetValue(id, out var tilemap)
            ? tilemap
            : throw new KeyNotFoundException($"Tilemap '{id}' does not exist.");

    public bool ContainsSurface(ResourceId id) => _surfaces.ContainsKey(id);
    public bool ContainsPalette(PaletteId id) => _palettes.ContainsKey(id);
    public bool ContainsTileset(TilesetId id) => _tilesets.ContainsKey(id);
    public bool ContainsTilemap(TilemapId id) => _tilemaps.ContainsKey(id);

    internal ResourceId AddSurface(PixelSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var id = ResourceId.New();
        AddSurface(id, surface);
        return id;
    }

    internal void AddSurface(ResourceId id, PixelSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (id.Value == Guid.Empty) throw new ArgumentException("ResourceId cannot be empty.", nameof(id));
        ValidateSurfaceReferences(surface);
        if (!_surfaces.TryAdd(id, surface))
            throw new InvalidOperationException($"Resource '{id}' already exists.");
    }

    internal PixelSurface RemoveSurface(ResourceId id)
    {
        if (IsSurfaceReferencedByTile(id))
            throw new InvalidOperationException($"PixelSurface '{id}' is still referenced by a Tile.");
        if (!_surfaces.Remove(id, out var surface))
            throw new KeyNotFoundException($"PixelSurface '{id}' does not exist.");
        return surface;
    }

    internal PaletteId AddPalette(Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var id = PaletteId.New();
        AddPalette(id, palette);
        return id;
    }

    internal void AddPalette(PaletteId id, Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (id.Value == Guid.Empty) throw new ArgumentException("PaletteId cannot be empty.", nameof(id));
        if (!_palettes.TryAdd(id, palette))
            throw new InvalidOperationException($"Palette '{id}' already exists.");
    }

    internal Palette RemovePalette(PaletteId id)
    {
        if (_surfaces.Values.Any(surface => surface.PaletteId == id))
            throw new InvalidOperationException($"Palette '{id}' is still referenced by an Indexed8 surface.");
        if (!_palettes.Remove(id, out var palette))
            throw new KeyNotFoundException($"Palette '{id}' does not exist.");
        return palette;
    }

    internal void AddTileset(Tileset tileset)
    {
        ArgumentNullException.ThrowIfNull(tileset);
        ValidateTilesetReferences(tileset);
        if (!_tilesets.TryAdd(tileset.Id, tileset))
            throw new InvalidOperationException($"Tileset '{tileset.Id}' already exists.");
    }

    internal Tileset RemoveTileset(TilesetId id)
    {
        if (_tilemaps.Values.Any(tilemap => tilemap.TilesetId == id))
            throw new InvalidOperationException($"Tileset '{id}' is still referenced by a Tilemap.");
        if (!_tilesets.Remove(id, out var tileset))
            throw new KeyNotFoundException($"Tileset '{id}' does not exist.");
        return tileset;
    }

    internal void AddTile(TilesetId tilesetId, TileDefinition tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        var tileset = GetTileset(tilesetId);
        ValidateTileSurface(tileset, tile.SurfaceId);
        tileset.AddTile(tile);
    }

    internal void InsertTile(TilesetId tilesetId, int index, TileDefinition tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        var tileset = GetTileset(tilesetId);
        ValidateTileSurface(tileset, tile.SurfaceId);
        tileset.InsertTile(index, tile);
    }

    internal TileDefinition RemoveTile(TilesetId tilesetId, TileId tileId)
    {
        if (IsTileReferenced(tilesetId, tileId))
            throw new InvalidOperationException($"Tile '{tileId}' is still referenced by a Tilemap cell.");
        return GetTileset(tilesetId).RemoveTile(tileId);
    }

    internal void AddTilemap(Tilemap tilemap)
    {
        ArgumentNullException.ThrowIfNull(tilemap);
        ValidateTilemapReferences(tilemap);
        if (!_tilemaps.TryAdd(tilemap.Id, tilemap))
            throw new InvalidOperationException($"Tilemap '{tilemap.Id}' already exists.");
    }

    internal Tilemap RemoveTilemap(TilemapId id)
    {
        if (!_tilemaps.Remove(id, out var tilemap))
            throw new KeyNotFoundException($"Tilemap '{id}' does not exist.");
        return tilemap;
    }

    internal TileCell? SetTileCell(TilemapId tilemapId, IntPoint coordinate, TileCell? cell)
    {
        var tilemap = GetTilemap(tilemapId);
        if (cell is { } value)
        {
            var tileset = GetTileset(tilemap.TilesetId);
            if (!tileset.ContainsTile(value.TileId))
                throw new InvalidOperationException(
                    $"Tilemap '{tilemapId}' cannot reference tile '{value.TileId}' outside tileset '{tileset.Id}'.");
        }
        return tilemap.SetCell(coordinate, cell);
    }

    internal bool IsSurfaceReferencedByTile(ResourceId surfaceId) =>
        _tilesets.Values.Any(tileset =>
            tileset.TileOrder.Any(tileId => tileset.GetTile(tileId).SurfaceId == surfaceId));

    internal bool IsTileReferenced(TilesetId tilesetId, TileId tileId) =>
        _tilemaps.Values
            .Where(tilemap => tilemap.TilesetId == tilesetId)
            .Any(tilemap => tilemap.EnumerateCells().Any(pair => pair.Value.TileId == tileId));

    internal void ValidateSurfaceReferences(PixelSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        switch (surface.Format)
        {
            case PixelFormat.Rgba32:
                if (surface.PaletteId is not null)
                    throw new InvalidOperationException("RGBA32 surfaces cannot reference a palette.");
                return;

            case PixelFormat.Indexed8:
                if (surface.PaletteId is not { } paletteId)
                    throw new InvalidOperationException("Indexed8 surfaces must reference a palette.");
                if (!_palettes.TryGetValue(paletteId, out var palette))
                    throw new InvalidOperationException($"Indexed8 surface references missing palette '{paletteId}'.");
                ValidateIndexedValues(surface.Snapshot(), palette);
                return;

            default:
                throw new NotSupportedException($"Pixel format '{surface.Format}' is not supported by the resource store.");
        }
    }

    internal void ValidateTilesetReferences(Tileset tileset)
    {
        ArgumentNullException.ThrowIfNull(tileset);
        foreach (var tileId in tileset.TileOrder)
            ValidateTileSurface(tileset, tileset.GetTile(tileId).SurfaceId);
    }

    internal void ValidateTilemapReferences(Tilemap tilemap)
    {
        ArgumentNullException.ThrowIfNull(tilemap);
        if (!_tilesets.TryGetValue(tilemap.TilesetId, out var tileset))
            throw new InvalidOperationException($"Tilemap '{tilemap.Id}' references missing tileset '{tilemap.TilesetId}'.");
        foreach (var pair in tilemap.EnumerateCells())
        {
            if (!tileset.ContainsTile(pair.Value.TileId))
                throw new InvalidOperationException(
                    $"Tilemap '{tilemap.Id}' cell {pair.Key} references missing tile '{pair.Value.TileId}'.");
        }
    }

    private void ValidateTileSurface(Tileset tileset, ResourceId surfaceId)
    {
        if (!_surfaces.TryGetValue(surfaceId, out var surface))
            throw new InvalidOperationException($"Tile references missing PixelSurface '{surfaceId}'.");
        if (surface.Size != tileset.TileSize)
            throw new InvalidOperationException(
                $"Tile surface '{surfaceId}' size {surface.Size} does not match tileset tile size {tileset.TileSize}.");
    }

    private static void ValidateIndexedValues(PixelSurfaceSnapshot surface, Palette palette)
    {
        foreach (var index in surface.Bytes.Span)
        {
            if (index >= palette.Count)
                throw new InvalidOperationException(
                    $"Indexed8 surface contains palette index {index}, but palette contains only {palette.Count} entries.");
        }
    }
}
