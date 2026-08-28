using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;

namespace MyLovePixel.Commands.Tiles;

public sealed class SetTileCellCommand(
    TilemapId tilemapId,
    IntPoint coordinate,
    TileCell? cell) : ICommand
{
    public string Name => cell is null ? "Clear Tile Cell" : "Set Tile Cell";

    public CommandApplication Apply(PixelDocument document)
    {
        var previous = document.Resources.SetTileCell(tilemapId, coordinate, cell);
        return new CommandApplication(new Undo(previous), DocumentChange.ForTilemapCell(tilemapId, coordinate));
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.Resources.SetTileCell(tilemapId, coordinate, undo.Previous);
        return DocumentChange.ForTilemapCell(tilemapId, coordinate);
    }

    private sealed record Undo(TileCell? Previous) : IUndoToken;
}

public sealed class EditTilePixelsCommand : ICommand
{
    private readonly TilesetId _tilesetId;
    private readonly TileId _tileId;
    private readonly PixelWrite[]? _rgbaWrites;
    private readonly IndexedPixelWrite[]? _indexedWrites;
    private readonly IntRect _dirtyRegion;

    public EditTilePixelsCommand(
        TilesetId tilesetId,
        TileId tileId,
        IEnumerable<PixelWrite> writes,
        string name = "Edit Tile Pixels")
    {
        _tilesetId = tilesetId;
        _tileId = tileId;
        Name = name;
        ArgumentNullException.ThrowIfNull(writes);
        _rgbaWrites = writes
            .GroupBy(write => (write.X, write.Y))
            .Select(group => group.Last())
            .ToArray();
        if (_rgbaWrites.Length == 0) throw new ArgumentException("Tile pixel edit must contain at least one write.", nameof(writes));
        _dirtyRegion = CalculateBounds(_rgbaWrites.Select(write => new IntPoint(write.X, write.Y)).ToArray());
    }

    public EditTilePixelsCommand(
        TilesetId tilesetId,
        TileId tileId,
        IEnumerable<IndexedPixelWrite> writes,
        string name = "Edit Indexed Tile Pixels")
    {
        _tilesetId = tilesetId;
        _tileId = tileId;
        Name = name;
        ArgumentNullException.ThrowIfNull(writes);
        _indexedWrites = writes
            .GroupBy(write => (write.X, write.Y))
            .Select(group => group.Last())
            .ToArray();
        if (_indexedWrites.Length == 0) throw new ArgumentException("Indexed tile pixel edit must contain at least one write.", nameof(writes));
        _dirtyRegion = CalculateBounds(_indexedWrites.Select(write => new IntPoint(write.X, write.Y)).ToArray());
    }

    public string Name { get; }

    public CommandApplication Apply(PixelDocument document)
    {
        var surfaceId = ResolveSurfaceId(document);
        var surface = document.Resources.GetSurface(surfaceId);

        if (_rgbaWrites is { } rgbaWrites)
        {
            if (surface.Format != PixelFormat.Rgba32)
                throw new InvalidOperationException($"Tile '{_tileId}' surface is {surface.Format}, not RGBA32.");
            var before = new PixelWrite[rgbaWrites.Length];
            for (var index = 0; index < rgbaWrites.Length; index++)
            {
                var write = rgbaWrites[index];
                before[index] = new PixelWrite(write.X, write.Y, surface.GetPixel(write.X, write.Y));
            }
            surface.SetPixels(rgbaWrites);
            return new CommandApplication(
                new Undo(surfaceId, before, null),
                DocumentChange.ForSurface(surfaceId, _dirtyRegion));
        }

        var indexedWrites = _indexedWrites!;
        if (surface.Format != PixelFormat.Indexed8 || surface.PaletteId is not { } paletteId)
            throw new InvalidOperationException($"Tile '{_tileId}' surface is not an Indexed8 surface with a palette.");
        var palette = document.Resources.GetPalette(paletteId);
        var indexedBefore = new IndexedPixelWrite[indexedWrites.Length];
        for (var index = 0; index < indexedWrites.Length; index++)
        {
            var write = indexedWrites[index];
            if (write.Index >= palette.Count)
                throw new ArgumentOutOfRangeException(nameof(_indexedWrites), $"Palette index {write.Index} is outside palette '{paletteId}'.");
            indexedBefore[index] = new IndexedPixelWrite(write.X, write.Y, surface.GetIndex(write.X, write.Y));
        }
        surface.SetIndices(indexedWrites);
        return new CommandApplication(
            new Undo(surfaceId, null, indexedBefore),
            DocumentChange.ForSurface(surfaceId, _dirtyRegion));
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        var surface = document.Resources.GetSurface(undo.SurfaceId);
        if (undo.RgbaBefore is { } rgbaBefore) surface.SetPixels(rgbaBefore);
        else surface.SetIndices(undo.IndexedBefore!);
        return DocumentChange.ForSurface(undo.SurfaceId, _dirtyRegion);
    }

    private ResourceId ResolveSurfaceId(PixelDocument document)
    {
        var tileset = document.Resources.GetTileset(_tilesetId);
        return tileset.GetTile(_tileId).SurfaceId;
    }

