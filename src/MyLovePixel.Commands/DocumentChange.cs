using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Commands;

public readonly record struct DirtySurfaceRegion(ResourceId SurfaceId, IntRect Region);

public sealed class DocumentChange
{
    public static readonly DocumentChange Empty = new([]);

    public DocumentChange(IReadOnlyList<DirtySurfaceRegion> dirtySurfaces)
    {
        DirtySurfaces = dirtySurfaces;
    }

    public IReadOnlyList<DirtySurfaceRegion> DirtySurfaces { get; }

    public static DocumentChange ForSurface(ResourceId surfaceId, IntRect region) =>
        new([new DirtySurfaceRegion(surfaceId, region)]);
}
