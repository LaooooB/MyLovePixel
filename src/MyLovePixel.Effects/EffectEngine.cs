using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Effects;

public sealed class EffectImage
{
    private readonly Rgba32[] _pixels;

    public EffectImage(IntSize size, IntPoint origin, IEnumerable<Rgba32> pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        Size = size;
        Origin = origin;
        _pixels = pixels.ToArray();
        var expected = checked(size.Width * size.Height);
        if (_pixels.Length != expected)
            throw new ArgumentException($"Effect image requires {expected} pixels, received {_pixels.Length}.", nameof(pixels));
    }

    public IntSize Size { get; }
    public IntPoint Origin { get; }
    public IReadOnlyList<Rgba32> Pixels => Array.AsReadOnly(_pixels);

    public Rgba32 GetPixel(int x, int y)
    {
        if ((uint)x >= (uint)Size.Width || (uint)y >= (uint)Size.Height)
            throw new ArgumentOutOfRangeException(nameof(x));
        return _pixels[checked(y * Size.Width + x)];
    }

    internal Rgba32[] ClonePixels() => (Rgba32[])_pixels.Clone();
}

public sealed record EffectEvaluationContext(
    DocumentSnapshot Snapshot,
    FrameId FrameId,
    CelSnapshot Cel);

public interface IEffectEvaluatorBackend
{
    string Id { get; }
    long Revision { get; }
    bool CanEvaluate(string effectTypeId);
    EffectImage Evaluate(
        EffectDescriptor descriptor,
        EffectInstanceSnapshot instance,
        EffectImage source,
        EffectEvaluationContext context);
}

public sealed class EffectRegistry
{
    private readonly Dictionary<string, EffectDescriptor> _descriptors = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> TypeIds => _descriptors.Keys;

    public void Register(EffectDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!_descriptors.TryAdd(descriptor.TypeId, descriptor))
            throw new InvalidOperationException($"Effect descriptor '{descriptor.TypeId}' is already registered.");
    }

    public bool TryGetDescriptor(string typeId, out EffectDescriptor descriptor) =>
        _descriptors.TryGetValue(typeId, out descriptor!);

    public EffectDescriptor GetDescriptor(string typeId) =>
        TryGetDescriptor(typeId, out var descriptor)
            ? descriptor
            : throw new KeyNotFoundException($"Effect descriptor '{typeId}' is not registered.");

    public static EffectRegistry CreateDefault()
    {
        var registry = new EffectRegistry();
        registry.Register(BuiltinEffectDescriptors.Outline);
        registry.Register(BuiltinEffectDescriptors.Shadow);
        registry.Register(BuiltinEffectDescriptors.PaletteMap);
        return registry;
    }
}

public sealed record EffectEvaluationResult(
    EffectImage Image,
    IReadOnlyList<string> UnavailableEffectTypes,
    bool CacheHit);

public sealed class EffectEngine
{
    private readonly EffectRegistry _registry;
    private readonly IEffectEvaluatorBackend _backend;
    private readonly Dictionary<EffectCacheIdentity, CacheEntry> _cache = [];

    public EffectEngine(EffectRegistry registry, IEffectEvaluatorBackend backend)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public long CacheHitCount { get; private set; }
    public long EvaluationCount { get; private set; }
    public long UnavailableEffectCount { get; private set; }

    public static EffectEngine CreateDefault()
    {
        var registry = EffectRegistry.CreateDefault();
        return new EffectEngine(registry, CpuEffectEvaluatorBackend.CreateDefault());
    }

    public EffectEvaluationResult EvaluateCel(
        DocumentSnapshot snapshot,
        FrameId frameId,
        CelSnapshot cel)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (cel.FrameId != frameId)
            throw new ArgumentException("Cel does not belong to the requested frame.", nameof(cel));
        var identity = new EffectCacheIdentity(snapshot.Id, cel.Id, frameId);
        var signature = EffectCacheSignature.Capture(snapshot, frameId, cel, _backend.Revision);
        if (_cache.TryGetValue(identity, out var cached) && cached.Signature.Equals(signature))
        {
            CacheHitCount++;
            return new EffectEvaluationResult(cached.Image, cached.UnavailableEffectTypes, true);
        }

        var context = new EffectEvaluationContext(snapshot, frameId, cel);
        var image = BuildSourceImage(snapshot, frameId, cel);
        var unavailable = new List<string>();

        foreach (var effectId in cel.Effects.EffectOrder)
        {
            var effect = cel.Effects.GetEffect(effectId);
            if (!effect.Enabled) continue;
            if (!_registry.TryGetDescriptor(effect.TypeId, out var descriptor) ||
                !_backend.CanEvaluate(effect.TypeId))
            {
                unavailable.Add(effect.TypeId);
                UnavailableEffectCount++;
                continue;
            }

            descriptor.Validate(effect);
            image = _backend.Evaluate(descriptor, effect, image, context);
            EvaluationCount++;
        }

