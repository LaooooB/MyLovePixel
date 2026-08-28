using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;

namespace MyLovePixel.Commands.Resources;

public sealed class AddPaletteCommand : ICommand
{
    private readonly PaletteId _id = PaletteId.New();
    private readonly Rgba32[] _colors;
    private readonly byte? _transparentIndex;

    public AddPaletteCommand(IEnumerable<Rgba32> colors, byte? transparentIndex = null)
    {
        ArgumentNullException.ThrowIfNull(colors);
        _colors = colors.ToArray();
        _transparentIndex = transparentIndex;
    }

    public string Name => "Add Palette";
    public PaletteId PaletteId => _id;

    public CommandApplication Apply(PixelDocument document)
    {
        document.Resources.AddPalette(_id, new Palette(_colors, _transparentIndex));
        return new CommandApplication(new Undo(), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.Resources.RemovePalette(_id);
        return DocumentChange.Empty;
    }

    private sealed record Undo : IUndoToken;
}

public sealed class AddTilesetCommand : ICommand
{
    private readonly TilesetId _id = TilesetId.New();
    private readonly string _name;
    private readonly IntSize _tileSize;

    public AddTilesetCommand(string name, IntSize tileSize)
    {
        _name = string.IsNullOrWhiteSpace(name) ? "Tileset" : name.Trim();
        _tileSize = tileSize;
    }

    public string Name => "Add Tileset";
    public TilesetId TilesetId => _id;

    public CommandApplication Apply(PixelDocument document)
    {
        document.Resources.AddTileset(new Tileset(_id, _name, _tileSize));
        return new CommandApplication(new Undo(), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.Resources.RemoveTileset(_id);
        return DocumentChange.Empty;
    }

    private sealed record Undo : IUndoToken;
}

public sealed class AddTileCommand : ICommand
{
    private readonly TilesetId _tilesetId;
    private readonly string _name;
    private readonly TileId _tileId = TileId.New();
    private readonly ResourceId _surfaceId = ResourceId.New();

    public AddTileCommand(TilesetId tilesetId, string name = "Tile")
    {
        _tilesetId = tilesetId;
        _name = string.IsNullOrWhiteSpace(name) ? "Tile" : name.Trim();
    }

    public string Name => "Add Tile";
    public TileId TileId => _tileId;

    public CommandApplication Apply(PixelDocument document)
    {
        var tileset = document.Resources.GetTileset(_tilesetId);
        document.Resources.AddSurface(_surfaceId, new PixelSurface(tileset.TileSize));
        try { document.Resources.AddTile(_tilesetId, new TileDefinition(_tileId, _surfaceId, _name)); }
        catch
        {
            document.Resources.RemoveSurface(_surfaceId);
            throw;
        }
        return new CommandApplication(new Undo(), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.Resources.RemoveTile(_tilesetId, _tileId);
        document.Resources.RemoveSurface(_surfaceId);
        return DocumentChange.Empty;
    }

    private sealed record Undo : IUndoToken;
}

public sealed class AddTilemapCommand : ICommand
{
    private readonly TilemapId _id = TilemapId.New();
    private readonly string _name;
    private readonly TilesetId _tilesetId;

    public AddTilemapCommand(string name, TilesetId tilesetId)
    {
        _name = string.IsNullOrWhiteSpace(name) ? "Tilemap" : name.Trim();
        _tilesetId = tilesetId;
    }

    public string Name => "Add Tilemap";
    public TilemapId TilemapId => _id;

    public CommandApplication Apply(PixelDocument document)
    {
        document.Resources.GetTileset(_tilesetId);
        document.Resources.AddTilemap(new MyLovePixel.Core.Tiles.Tilemap(_id, _name, _tilesetId));
        return new CommandApplication(new Undo(), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.Resources.RemoveTilemap(_id);
        return DocumentChange.Empty;
    }

    private sealed record Undo : IUndoToken;
}
