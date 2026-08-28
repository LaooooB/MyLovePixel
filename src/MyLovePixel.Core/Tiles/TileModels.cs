using System.Collections.ObjectModel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Tiles;

[Flags]
public enum TileCellFlags : byte
{
    None = 0,
    FlipX = 1 << 0,
    FlipY = 1 << 1,
    Rotate90 = 1 << 2,
}

public readonly record struct TileCell
{
    private const TileCellFlags AllowedFlags =
        TileCellFlags.FlipX | TileCellFlags.FlipY | TileCellFlags.Rotate90;

    public TileCell(TileId tileId, TileCellFlags flags = TileCellFlags.None, ushort variant = 0)
    {
        if (tileId.Value == Guid.Empty) throw new ArgumentException("TileId cannot be empty.", nameof(tileId));
        if ((flags & ~AllowedFlags) != 0) throw new ArgumentOutOfRangeException(nameof(flags));
        TileId = tileId;
        Flags = flags;
        Variant = variant;
    }

    public TileId TileId { get; }
    public TileCellFlags Flags { get; }
    public ushort Variant { get; }
}

public readonly record struct TileChunkCoordinate(int X, int Y);

public sealed record TileDefinition
{
    public TileDefinition(TileId id, ResourceId surfaceId, string name = "Tile")
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("TileId cannot be empty.", nameof(id));
        if (surfaceId.Value == Guid.Empty) throw new ArgumentException("ResourceId cannot be empty.", nameof(surfaceId));
        Id = id;
        SurfaceId = surfaceId;
        Name = string.IsNullOrWhiteSpace(name) ? "Tile" : name;
    }

    public TileId Id { get; }
    public ResourceId SurfaceId { get; }
    public string Name { get; }
}

public sealed class Tileset
{
    private readonly Dictionary<TileId, TileDefinition> _tiles = [];
    private readonly List<TileId> _tileOrder = [];

    public Tileset(TilesetId id, string name, IntSize tileSize)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("TilesetId cannot be empty.", nameof(id));
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? "Tileset" : name;
        TileSize = tileSize;
    }

    public TilesetId Id { get; }
    public string Name { get; internal set; }
    public IntSize TileSize { get; }
    public long Revision { get; private set; }
    public IReadOnlyList<TileId> TileOrder => _tileOrder;

    public TileDefinition GetTile(TileId id) => _tiles.TryGetValue(id, out var tile)
        ? tile
        : throw new KeyNotFoundException($"Tile '{id}' does not exist in tileset '{Id}'.");

    public bool ContainsTile(TileId id) => _tiles.ContainsKey(id);

    internal void AddTile(TileDefinition tile) => InsertTile(_tileOrder.Count, tile);

    internal void InsertTile(int index, TileDefinition tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        if ((uint)index > (uint)_tileOrder.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (_tiles.ContainsKey(tile.Id)) throw new InvalidOperationException($"Tile '{tile.Id}' already exists in tileset '{Id}'.");
        var nextRevision = checked(Revision + 1);
        _tiles.Add(tile.Id, tile);
        _tileOrder.Insert(index, tile.Id);
        Revision = nextRevision;
    }

    internal TileDefinition RemoveTile(TileId id)
    {
        if (!_tiles.Remove(id, out var tile))
            throw new KeyNotFoundException($"Tile '{id}' does not exist in tileset '{Id}'.");
        var nextRevision = checked(Revision + 1);
        _tileOrder.Remove(id);
        Revision = nextRevision;
        return tile;
    }

    internal int IndexOf(TileId id)
    {
        var index = _tileOrder.IndexOf(id);
        return index >= 0 ? index : throw new KeyNotFoundException($"Tile '{id}' does not exist in tileset '{Id}'.");
    }

    internal TilesetSnapshot Snapshot()
    {
        var order = _tileOrder.ToArray();
        var tiles = order.ToDictionary(id => id, GetTile);
        return new TilesetSnapshot(
            Id,
            Name,
            TileSize,
            Revision,
            Array.AsReadOnly(order),
            new ReadOnlyDictionary<TileId, TileDefinition>(tiles));
    }
}

