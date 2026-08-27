using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Resources;

public sealed class ResourceStore
{
    private readonly Dictionary<ResourceId, PixelSurface> _surfaces = [];

    public IReadOnlyCollection<ResourceId> SurfaceIds => _surfaces.Keys;

    public PixelSurface GetSurface(ResourceId id) =>
        _surfaces.TryGetValue(id, out var surface)
            ? surface
            : throw new KeyNotFoundException($"PixelSurface '{id}' does not exist.");

    public bool ContainsSurface(ResourceId id) => _surfaces.ContainsKey(id);

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
        if (!_surfaces.TryAdd(id, surface))
            throw new InvalidOperationException($"Resource '{id}' already exists.");
    }

    internal PixelSurface RemoveSurface(ResourceId id)
    {
        if (!_surfaces.Remove(id, out var surface))
            throw new KeyNotFoundException($"PixelSurface '{id}' does not exist.");
        return surface;
    }
}