    private static IntRect CalculateBounds(IReadOnlyList<IntPoint> points)
    {
        var minX = points[0].X;
        var minY = points[0].Y;
        var maxX = minX;
        var maxY = minY;
        for (var index = 1; index < points.Count; index++)
        {
            minX = Math.Min(minX, points[index].X);
            minY = Math.Min(minY, points[index].Y);
            maxX = Math.Max(maxX, points[index].X);
            maxY = Math.Max(maxY, points[index].Y);
        }
        return new IntRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private sealed record Undo(
        ResourceId SurfaceId,
        PixelWrite[]? RgbaBefore,
        IndexedPixelWrite[]? IndexedBefore) : IUndoToken;
}

public sealed class MakeUniqueTileCommand : ICommand
{
    private readonly TilemapId _tilemapId;
    private readonly IntPoint _coordinate;
    private readonly TileId _newTileId = TileId.New();
    private readonly ResourceId _newSurfaceId = ResourceId.New();

    public MakeUniqueTileCommand(TilemapId tilemapId, IntPoint coordinate)
    {
        _tilemapId = tilemapId;
        _coordinate = coordinate;
    }

    public string Name => "Make Unique Tile";
    public TileId NewTileId => _newTileId;
    public ResourceId NewSurfaceId => _newSurfaceId;

    public CommandApplication Apply(PixelDocument document)
    {
        var tilemap = document.Resources.GetTilemap(_tilemapId);
        var originalCell = tilemap.GetCell(_coordinate)
            ?? throw new InvalidOperationException($"Tilemap '{_tilemapId}' cell {_coordinate} is empty.");
        var tileset = document.Resources.GetTileset(tilemap.TilesetId);
        var originalTile = tileset.GetTile(originalCell.TileId);
        var originalSurface = document.Resources.GetSurface(originalTile.SurfaceId);

        if (tilemap.Revision == long.MaxValue) throw new OverflowException("Tilemap revision cannot advance beyond Int64.MaxValue.");
        if (tileset.Revision == long.MaxValue) throw new OverflowException("Tileset revision cannot advance beyond Int64.MaxValue.");
        if (document.Resources.ContainsSurface(_newSurfaceId))
            throw new InvalidOperationException($"Unique surface id '{_newSurfaceId}' already exists.");
        if (tileset.ContainsTile(_newTileId))
            throw new InvalidOperationException($"Unique tile id '{_newTileId}' already exists.");

        document.Resources.AddSurface(_newSurfaceId, originalSurface.Clone());
        try
        {
            document.Resources.AddTile(
                tileset.Id,
                new TileDefinition(_newTileId, _newSurfaceId, $"{originalTile.Name} Unique"));
            document.Resources.SetTileCell(
                _tilemapId,
                _coordinate,
                new TileCell(_newTileId, originalCell.Flags, originalCell.Variant));
        }
        catch
        {
            if (tileset.ContainsTile(_newTileId) && !document.Resources.IsTileReferenced(tileset.Id, _newTileId))
                document.Resources.RemoveTile(tileset.Id, _newTileId);
            if (document.Resources.ContainsSurface(_newSurfaceId))
                document.Resources.RemoveSurface(_newSurfaceId);
            throw;
        }

        return new CommandApplication(
            new Undo(originalCell, tileset.Id),
            DocumentChange.ForTilemapCell(_tilemapId, _coordinate));
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.Resources.SetTileCell(_tilemapId, _coordinate, undo.OriginalCell);
        document.Resources.RemoveTile(undo.TilesetId, _newTileId);
        document.Resources.RemoveSurface(_newSurfaceId);
        return DocumentChange.ForTilemapCell(_tilemapId, _coordinate);
    }

    private sealed record Undo(TileCell OriginalCell, TilesetId TilesetId) : IUndoToken;
}

public sealed class CollectUnusedTilesCommand(TilesetId tilesetId) : ICommand
{
    public string Name => "Collect Unused Tile Resources";

    public CommandApplication Apply(PixelDocument document)
    {
        var tileset = document.Resources.GetTileset(tilesetId);
        var unused = tileset.TileOrder
            .Select((tileId, index) => new RemovedTile(index, tileset.GetTile(tileId)))
            .Where(item => !document.Resources.IsTileReferenced(tilesetId, item.Tile.Id))
            .ToArray();

        foreach (var item in unused.OrderByDescending(item => item.Index))
            document.Resources.RemoveTile(tilesetId, item.Tile.Id);

        var removedSurfaces = new Dictionary<ResourceId, PixelSurface>();
        foreach (var surfaceId in unused.Select(item => item.Tile.SurfaceId).Distinct())
        {
            if (document.IsSurfaceReferenced(surfaceId)) continue;
            removedSurfaces.Add(surfaceId, document.Resources.RemoveSurface(surfaceId));
        }

        return new CommandApplication(new Undo(unused, removedSurfaces), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        foreach (var pair in undo.RemovedSurfaces)
            document.Resources.AddSurface(pair.Key, pair.Value);
        foreach (var item in undo.Tiles.OrderBy(item => item.Index))
            document.Resources.InsertTile(tilesetId, item.Index, item.Tile);
        return DocumentChange.Empty;
    }

    private sealed record RemovedTile(int Index, TileDefinition Tile);
    private sealed record Undo(
        RemovedTile[] Tiles,
        IReadOnlyDictionary<ResourceId, PixelSurface> RemovedSurfaces) : IUndoToken;
}
