using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using MyLovePixel.Tilemap;

namespace MyLovePixel.Commands.Tiles;

public sealed class ApplyTilemapCellPatchCommand : ICommand
{
    private readonly TilemapId _tilemapId;
    private readonly TileCellWrite[] _writes;

    public ApplyTilemapCellPatchCommand(
        TilemapId tilemapId,
        TilemapCellPatch patch,
        string name = "Apply Tilemap Cell Patch")
    {
        if (tilemapId.Value == Guid.Empty) throw new ArgumentException("TilemapId cannot be empty.", nameof(tilemapId));
        ArgumentNullException.ThrowIfNull(patch);
        _tilemapId = tilemapId;
        _writes = patch.Writes.ToArray();
        if (_writes.Length == 0) throw new ArgumentException("Tilemap cell patch must contain at least one write.", nameof(patch));
        Name = string.IsNullOrWhiteSpace(name) ? "Apply Tilemap Cell Patch" : name;
    }

    public string Name { get; }

    public CommandApplication Apply(PixelDocument document)
    {
        var tilemap = document.Resources.GetTilemap(_tilemapId);
        var tileset = document.Resources.GetTileset(tilemap.TilesetId);
        var before = new TileCellWrite[_writes.Length];
        var changedCoordinates = new List<IntPoint>(_writes.Length);
        var revisionSteps = 0L;

        for (var index = 0; index < _writes.Length; index++)
        {
            var write = _writes[index];
            var previous = tilemap.GetCell(write.Coordinate);
            before[index] = new TileCellWrite(write.Coordinate, previous);
            PrevalidateCell(tileset, write);
            if (previous != write.Cell)
            {
                revisionSteps = checked(revisionSteps + 1);
                changedCoordinates.Add(write.Coordinate);
            }
        }

        EnsureRevisionCapacity(tilemap.Revision, revisionSteps);
        foreach (var write in _writes)
            document.Resources.SetTileCell(_tilemapId, write.Coordinate, write.Cell);

        return new CommandApplication(
            new Undo(before),
            DocumentChange.ForTilemapCells(_tilemapId, changedCoordinates));
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        var tilemap = document.Resources.GetTilemap(_tilemapId);
        var tileset = document.Resources.GetTileset(tilemap.TilesetId);
        var changedCoordinates = new List<IntPoint>(undo.Before.Length);
        var revisionSteps = 0L;

        foreach (var write in undo.Before)
        {
            PrevalidateCell(tileset, write);
            if (tilemap.GetCell(write.Coordinate) != write.Cell)
            {
                revisionSteps = checked(revisionSteps + 1);
                changedCoordinates.Add(write.Coordinate);
            }
        }

        EnsureRevisionCapacity(tilemap.Revision, revisionSteps);
        foreach (var write in undo.Before)
            document.Resources.SetTileCell(_tilemapId, write.Coordinate, write.Cell);

        return DocumentChange.ForTilemapCells(_tilemapId, changedCoordinates);
    }

    private static void PrevalidateCell(Tileset tileset, TileCellWrite write)
    {
        if (write.Cell is not { } cell) return;
        if (!tileset.ContainsTile(cell.TileId))
            throw new InvalidOperationException(
                $"Tilemap patch cell {write.Coordinate} references tile '{cell.TileId}' outside tileset '{tileset.Id}'.");
        if ((cell.Flags & TileCellFlags.Rotate90) != 0 && tileset.TileSize.Width != tileset.TileSize.Height)
            throw new InvalidOperationException(
                $"Tilemap patch cell {write.Coordinate} cannot rotate non-square tile size {tileset.TileSize} by 90 degrees.");
    }

    private static void EnsureRevisionCapacity(long revision, long steps)
    {
        if (steps < 0) throw new ArgumentOutOfRangeException(nameof(steps));
        if (revision > long.MaxValue - steps)
            throw new OverflowException("Tilemap revision cannot advance beyond Int64.MaxValue.");
    }

    private sealed record Undo(TileCellWrite[] Before) : IUndoToken;
}
