using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Effects.Tests;

public sealed class EffectEngineTests
{
    [Fact]
    public void Outline_ExpandsBoundsWithoutMutatingSourceSurface()
    {
        var document = PixelDocumentFactory.CreateBlank(3, 3);
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        surface.SetPixel(1, 1, new Rgba32(255, 0, 0, 255));
        var sourceRevision = surface.Revision;
        var effect = new EffectInstance(EffectInstanceId.New(), BuiltinEffectDescriptors.Outline.TypeId);
        effect.SetParameter("radius", EffectValue.Integer(1), out _);
        effect.SetParameter("color", EffectValue.Color(new Rgba32(0, 0, 0, 255)), out _);
        cel.Effects.Add(effect);
        var snapshot = DocumentSnapshot.Capture(document);

        var result = EffectEngine.CreateDefault().EvaluateCel(snapshot, cel.FrameId, snapshot.Cels.Single());

        Assert.Equal(new IntSize(5, 5), result.Image.Size);
        Assert.Equal(new IntPoint(-1, -1), result.Image.Origin);
        Assert.Equal(new Rgba32(255, 0, 0, 255), result.Image.GetPixel(2, 2));
        Assert.Equal(new Rgba32(0, 0, 0, 255), result.Image.GetPixel(1, 2));
        Assert.Equal(sourceRevision, surface.Revision);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface.GetPixel(1, 1));
    }

    [Fact]
    public void AnimatedParameter_UsesCurrentFrameTrackValue()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var cel = document.Cels.Single();
        document.Resources.GetSurface(cel.SurfaceId).SetPixel(0, 0, new Rgba32(255, 255, 255, 255));
        var effect = new EffectInstance(EffectInstanceId.New(), BuiltinEffectDescriptors.Outline.TypeId);
        effect.SetParameter("radius", EffectValue.Integer(1), out _);
        effect.SetKeyframe(
            "radius",
            cel.FrameId,
            EffectValue.Integer(2),
            AnimationTrackId.New(),
            out _,
            out _);
        cel.Effects.Add(effect);
        var snapshot = DocumentSnapshot.Capture(document);

        var result = EffectEngine.CreateDefault().EvaluateCel(snapshot, cel.FrameId, snapshot.Cels.Single());

        Assert.Equal(new IntSize(5, 5), result.Image.Size);
        Assert.Equal(new IntPoint(-2, -2), result.Image.Origin);
    }

    [Fact]
    public void Shadow_WithNegativeOffsetPreservesExpandedOrigin()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var cel = document.Cels.Single();
        document.Resources.GetSurface(cel.SurfaceId).SetPixel(0, 0, new Rgba32(255, 255, 255, 255));
        var effect = new EffectInstance(EffectInstanceId.New(), BuiltinEffectDescriptors.Shadow.TypeId);
        effect.SetParameter("offset", EffectValue.Point(new IntPoint(-2, 1)), out _);
        effect.SetParameter("color", EffectValue.Color(new Rgba32(0, 0, 0, 255)), out _);
        cel.Effects.Add(effect);
        var snapshot = DocumentSnapshot.Capture(document);

        var result = EffectEngine.CreateDefault().EvaluateCel(snapshot, cel.FrameId, snapshot.Cels.Single());

        Assert.Equal(new IntPoint(-2, 0), result.Image.Origin);
        Assert.Equal(new IntSize(3, 2), result.Image.Size);
        Assert.Equal(new Rgba32(255, 255, 255, 255), result.Image.GetPixel(2, 0));
        Assert.Equal(new Rgba32(0, 0, 0, 255), result.Image.GetPixel(0, 1));
    }

    [Fact]
    public void PaletteMap_ReplacesMatchingPaletteColorsFromSnapshot()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var cel = document.Cels.Single();
        var sourceColor = new Rgba32(10, 20, 30, 255);
        var targetColor = new Rgba32(200, 210, 220, 255);
        document.Resources.GetSurface(cel.SurfaceId).SetPixel(0, 0, sourceColor);
        var sourcePaletteId = PaletteId.New();
        var targetPaletteId = PaletteId.New();
        document.Resources.AddPalette(sourcePaletteId, new Palette([sourceColor]));
        document.Resources.AddPalette(targetPaletteId, new Palette([targetColor]));
        var effect = new EffectInstance(EffectInstanceId.New(), BuiltinEffectDescriptors.PaletteMap.TypeId);
        effect.SetParameter("sourcePalette", EffectValue.PaletteReference(sourcePaletteId), out _);
        effect.SetParameter("targetPalette", EffectValue.PaletteReference(targetPaletteId), out _);
        cel.Effects.Add(effect);
        var snapshot = DocumentSnapshot.Capture(document);

        var result = EffectEngine.CreateDefault().EvaluateCel(snapshot, cel.FrameId, snapshot.Cels.Single());

        Assert.Equal(targetColor, result.Image.GetPixel(0, 0));
    }

    [Fact]
    public void UnknownEffect_PassesThroughAndExactSignatureCachesResult()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var cel = document.Cels.Single();
        var color = new Rgba32(12, 34, 56, 255);
        document.Resources.GetSurface(cel.SurfaceId).SetPixel(0, 0, color);
        var unknown = new EffectInstance(EffectInstanceId.New(), "vendor.missing");
        unknown.SetParameter("future", EffectValue.Number(0.5), out _);
        cel.Effects.Add(unknown);
        var engine = EffectEngine.CreateDefault();
        var snapshot = DocumentSnapshot.Capture(document);
        var celSnapshot = snapshot.Cels.Single();

        var first = engine.EvaluateCel(snapshot, cel.FrameId, celSnapshot);
        var second = engine.EvaluateCel(snapshot, cel.FrameId, celSnapshot);

        Assert.False(first.CacheHit);
        Assert.True(second.CacheHit);
        Assert.Equal(color, second.Image.GetPixel(0, 0));
        Assert.Equal(new[] { "vendor.missing" }, second.UnavailableEffectTypes);
        Assert.Equal(1, engine.CacheHitCount);
    }
}
