using MyLovePixel.Commands;
using MyLovePixel.Commands.Effects;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Effects;
using Xunit;

namespace MyLovePixel.Render.Tests;

public sealed class EffectRenderTests
{
    [Fact]
    public void Outline_RendersNonDestructivelyInsideFrameComposite()
    {
        var document = PixelDocumentFactory.CreateBlank(3, 3);
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        surface.SetPixel(1, 1, new Rgba32(255, 0, 0, 255));
        var revision = surface.Revision;
        var effect = new EffectInstance(EffectInstanceId.New(), BuiltinEffectDescriptors.Outline.TypeId);
        effect.SetParameter("radius", EffectValue.Integer(1), out _);
        effect.SetParameter("color", EffectValue.Color(new Rgba32(0, 0, 0, 255)), out _);
        cel.Effects.Add(effect);

        var result = new FrameRenderer().Render(
            DocumentSnapshot.Capture(document),
            new FrameRenderRequest(cel.FrameId));

        Assert.Equal(new Rgba32(255, 0, 0, 255), result.Surface.GetPixel(1, 1));
        Assert.Equal(new Rgba32(0, 0, 0, 255), result.Surface.GetPixel(0, 1));
        Assert.Equal(new Rgba32(0, 0, 0, 255), result.Surface.GetPixel(1, 0));
        Assert.Equal(revision, surface.Revision);
    }

    [Fact]
    public void EffectParameterChange_InvalidatesFrameStructureExactly()
    {
        var document = PixelDocumentFactory.CreateBlank(3, 3);
        var cel = document.Cels.Single();
        document.Resources.GetSurface(cel.SurfaceId).SetPixel(1, 1, new Rgba32(255, 0, 0, 255));
        var bus = new CommandBus(document);
        var add = new AddEffectCommand(cel.Id, BuiltinEffectDescriptors.Outline.TypeId);
        bus.Execute(add);
        bus.Execute(new SetEffectParameterCommand(
            cel.Id,
            add.EffectId,
            "color",
            EffectValue.Color(new Rgba32(0, 0, 0, 255)),
            BuiltinEffectDescriptors.Outline));
        var renderer = new FrameRenderer();
        renderer.Render(DocumentSnapshot.Capture(document), new FrameRenderRequest(cel.FrameId));

        bus.Execute(new SetEffectParameterCommand(
            cel.Id,
            add.EffectId,
            "color",
            EffectValue.Color(new Rgba32(0, 255, 0, 255)),
            BuiltinEffectDescriptors.Outline));
        var second = renderer.Render(
            DocumentSnapshot.Capture(document),
            new FrameRenderRequest(cel.FrameId));

        Assert.Equal(RenderCacheOutcome.FullRecompose, second.CacheOutcome);
        Assert.Equal(new Rgba32(0, 255, 0, 255), second.Surface.GetPixel(0, 1));
    }

    [Fact]
    public void SurfaceDirtyWithEffect_UsesFullFallbackUntilEffectAwareDirtyPropagationExists()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document);
        var add = new AddEffectCommand(cel.Id, BuiltinEffectDescriptors.Outline.TypeId);
        bus.Execute(add);
        var renderer = new FrameRenderer();
        renderer.Render(DocumentSnapshot.Capture(document), new FrameRenderRequest(cel.FrameId));
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        var fromRevision = surface.Revision;

        bus.Execute(new PixelPatchCommand(
            cel.SurfaceId,
            [new PixelWrite(2, 2, new Rgba32(255, 255, 255, 255))]));
        var toRevision = surface.Revision;
        var second = renderer.Render(
            DocumentSnapshot.Capture(document),
            new FrameRenderRequest(
                cel.FrameId,
                [new SurfaceInvalidation(
                    cel.SurfaceId,
                    fromRevision,
                    toRevision,
                    new IntRect(2, 2, 1, 1))]));

        Assert.Equal(RenderCacheOutcome.FullRecompose, second.CacheOutcome);
        Assert.Equal(new Rgba32(255, 255, 255, 255), second.Surface.GetPixel(2, 2));
    }

    [Fact]
    public void UnknownEffect_PassesThroughAndCacheClearReevaluatesNestedEffectCache()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var cel = document.Cels.Single();
        var color = new Rgba32(8, 9, 10, 255);
        document.Resources.GetSurface(cel.SurfaceId).SetPixel(0, 0, color);
        cel.Effects.Add(new EffectInstance(EffectInstanceId.New(), "vendor.not-installed"));
        var engine = EffectEngine.CreateDefault();
        var graph = new RenderGraph();
        graph.Add(new FrameCompositeRenderNode(engine));
        var renderer = new FrameRenderer(graph);
        var snapshot = DocumentSnapshot.Capture(document);

        var first = renderer.Render(snapshot, new FrameRenderRequest(cel.FrameId));
        var second = renderer.Render(snapshot, new FrameRenderRequest(cel.FrameId));
        renderer.ClearCaches();
        var third = renderer.Render(snapshot, new FrameRenderRequest(cel.FrameId));

        Assert.Equal(color, first.Surface.GetPixel(0, 0));
        Assert.Equal(RenderCacheOutcome.CacheHit, second.CacheOutcome);
        Assert.Equal(RenderCacheOutcome.FullRecompose, third.CacheOutcome);
        Assert.Equal(2, engine.UnavailableEffectCount);
    }
}
