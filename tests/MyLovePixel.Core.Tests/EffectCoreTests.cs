using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Validation;
using Xunit;

namespace MyLovePixel.Core.Tests;

public sealed class EffectCoreTests
{
    [Fact]
    public void EffectGraph_SnapshotFreezesParametersAndAnimatedTracks()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var cel = document.Cels.Single();
        var frameId = document.FrameOrder[0];
        var descriptor = new EffectDescriptor(
            "core.test",
            "Test",
            [new EffectParameterDescriptor(
                "amount",
                "Amount",
                EffectParameterKind.Integer,
                EffectValue.Integer(1),
                minimum: 0,
                maximum: 10)]);
        var effect = new EffectInstance(EffectInstanceId.New(), descriptor.TypeId);
        effect.SetParameter("amount", EffectValue.Integer(2), out _);
        effect.SetKeyframe(
            "amount",
            frameId,
            EffectValue.Integer(3),
            AnimationTrackId.New(),
            out _,
            out _);
        cel.Effects.Add(effect);

        var before = DocumentSnapshot.Capture(document);
        descriptor.Validate(before.Cels.Single().Effects.GetEffect(effect.Id));

        effect.SetParameter("amount", EffectValue.Integer(7), out _);
        effect.SetKeyframe(
            "amount",
            frameId,
            EffectValue.Integer(8),
            AnimationTrackId.New(),
            out _,
            out _);

        var frozen = before.Cels.Single().Effects.GetEffect(effect.Id);
        Assert.Equal(EffectValue.Integer(2), frozen.Parameters["amount"]);
        Assert.Equal(EffectValue.Integer(3), frozen.ParameterTracks["amount"].Values[frameId]);
        Assert.Equal(EffectValue.Integer(3), Resolve(frozen, descriptor, frameId, "amount"));

        var after = DocumentSnapshot.Capture(document).Cels.Single().Effects.GetEffect(effect.Id);
        Assert.Equal(EffectValue.Integer(7), after.Parameters["amount"]);
        Assert.Equal(EffectValue.Integer(8), Resolve(after, descriptor, frameId, "amount"));
    }

    [Fact]
    public void Descriptor_RejectsWrongKindRangeAndNonAnimatableTrack()
    {
        var descriptor = new EffectDescriptor(
            "core.test",
            "Test",
            [new EffectParameterDescriptor(
                "radius",
                "Radius",
                EffectParameterKind.Integer,
                EffectValue.Integer(1),
                animatable: false,
                minimum: 0,
                maximum: 4)]);
        var effect = new EffectInstance(EffectInstanceId.New(), descriptor.TypeId);

        Assert.Throws<ArgumentException>(() =>
            descriptor.GetParameter("radius").Validate(EffectValue.Color(new Rgba32(1, 2, 3, 4))));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            descriptor.GetParameter("radius").Validate(EffectValue.Integer(5)));

        var frameId = FrameId.New();
        effect.SetKeyframe(
            "radius",
            frameId,
            EffectValue.Integer(2),
            AnimationTrackId.New(),
            out _,
            out _);
        Assert.Throws<ArgumentException>(() => descriptor.Validate(effect.Snapshot()));
    }

    [Fact]
    public void Validator_ReportsMissingPaletteAndMissingEffectTrackFrame()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var cel = document.Cels.Single();
        var effect = new EffectInstance(EffectInstanceId.New(), "plugin.unknown");
        effect.SetParameter(
            "palette",
            EffectValue.PaletteReference(PaletteId.New()),
            out _);
        effect.SetKeyframe(
            "amount",
            FrameId.New(),
            EffectValue.Number(0.5),
            AnimationTrackId.New(),
            out _,
            out _);
        cel.Effects.Add(effect);

        var issues = DocumentValidator.Validate(document);

        Assert.Contains(issues, issue => issue.Code == "effect.parameter.palette.missing");
        Assert.Contains(issues, issue => issue.Code == "effect.track.frame.missing");
    }

    [Fact]
    public void UnknownEffectType_IsValidDocumentDataWithoutDescriptor()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var cel = document.Cels.Single();
        var effect = new EffectInstance(EffectInstanceId.New(), "vendor.future-effect");
        effect.SetParameter("opaqueAmount", EffectValue.Number(0.25), out _);
        cel.Effects.Add(effect);

        Assert.Empty(DocumentValidator.Validate(document));
        var snapshot = DocumentSnapshot.Capture(document);
        Assert.Equal("vendor.future-effect", snapshot.Cels.Single().Effects.GetEffect(effect.Id).TypeId);
    }

    private static EffectValue Resolve(
        EffectInstanceSnapshot instance,
        EffectDescriptor descriptor,
        FrameId frameId,
        string key)
    {
        Assert.True(instance.TryResolveParameter(key, frameId, descriptor, out var value));
        return value;
    }
}