public sealed class TilesetSnapshot
{
    internal TilesetSnapshot(
        TilesetId id,
        string name,
        IntSize tileSize,
        long revision,
        IReadOnlyList<TileId> tileOrder,
        IReadOnlyDictionary<TileId, TileDefinition> tiles)
    {
        Id = id;
        Name = name;
        TileSize = tileSize;
        Revision = revision;
        TileOrder = tileOrder;
        Tiles = tiles;
    }

    public TilesetId Id { get; }
    public string Name { get; }
    public IntSize TileSize { get; }
    public long Revision { get; }
    public IReadOnlyList<TileId> TileOrder { get; }
    public IReadOnlyDictionary<TileId, TileDefinition> Tiles { get; }

    public TileDefinition GetTile(TileId id) => Tiles.TryGetValue(id, out var tile)
        ? tile
        : throw new KeyNotFoundException($"Tile snapshot '{id}' does not exist in tileset '{Id}'.");
}

public sealed class Tilemap
{
    public const int ChunkSize = 32;

    private readonly Dictionary<TileChunkCoordinate, TileChunk> _chunks = [];

    public Tilemap(TilemapId id, string name, TilesetId tilesetId, string topologyId = "rect")
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("TilemapId cannot be empty.", nameof(id));
        if (tilesetId.Value == Guid.Empty) throw new ArgumentException("TilesetId cannot be empty.", nameof(tilesetId));
        if (string.IsNullOrWhiteSpace(topologyId)) throw new ArgumentException("Topology id cannot be empty.", nameof(topologyId));
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? "Tilemap" : name;
        TilesetId = tilesetId;
        TopologyId = topologyId;
    }

    public TilemapId Id { get; }
    public string Name { get; internal set; }
    public TilesetId TilesetId { get; }
    public string TopologyId { get; }
    public long Revision { get; private set; }
    public int OccupiedCellCount => _chunks.Values.Sum(chunk => chunk.Count);

    public TileCell? GetCell(IntPoint coordinate)
    {
        var chunkCoordinate = GetChunkCoordinate(coordinate);
        if (!_chunks.TryGetValue(chunkCoordinate, out var chunk)) return null;
        return chunk.Get(GetLocalIndex(coordinate));
    }

    public IEnumerable<KeyValuePair<IntPoint, TileCell>> EnumerateCells()
    {
        foreach (var pair in _chunks.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
        {
            for (var localY = 0; localY < ChunkSize; localY++)
            for (var localX = 0; localX < ChunkSize; localX++)
            {
                var cell = pair.Value.Get(checked(localY * ChunkSize + localX));
                if (cell is not { } value) continue;
                var coordinate = new IntPoint(
                    checked(pair.Key.X * ChunkSize + localX),
                    checked(pair.Key.Y * ChunkSize + localY));
                yield return new KeyValuePair<IntPoint, TileCell>(coordinate, value);
            }
        }
    }

    internal TileCell? SetCell(IntPoint coordinate, TileCell? cell)
    {
        var previous = GetCell(coordinate);
        if (previous == cell) return previous;

        var nextRevision = checked(Revision + 1);
        var chunkCoordinate = GetChunkCoordinate(coordinate);
        var localIndex = GetLocalIndex(coordinate);

        if (cell is { } value)
        {
            if (!_chunks.TryGetValue(chunkCoordinate, out var chunk))
            {
                chunk = new TileChunk();
                _chunks.Add(chunkCoordinate, chunk);
            }
            chunk.Set(localIndex, value);
        }
        else if (_chunks.TryGetValue(chunkCoordinate, out var chunk))
        {
            chunk.Set(localIndex, null);
            if (chunk.Count == 0) _chunks.Remove(chunkCoordinate);
        }

        Revision = nextRevision;
        return previous;
    }

    internal TilemapSnapshot Snapshot()
    {
        var chunks = _chunks.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Snapshot());
        return new TilemapSnapshot(
            Id,
            Name,
            TilesetId,
            TopologyId,
            Revision,
            new ReadOnlyDictionary<TileChunkCoordinate, TileChunkSnapshot>(chunks));
    }

    internal static TileChunkCoordinate GetChunkCoordinate(IntPoint coordinate) =>
        new(FloorDiv(coordinate.X, ChunkSize), FloorDiv(coordinate.Y, ChunkSize));

    internal static int GetLocalIndex(IntPoint coordinate)
    {
        var localX = PositiveMod(coordinate.X, ChunkSize);
        var localY = PositiveMod(coordinate.Y, ChunkSize);
        return checked(localY * ChunkSize + localX);
    }

    private static int FloorDiv(int value, int divisor)
    {
        var quotient = Math.DivRem(value, divisor, out var remainder);
        return remainder < 0 ? quotient - 1 : quotient;
    }

    private static int PositiveMod(int value, int divisor)
    {
        var remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }

    private sealed class TileChunk
    {
        private readonly TileCell?[] _cells = new TileCell?[ChunkSize * ChunkSize];

        public int Count { get; private set; }
        public TileCell? Get(int index) => _cells[index];

        public void Set(int index, TileCell? cell)
        {
            var previous = _cells[index];
            if (previous is null && cell is not null) Count++;
            else if (previous is not null && cell is null) Count--;
            _cells[index] = cell;
        }

        public TileChunkSnapshot Snapshot() => new((TileCell?[])_cells.Clone(), Count);
    }
}

