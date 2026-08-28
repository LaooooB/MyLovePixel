using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Commands;

public readonly record struct DirtySurfaceRegion(ResourceId SurfaceId, IntRect Region);
public readonly record struct DirtyTilemapCell(TilemapId TilemapId, IntPoint Coordinate);

public sealed class DocumentChange
{
    public static readonly DocumentChange Empty = new([], []);

    public DocumentChange(IReadOnlyList<DirtySurfaceRegion> dirtySurfaces)
        : this(dirtySurfaces, [])
    {
    }

    public DocumentChange(
        IReadOnlyList<DirtySurfaceRegion> dirtySurfaces,
        IReadOnlyList<DirtyTilemapCell> dirtyTilemapCells)
    {
        DirtySurfaces = dirtySurfaces ?? throw new ArgumentNullException(nameof(dirtySurfaces));
        DirtyTilemapCells = dirtyTilemapCells ?? throw new ArgumentNullException(nameof(dirtyTilemapCells));
    }

    public IReadOnlyList<DirtySurfaceRegion> DirtySurfaces { get; }
    public IReadOnlyList<DirtyTilemapCell> DirtyTilemapCells { get; }

    public static DocumentChange ForSurface(ResourceId surfaceId, IntRect region) =>
        new([new DirtySurfaceRegion(surfaceId, region)], []);

    public static DocumentChange ForTilemapCell(TilemapId tilemapId, IntPoint coordinate) =>
        new([], [new DirtyTilemapCell(tilemapId, coordinate)]);

    public static DocumentChange ForTilemapCells(TilemapId tilemapId, IEnumerable<IntPoint> coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        var cells = coordinates
            .Distinct()
            .Select(coordinate => new DirtyTilemapCell(tilemapId, coordinate))
            .ToArray();
        return cells.Length == 0 ? Empty : new DocumentChange([], cells);
    }
}
