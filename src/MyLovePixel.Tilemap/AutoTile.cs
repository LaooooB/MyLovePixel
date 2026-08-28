using System.Buffers.Binary;
using System.Text;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;

namespace MyLovePixel.Tilemap;

public enum TileNeighborMode
{
    Four = 4,
    Eight = 8,
}

[Flags]
public enum TileNeighborMask : byte
{
    None = 0,
    North = 1 << 0,
    East = 1 << 1,
    South = 1 << 2,
    West = 1 << 3,
    NorthEast = 1 << 4,
    SouthEast = 1 << 5,
    SouthWest = 1 << 6,
    NorthWest = 1 << 7,
}

public static class TileNeighborMaskCalculator
{
    public static TileNeighborMask Calculate(
        TilemapSnapshot tilemap,
        IntPoint coordinate,
        TileNeighborMode mode,
        Func<TileCell, bool>? connects = null)
    {
        ArgumentNullException.ThrowIfNull(tilemap);
        if (mode is not TileNeighborMode.Four and not TileNeighborMode.Eight)
            throw new ArgumentOutOfRangeException(nameof(mode));

        var result = TileNeighborMask.None;
        AddIfConnected(tilemap, coordinate, 0, -1, TileNeighborMask.North, connects, ref result);
        AddIfConnected(tilemap, coordinate, 1, 0, TileNeighborMask.East, connects, ref result);
        AddIfConnected(tilemap, coordinate, 0, 1, TileNeighborMask.South, connects, ref result);
        AddIfConnected(tilemap, coordinate, -1, 0, TileNeighborMask.West, connects, ref result);

        if (mode == TileNeighborMode.Eight)
        {
            AddIfConnected(tilemap, coordinate, 1, -1, TileNeighborMask.NorthEast, connects, ref result);
            AddIfConnected(tilemap, coordinate, 1, 1, TileNeighborMask.SouthEast, connects, ref result);
            AddIfConnected(tilemap, coordinate, -1, 1, TileNeighborMask.SouthWest, connects, ref result);
            AddIfConnected(tilemap, coordinate, -1, -1, TileNeighborMask.NorthWest, connects, ref result);
        }

        return result;
    }

    private static void AddIfConnected(
        TilemapSnapshot tilemap,
        IntPoint coordinate,
        int dx,
        int dy,
        TileNeighborMask flag,
        Func<TileCell, bool>? connects,
        ref TileNeighborMask result)
    {
        var neighbor = tilemap.GetCell(new IntPoint(
            checked(coordinate.X + dx),
            checked(coordinate.Y + dy)));
        if (neighbor is not { } value) return;
        if (connects is not null && !connects(value)) return;
        result |= flag;
    }
}

public sealed record AutoTileContext(
    DocumentSnapshot Document,
    TilemapSnapshot Tilemap,
    TilesetSnapshot Tileset,
    IntPoint Coordinate)
{
    public static AutoTileContext Create(DocumentSnapshot document, TilemapId tilemapId, IntPoint coordinate)
    {
        ArgumentNullException.ThrowIfNull(document);
        var tilemap = document.GetTilemap(tilemapId);
        return new AutoTileContext(document, tilemap, document.GetTileset(tilemap.TilesetId), coordinate);
    }
}

public interface IAutoTileRule
{
    string Id { get; }
    TileCell Resolve(AutoTileContext context);
}

public sealed record WeightedTileVariant
{
    public WeightedTileVariant(
        TileId tileId,
        int weight,
        TileCellFlags flags = TileCellFlags.None,
        ushort variant = 0)
    {
        if (tileId.Value == Guid.Empty) throw new ArgumentException("TileId cannot be empty.", nameof(tileId));
        if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be positive.");
        _ = new TileCell(tileId, flags, variant);
        TileId = tileId;
        Weight = weight;
        Flags = flags;
        Variant = variant;
    }

    public TileId TileId { get; }
    public int Weight { get; }
    public TileCellFlags Flags { get; }
    public ushort Variant { get; }
}

public sealed class BitmaskAutoTileRule : IAutoTileRule
{
    private readonly Dictionary<TileNeighborMask, WeightedTileVariant[]> _variants;
    private readonly WeightedTileVariant[]? _fallback;
    private readonly Func<TileCell, bool>? _connects;

