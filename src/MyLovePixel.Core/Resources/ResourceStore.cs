using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Resources;

public sealed class ResourceStore
{
    private readonly Dictionary<ResourceId, PixelSurface> _surfaces = [];
    private readonly Dictionary<PaletteId, Palette> _palettes = [];

    public IReadOnlyCollection<ResourceId> SurfaceIds => _surfaces.Keys;
    public IReadOnlyCollection<PaletteId> PaletteIds => _palettes.Keys;

    public PixelSurface GetSurface(ResourceId id) =>
        _surfaces.TryGetValue(id, out var surface)
            ? surface
            : throw new KeyNotFoundException($"PixelSurface '{id}' does not exist.");

    public Palette GetPalette(PaletteId id) =>
        _palettes.TryGetValue(id, out var palette)
            ? palette
            : throw new KeyNotFoundException($"Palette '{id}' does not exist.");

    public bool ContainsSurface(ResourceId id) => _surfaces.ContainsKey(id);
    public bool ContainsPalette(PaletteId id) => _palettes.ContainsKey(id);

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
