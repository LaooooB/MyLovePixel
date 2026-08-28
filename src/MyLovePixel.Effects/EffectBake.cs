using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Effects;

public sealed record EffectRevisionStamp(
    EffectInstanceId EffectId,
    long Revision);

public sealed record PaletteRevisionStamp(
    PaletteId PaletteId,
    long Revision);

public sealed record EffectBakePlan(
    CelId CelId,
    FrameId FrameId,
    ResourceId SourceSurfaceId,
    long SourceSurfaceRevision,
    IntPoint SourcePosition,
    long EffectGraphRevision,
    IReadOnlyList<EffectRevisionStamp> Effects,
    IReadOnlyList<PaletteRevisionStamp> Palettes,
    ColorCycleFrameValue? ColorCycles,
    EffectImage Image);

public sealed class EffectBakePlanner
{
    private readonly EffectEngine _engine;

    public EffectBakePlanner(EffectEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public EffectBakePlan Prepare(
        DocumentSnapshot snapshot,
        FrameId frameId,
        CelSnapshot cel)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (cel.FrameId != frameId)
            throw new ArgumentException("Cel does not belong to the requested frame.", nameof(cel));
        if (cel.Effects.EffectOrder.Count == 0)
            throw new InvalidOperationException("Cannot bake a Cel with no effects.");

        var result = _engine.EvaluateCel(snapshot, frameId, cel);
        if (result.UnavailableEffectTypes.Count != 0)
        {
            throw new InvalidOperationException(
                $"Cannot bake while effect evaluators are unavailable: {string.Join(", ", result.UnavailableEffectTypes.Distinct(StringComparer.Ordinal))}.");
        }

        var surface = snapshot.GetSurface(cel.SurfaceId);
        var effects = cel.Effects.EffectOrder
            .Select(cel.Effects.GetEffect)
            .Select(effect => new EffectRevisionStamp(effect.Id, effect.Revision))
            .ToArray();
        var palettes = snapshot.Palettes
            .OrderBy(pair => pair.Key.Value)
            .Select(pair => new PaletteRevisionStamp(pair.Key, pair.Value.Revision))
            .ToArray();
        snapshot.Animation.ColorCycleTrack.Values.TryGetValue(frameId, out var colorCycles);

        return new EffectBakePlan(
            cel.Id,
            frameId,
            cel.SurfaceId,
            surface.Revision,
            cel.Position,
            cel.Effects.Revision,
            Array.AsReadOnly(effects),
            Array.AsReadOnly(palettes),
            colorCycles,
            result.Image);
    }
}