    public BitmaskAutoTileRule(
        string id,
        TileNeighborMode neighborMode,
        IReadOnlyDictionary<TileNeighborMask, IEnumerable<WeightedTileVariant>> variants,
        IEnumerable<WeightedTileVariant>? fallback = null,
        Func<TileCell, bool>? connects = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(variants);
        if (neighborMode is not TileNeighborMode.Four and not TileNeighborMode.Eight)
            throw new ArgumentOutOfRangeException(nameof(neighborMode));

        Id = id;
        NeighborMode = neighborMode;
        _connects = connects;
        _variants = new Dictionary<TileNeighborMask, WeightedTileVariant[]>();
        foreach (var pair in variants)
        {
            var values = pair.Value?.ToArray() ?? throw new ArgumentException("Variant collections cannot be null.", nameof(variants));
            if (values.Length == 0) throw new ArgumentException($"Mask '{pair.Key}' must contain at least one variant.", nameof(variants));
            _variants.Add(pair.Key, values);
        }

        _fallback = fallback?.ToArray();
        if (_fallback is { Length: 0 }) throw new ArgumentException("Fallback variants cannot be empty.", nameof(fallback));
    }

    public string Id { get; }
    public TileNeighborMode NeighborMode { get; }

    public TileCell Resolve(AutoTileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Tilemap.TilesetId != context.Tileset.Id)
            throw new ArgumentException("AutoTile context tilemap and tileset do not match.", nameof(context));

        var mask = TileNeighborMaskCalculator.Calculate(
            context.Tilemap,
            context.Coordinate,
            NeighborMode,
            _connects);
        var candidates = _variants.TryGetValue(mask, out var exact)
            ? exact
            : _fallback ?? throw new InvalidOperationException($"AutoTile rule '{Id}' has no variants for mask '{mask}'.");

        foreach (var candidate in candidates)
        {
            if (!context.Tileset.Tiles.ContainsKey(candidate.TileId))
                throw new InvalidOperationException(
                    $"AutoTile rule '{Id}' references tile '{candidate.TileId}' outside tileset '{context.Tileset.Id}'.");
        }

        var selected = DeterministicWeightedTileSelector.Select(
            context.Document.Seed,
            context.Tilemap.Id,
            context.Coordinate,
            Id,
            mask,
            candidates);
        return new TileCell(selected.TileId, selected.Flags, selected.Variant);
    }
}

public static class DeterministicWeightedTileSelector
{
    public static WeightedTileVariant Select(
        ulong documentSeed,
        TilemapId tilemapId,
        IntPoint coordinate,
        string ruleId,
        TileNeighborMask mask,
        IReadOnlyList<WeightedTileVariant> variants)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(variants);
        if (tilemapId.Value == Guid.Empty) throw new ArgumentException("TilemapId cannot be empty.", nameof(tilemapId));
        if (variants.Count == 0) throw new ArgumentException("At least one weighted variant is required.", nameof(variants));

        long totalWeight = 0;
        foreach (var variant in variants)
            totalWeight = checked(totalWeight + variant.Weight);

        var state = documentSeed;
        state = Mix(state ^ HashGuid(tilemapId.Value));
        state = Mix(state ^ unchecked((ulong)(uint)coordinate.X));
        state = Mix(state ^ (unchecked((ulong)(uint)coordinate.Y) << 1));
        state = Mix(state ^ (byte)mask);
        state = Mix(state ^ HashString(ruleId));
        var roll = (long)(state % (ulong)totalWeight);

        long cumulative = 0;
        foreach (var variant in variants)
        {
            cumulative += variant.Weight;
            if (roll < cumulative) return variant;
        }

        throw new InvalidOperationException("Weighted tile selection failed to choose a variant.");
    }

    private static ulong HashGuid(Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        if (!value.TryWriteBytes(bytes)) throw new InvalidOperationException("Unable to encode TilemapId.");
        return Fnv1A(bytes);
    }

    private static ulong HashString(string value) => Fnv1A(Encoding.UTF8.GetBytes(value));

    private static ulong Fnv1A(ReadOnlySpan<byte> bytes)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var value in bytes)
        {
            hash ^= value;
            hash *= prime;
        }
        return hash;
    }

    private static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}

public readonly record struct TileCellWrite(IntPoint Coordinate, TileCell? Cell);

public sealed class TilemapCellPatch
{
    private readonly TileCellWrite[] _writes;

    public TilemapCellPatch(IEnumerable<TileCellWrite> writes)
    {
        ArgumentNullException.ThrowIfNull(writes);
        _writes = writes
            .GroupBy(write => write.Coordinate)
            .Select(group => group.Last())
            .OrderBy(write => write.Coordinate.Y)
            .ThenBy(write => write.Coordinate.X)
            .ToArray();
    }

    public IReadOnlyList<TileCellWrite> Writes => Array.AsReadOnly(_writes);
}

public static class AutoTileEngine
{
    public static TilemapCellPatch Resolve(
        DocumentSnapshot document,
        TilemapId tilemapId,
        IAutoTileRule rule,
        IEnumerable<IntPoint> coordinates)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(coordinates);
        var writes = coordinates
            .Distinct()
            .Select(coordinate => new TileCellWrite(
                coordinate,
                rule.Resolve(AutoTileContext.Create(document, tilemapId, coordinate))))
            .ToArray();
        return new TilemapCellPatch(writes);
    }
}