        var unavailableView = unavailable.AsReadOnly();
        _cache[identity] = new CacheEntry(signature, image, unavailableView);
        return new EffectEvaluationResult(image, unavailableView, false);
    }

    public void ClearCaches() => _cache.Clear();

    private static EffectImage BuildSourceImage(
        DocumentSnapshot snapshot,
        FrameId frameId,
        CelSnapshot cel)
    {
        var surface = snapshot.GetSurface(cel.SurfaceId);
        snapshot.Animation.ColorCycleTrack.Values.TryGetValue(frameId, out var colorCycles);
        PaletteSnapshot? palette = null;
        PaletteId? paletteId = null;
        if (surface.Format == PixelFormat.Indexed8)
        {
            if (surface.PaletteId is not { } id)
                throw new InvalidOperationException($"Indexed8 surface '{cel.SurfaceId}' has no palette reference.");
            paletteId = id;
            palette = snapshot.GetPalette(id);
        }

        var pixels = new Rgba32[checked(surface.Size.Width * surface.Size.Height)];
        for (var y = 0; y < surface.Size.Height; y++)
        for (var x = 0; x < surface.Size.Width; x++)
        {
            pixels[checked(y * surface.Size.Width + x)] = surface.Format switch
            {
                PixelFormat.Rgba32 => surface.GetPixel(x, y),
                PixelFormat.Indexed8 => ResolveIndexed(surface, x, y, paletteId!.Value, palette!, colorCycles),
                _ => throw new NotSupportedException($"Surface format '{surface.Format}' cannot feed the effect engine."),
            };
        }
        return new EffectImage(surface.Size, IntPoint.Zero, pixels);
    }

    private static Rgba32 ResolveIndexed(
        PixelSurfaceSnapshot surface,
        int x,
        int y,
        PaletteId paletteId,
        PaletteSnapshot palette,
        ColorCycleFrameValue? colorCycles)
    {
        var index = surface.GetIndex(x, y);
        var resolved = colorCycles?.ResolveIndex(paletteId, index) ?? index;
        return palette.ResolveColor(resolved);
    }

    private readonly record struct EffectCacheIdentity(
        DocumentId DocumentId,
        CelId CelId,
        FrameId FrameId);

    private sealed record CacheEntry(
        EffectCacheSignature Signature,
        EffectImage Image,
        IReadOnlyList<string> UnavailableEffectTypes);
}

internal sealed class EffectCacheSignature : IEquatable<EffectCacheSignature>
{
    private readonly EffectState[] _effects;
    private readonly PaletteState[] _palettes;
    private readonly ColorCycleFrameValue? _colorCycles;

    private EffectCacheSignature(
        ResourceId surfaceId,
        long surfaceRevision,
        long graphRevision,
        long backendRevision,
        ColorCycleFrameValue? colorCycles,
        EffectState[] effects,
        PaletteState[] palettes)
    {
        SurfaceId = surfaceId;
        SurfaceRevision = surfaceRevision;
        GraphRevision = graphRevision;
        BackendRevision = backendRevision;
        _colorCycles = colorCycles;
        _effects = effects;
        _palettes = palettes;
    }

    private ResourceId SurfaceId { get; }
    private long SurfaceRevision { get; }
    private long GraphRevision { get; }
    private long BackendRevision { get; }

    public static EffectCacheSignature Capture(
        DocumentSnapshot snapshot,
        FrameId frameId,
        CelSnapshot cel,
        long backendRevision)
    {
        var surface = snapshot.GetSurface(cel.SurfaceId);
        snapshot.Animation.ColorCycleTrack.Values.TryGetValue(frameId, out var colorCycles);
        var effects = cel.Effects.EffectOrder
            .Select(id => cel.Effects.GetEffect(id))
            .Select(effect => new EffectState(effect.Id, effect.TypeId, effect.Enabled, effect.Revision))
            .ToArray();
        var palettes = snapshot.Palettes
            .OrderBy(pair => pair.Key.Value)
            .Select(pair => new PaletteState(pair.Key, pair.Value.Revision))
            .ToArray();
        return new EffectCacheSignature(
            cel.SurfaceId,
            surface.Revision,
            cel.Effects.Revision,
            backendRevision,
            colorCycles,
            effects,
            palettes);
    }

    public bool Equals(EffectCacheSignature? other) =>
        other is not null &&
        SurfaceId == other.SurfaceId &&
        SurfaceRevision == other.SurfaceRevision &&
        GraphRevision == other.GraphRevision &&
        BackendRevision == other.BackendRevision &&
        Equals(_colorCycles, other._colorCycles) &&
        _effects.AsSpan().SequenceEqual(other._effects) &&
        _palettes.AsSpan().SequenceEqual(other._palettes);

    public override bool Equals(object? obj) => obj is EffectCacheSignature other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SurfaceId);
        hash.Add(SurfaceRevision);
        hash.Add(GraphRevision);
        hash.Add(BackendRevision);
        hash.Add(_colorCycles);
        foreach (var effect in _effects) hash.Add(effect);
        foreach (var palette in _palettes) hash.Add(palette);
        return hash.ToHashCode();
    }

    private readonly record struct EffectState(
        EffectInstanceId Id,
        string TypeId,
        bool Enabled,
        long Revision);

    private readonly record struct PaletteState(
        PaletteId Id,
        long Revision);
}