public sealed class TileChunkSnapshot
{
    private readonly TileCell?[] _cells;

    internal TileChunkSnapshot(TileCell?[] cells, int count)
    {
        _cells = cells;
        Count = count;
    }

    public int Count { get; }
    internal TileCell? Get(int index) => _cells[index];
}

public sealed class TilemapSnapshot
{
    internal TilemapSnapshot(
        TilemapId id,
        string name,
        TilesetId tilesetId,
        string topologyId,
        long revision,
        IReadOnlyDictionary<TileChunkCoordinate, TileChunkSnapshot> chunks)
    {
        Id = id;
        Name = name;
        TilesetId = tilesetId;
        TopologyId = topologyId;
        Revision = revision;
        Chunks = chunks;
    }

    public TilemapId Id { get; }
    public string Name { get; }
    public TilesetId TilesetId { get; }
    public string TopologyId { get; }
    public long Revision { get; }
    public IReadOnlyDictionary<TileChunkCoordinate, TileChunkSnapshot> Chunks { get; }
    public int OccupiedCellCount => Chunks.Values.Sum(chunk => chunk.Count);

    public TileCell? GetCell(IntPoint coordinate)
    {
        var chunkCoordinate = Tilemap.GetChunkCoordinate(coordinate);
        if (!Chunks.TryGetValue(chunkCoordinate, out var chunk)) return null;
        return chunk.Get(Tilemap.GetLocalIndex(coordinate));
    }

    public IEnumerable<KeyValuePair<IntPoint, TileCell>> EnumerateCells()
    {
        foreach (var pair in Chunks.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
        {
            for (var localY = 0; localY < Tilemap.ChunkSize; localY++)
            for (var localX = 0; localX < Tilemap.ChunkSize; localX++)
            {
                var cell = pair.Value.Get(checked(localY * Tilemap.ChunkSize + localX));
                if (cell is not { } value) continue;
                var coordinate = new IntPoint(
                    checked(pair.Key.X * Tilemap.ChunkSize + localX),
                    checked(pair.Key.Y * Tilemap.ChunkSize + localY));
                yield return new KeyValuePair<IntPoint, TileCell>(coordinate, value);
            }
        }
    }
}
