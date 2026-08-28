using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Tilemap;

public interface IGridTopology
{
    string Id { get; }
    IntPoint GetCellOrigin(IntPoint coordinate, IntSize tileSize);
    IReadOnlyList<IntPoint> GetNeighborCoordinates(IntPoint coordinate);
}

public sealed class RectGridTopology : IGridTopology
{
    private static readonly IntPoint[] NeighborOffsets =
    [
        new(0, -1),
        new(1, 0),
        new(0, 1),
        new(-1, 0),
    ];

    public static RectGridTopology Instance { get; } = new();
    private RectGridTopology() { }
    public string Id => "rect";

    public IntPoint GetCellOrigin(IntPoint coordinate, IntSize tileSize) =>
        new(checked(coordinate.X * tileSize.Width), checked(coordinate.Y * tileSize.Height));

    public IReadOnlyList<IntPoint> GetNeighborCoordinates(IntPoint coordinate) =>
        NeighborOffsets.Select(offset => new IntPoint(
            checked(coordinate.X + offset.X),
            checked(coordinate.Y + offset.Y))).ToArray();
}

public sealed class IsometricDiamondGridTopology : IGridTopology
{
    private static readonly IntPoint[] NeighborOffsets =
    [
        new(0, -1),
        new(1, 0),
        new(0, 1),
        new(-1, 0),
    ];

    public static IsometricDiamondGridTopology Instance { get; } = new();
    private IsometricDiamondGridTopology() { }
    public string Id => "iso-diamond";

    public IntPoint GetCellOrigin(IntPoint coordinate, IntSize tileSize)
    {
        var halfWidth = Math.Max(1, tileSize.Width / 2);
        var halfHeight = Math.Max(1, tileSize.Height / 2);
        return new IntPoint(
            checked((coordinate.X - coordinate.Y) * halfWidth),
            checked((coordinate.X + coordinate.Y) * halfHeight));
    }

    public IReadOnlyList<IntPoint> GetNeighborCoordinates(IntPoint coordinate) =>
        NeighborOffsets.Select(offset => new IntPoint(
            checked(coordinate.X + offset.X),
            checked(coordinate.Y + offset.Y))).ToArray();
}

public sealed class HexOddRowGridTopology : IGridTopology
{
    public static HexOddRowGridTopology Instance { get; } = new();
    private HexOddRowGridTopology() { }
    public string Id => "hex-odd-r";

    public IntPoint GetCellOrigin(IntPoint coordinate, IntSize tileSize)
    {
        var halfWidth = tileSize.Width / 2;
        var rowStep = Math.Max(1, checked(tileSize.Height * 3 / 4));
        var offset = (coordinate.Y & 1) != 0 ? halfWidth : 0;
        return new IntPoint(
            checked(coordinate.X * tileSize.Width + offset),
            checked(coordinate.Y * rowStep));
    }

    public IReadOnlyList<IntPoint> GetNeighborCoordinates(IntPoint coordinate)
    {
        var odd = (coordinate.Y & 1) != 0;
        var offsets = odd
            ? new[] { new IntPoint(0, -1), new IntPoint(1, -1), new IntPoint(1, 0), new IntPoint(1, 1), new IntPoint(0, 1), new IntPoint(-1, 0) }
            : new[] { new IntPoint(-1, -1), new IntPoint(0, -1), new IntPoint(1, 0), new IntPoint(0, 1), new IntPoint(-1, 1), new IntPoint(-1, 0) };
        return offsets.Select(offset => new IntPoint(
            checked(coordinate.X + offset.X),
            checked(coordinate.Y + offset.Y))).ToArray();
    }
}

public sealed class GridTopologyRegistry
{
    private readonly Dictionary<string, IGridTopology> _topologies = new(StringComparer.Ordinal);

    public static GridTopologyRegistry CreateDefault()
    {
        var registry = new GridTopologyRegistry();
        registry.Register(RectGridTopology.Instance);
        registry.Register(IsometricDiamondGridTopology.Instance);
        registry.Register(HexOddRowGridTopology.Instance);
        return registry;
    }

    public void Register(IGridTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        if (string.IsNullOrWhiteSpace(topology.Id)) throw new ArgumentException("Topology id cannot be empty.", nameof(topology));
        if (!_topologies.TryAdd(topology.Id, topology))
            throw new InvalidOperationException($"Grid topology '{topology.Id}' is already registered.");
    }

    public IGridTopology Resolve(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _topologies.TryGetValue(id, out var topology)
            ? topology
            : throw new KeyNotFoundException($"Grid topology '{id}' is not registered.");
    }
}
