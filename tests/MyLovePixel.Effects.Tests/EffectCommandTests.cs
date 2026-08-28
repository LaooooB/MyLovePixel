using MyLovePixel.Commands;
using MyLovePixel.Commands.Effects;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Effects.Tests;

public sealed class EffectCommandTests
{
    [Fact]
    public void AddSetAnimateUndo_UsesOneCommandEntryPerMutation()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document);
        var add = new AddEffectCommand(cel.Id, BuiltinEffectDescriptors.Outline.TypeId);
        bus.Execute(add);
        bus.Execute(new SetEffectParameterCommand(
            cel.Id,
            add.EffectId,
            "radius",
            EffectValue.Integer(2),
            BuiltinEffectDescriptors.Outline));
        bus.Execute(new SetEffectParameterKeyframeCommand(
            cel.Id,
            add.EffectId,
            cel.FrameId,
            "radius",
            EffectValue.Integer(3),
            BuiltinEffectDescriptors.Outline));

        var effect = cel.Effects.GetEffect(add.EffectId);
        Assert.Equal(EffectValue.Integer(2), effect.Parameters["radius"]);
        Assert.Equal(EffectValue.Integer(3), effect.ParameterTracks["radius"].Values[cel.FrameId]);

        bus.Undo();
        Assert.False(effect.ParameterTracks.ContainsKey("radius"));
        bus.Undo();
        Assert.False(effect.Parameters.ContainsKey("radius"));
        bus.Undo();
        Assert.Empty(cel.Effects.EffectOrder);
    }

    [Fact]
    public void BakeEffects_CreatesIndependentRgbaSurfaceAndUndoRestoresGraph()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var cel = document.Cels.Single();
        var sourceId = cel.SurfaceId;
        var source = document.Resources.GetSurface(sourceId);
        source.SetPixel(0, 0, new Rgba32(255, 0, 0, 255));
        var bus = new CommandBus(document);
        var add = new AddEffectCommand(cel.Id, BuiltinEffectDescriptors.Outline.TypeId);
        bus.Execute(add);
        bus.Execute(new SetEffectParameterCommand(
            cel.Id,
            add.EffectId,
            "radius",
            EffectValue.Integer(1),
            BuiltinEffectDescriptors.Outline));
        var snapshot = DocumentSnapshot.Capture(document);
        var plan = new EffectBakePlanner(EffectEngine.CreateDefault())
            .Prepare(snapshot, cel.FrameId, snapshot.Cels.Single());
        var bake = new BakeEffectsCommand(plan);

        bus.Execute(bake);

        Assert.Equal(bake.BakedSurfaceId, cel.SurfaceId);
        Assert.Equal(new IntPoint(-1, -1), cel.Position);
        Assert.Empty(cel.Effects.EffectOrder);
        Assert.Equal(new IntSize(3, 3), document.Resources.GetSurface(cel.SurfaceId).Size);
        Assert.Equal(new Rgba32(255, 0, 0, 255), document.Resources.GetSurface(cel.SurfaceId).GetPixel(1, 1));
        Assert.Equal(new Rgba32(255, 0, 0, 255), source.GetPixel(0, 0));

        bus.Undo();

        Assert.Equal(sourceId, cel.SurfaceId);
        Assert.Equal(IntPoint.Zero, cel.Position);
        Assert.Single(cel.Effects.EffectOrder);
        Assert.False(document.Resources.ContainsSurface(bake.BakedSurfaceId));
    }

    [Fact]
    public void BakeEffects_RejectsPlanAfterParameterMutation()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var cel = document.Cels.Single();
        document.Resources.GetSurface(cel.SurfaceId).SetPixel(0, 0, new Rgba32(255, 255, 255, 255));
        var bus = new CommandBus(document);
        var add = new AddEffectCommand(cel.Id, BuiltinEffectDescriptors.Outline.TypeId);
        bus.Execute(add);
        var snapshot = DocumentSnapshot.Capture(document);
        var plan = new EffectBakePlanner(EffectEngine.CreateDefault())
            .Prepare(snapshot, cel.FrameId, snapshot.Cels.Single());
        bus.Execute(new SetEffectParameterCommand(
            cel.Id,
            add.EffectId,
            "radius",
            EffectValue.Integer(2),
            BuiltinEffectDescriptors.Outline));

        Assert.Throws<InvalidOperationException>(() => bus.Execute(new BakeEffectsCommand(plan)));
        Assert.Equal(cel.SurfaceId, document.Cels.Single().SurfaceId);
        Assert.Single(cel.Effects.EffectOrder);
    }

    [Fact]
    public void BakePlanner_RejectsUnavailableEffectRatherThanDroppingOpaqueData()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var cel = document.Cels.Single();
        cel.Effects.Add(new EffectInstance(EffectInstanceId.New(), "vendor.future"));
        var snapshot = DocumentSnapshot.Capture(document);

        Assert.Throws<InvalidOperationException>(() =>
            new EffectBakePlanner(EffectEngine.CreateDefault())
                .Prepare(snapshot, cel.FrameId, snapshot.Cels.Single()));
    }
}
