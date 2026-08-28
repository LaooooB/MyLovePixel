using System.Text.Json;
using MyLovePixel.Commands;
using MyLovePixel.Commands.Effects;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Effects;
using Xunit;

namespace MyLovePixel.Export.Tests;

public sealed class ExportPipelineTests
{
    [Fact]
    public void PngCodec_RoundTripsRgbaPixels()
    {
        var image = new ExportImage(
            new IntSize(2, 2),
            [
                255, 0, 0, 255, 0, 255, 0, 128,
                0, 0, 255, 0, 255, 255, 255, 255,
            ]);
        var decoded = PngCodec.Decode(PngCodec.Encode(image));
        Assert.Equal(image.Size, decoded.Size);
        Assert.Equal(image.Bytes.ToArray(), decoded.Bytes.ToArray());
    }

    [Fact]
    public void Export_UsesCapturedSnapshotEvenWhenLiveDocumentChangesLater()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 1);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, new Rgba32(255, 0, 0, 255))]));
        var snapshot = DocumentSnapshot.Capture(document);
        bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, new Rgba32(0, 255, 0, 255))]));

        var bundle = ExportPipeline.CreateDefault().Execute(new ExportRequest(snapshot, new ExportPreset
        {
            Layout = ExportLayout.SeparateFrames,
            Trim = false,
            ImageBaseName = "hero",
        }));
        var decoded = PngCodec.Decode(bundle.Artifacts.Single(item => item.MediaType == "image/png").Content.Span);
        Assert.Equal(new Rgba32(255, 0, 0, 255), decoded.GetPixel(0, 0));
        Assert.Equal(new Rgba32(0, 255, 0, 255), document.Resources.GetSurface(cel.SurfaceId).GetPixel(0, 0));
    }

    [Fact]
    public void Export_UsesRendererEffectSemantics()
    {
        var document = PixelDocumentFactory.CreateBlank(3, 3);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(1, 1, new Rgba32(255, 0, 0, 255))]));
        var add = new AddEffectCommand(cel.Id, BuiltinEffectDescriptors.Outline.TypeId);
        bus.Execute(add);
        bus.Execute(new SetEffectParameterCommand(cel.Id, add.EffectId, "radius", EffectValue.Integer(1), BuiltinEffectDescriptors.Outline));
        bus.Execute(new SetEffectParameterCommand(cel.Id, add.EffectId, "color", EffectValue.Color(new Rgba32(0, 0, 0, 255)), BuiltinEffectDescriptors.Outline));

        var bundle = ExportPipeline.CreateDefault().Execute(new ExportRequest(DocumentSnapshot.Capture(document), new ExportPreset
        {
            Layout = ExportLayout.SeparateFrames,
            Trim = false,
        }));
        var decoded = PngCodec.Decode(bundle.Artifacts.Single(item => item.MediaType == "image/png").Content.Span);
        Assert.Equal(new Rgba32(255, 0, 0, 255), decoded.GetPixel(1, 1));
        Assert.Equal(new Rgba32(0, 0, 0, 255), decoded.GetPixel(0, 1));
    }

    [Fact]
    public void IndexedPaletteAndColorCycle_AreResolvedByRendererWithoutMutatingSourceResources()
    {
        var transparent = new Rgba32(0, 0, 0, 0);
        var red = new Rgba32(255, 0, 0, 255);
        var green = new Rgba32(0, 255, 0, 255);
        var document = IndexedDocumentFactory.Create(
            new IntSize(2, 1),
            [transparent, red, green],
            transparentIndex: 0,
            indices: [1, 2]);
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        var paletteId = surface.PaletteId!.Value;
        var palette = document.Resources.GetPalette(paletteId);
        var surfaceRevision = surface.Revision;
        var paletteRevision = palette.Revision;
        var sourceIndices = surface.Snapshot().Bytes.ToArray();
        new CommandBus(document).Execute(new SetColorCyclesKeyframeCommand(
            cel.FrameId,
            new ColorCycleFrameValue([new PaletteCycle(paletteId, 1, 2, 1)])));

        var bundle = ExportPipeline.CreateDefault().Execute(new ExportRequest(DocumentSnapshot.Capture(document), new ExportPreset
        {
            Layout = ExportLayout.SeparateFrames,
            Trim = false,
        }));
        var decoded = PngCodec.Decode(bundle.Artifacts.Single(item => item.MediaType == "image/png").Content.Span);

        Assert.Equal(green, decoded.GetPixel(0, 0));
        Assert.Equal(red, decoded.GetPixel(1, 0));
        Assert.Equal(surfaceRevision, surface.Revision);
        Assert.Equal(paletteRevision, palette.Revision);
        Assert.Equal(sourceIndices, surface.Snapshot().Bytes.ToArray());
    }

    [Fact]
    public void AtlasPacking_IsDeterministicAndMetadataKeepsStableFrameOrder()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var firstFrame = document.FrameOrder.Single();
        var bus = new CommandBus(document);
        var copy = new CopyFrameCommand(firstFrame, FrameCopyMode.Linked);
        bus.Execute(copy);
        var snapshot = DocumentSnapshot.Capture(document);
        var preset = new ExportPreset
        {
            Layout = ExportLayout.Atlas,
            Trim = false,
            Padding = 1,
            Extrude = 1,
            MaxAtlasWidth = 16,
            MaxAtlasHeight = 16,
            ImageBaseName = "atlas",
        };
        var pipeline = ExportPipeline.CreateDefault();
        var first = pipeline.Execute(new ExportRequest(snapshot, preset));
        var second = pipeline.Execute(new ExportRequest(snapshot, preset));

        Assert.Equal(
            first.Artifacts.Select(item => (item.RelativePath, Bytes: Convert.ToHexString(item.Content.Span))).ToArray(),
            second.Artifacts.Select(item => (item.RelativePath, Bytes: Convert.ToHexString(item.Content.Span))).ToArray());
        using var json = JsonDocument.Parse(first.Artifacts.Single(item => item.MediaType == "application/json").Content);
        var ids = json.RootElement.GetProperty("frames").EnumerateArray().Select(frame => frame.GetProperty("id").GetString()).ToArray();
        Assert.Equal(new[] { firstFrame.ToString(), copy.NewFrameId.ToString() }, ids);
    }

    [Fact]
    public void ClipTagAndExplicitSelections_ResolveAgainstSnapshotFrameOrder()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var first = document.FrameOrder.Single();
        var bus = new CommandBus(document);
        var copySecond = new CopyFrameCommand(first, FrameCopyMode.Linked);
        bus.Execute(copySecond);
        var second = copySecond.NewFrameId;
        var copyThird = new CopyFrameCommand(second, FrameCopyMode.Linked);
        bus.Execute(copyThird);
        var third = copyThird.NewFrameId;
        var clipId = AnimationClipId.New();
        var tagId = AnimationTagId.New();
        bus.Execute(new UpsertAnimationClipCommand(new AnimationClip(clipId, "Run", second, third)));
        bus.Execute(new UpsertAnimationTagCommand(new AnimationTag(tagId, "Windup", first, second)));
        var snapshot = DocumentSnapshot.Capture(document);
        var pipeline = ExportPipeline.CreateDefault();

        var clip = pipeline.Execute(new ExportRequest(snapshot, new ExportPreset
        {
            Layout = ExportLayout.SeparateFrames,
            Trim = false,
            Selection = ExportFrameSelection.ForClip(clipId),
        }));
        AssertMetadataFrameIds(clip, second, third);

        var tag = pipeline.Execute(new ExportRequest(snapshot, new ExportPreset
        {
            Layout = ExportLayout.SeparateFrames,
            Trim = false,
            Selection = ExportFrameSelection.ForTag(tagId),
        }));
        AssertMetadataFrameIds(tag, first, second);

        var explicitMiddle = pipeline.Execute(new ExportRequest(snapshot, new ExportPreset
        {
            Layout = ExportLayout.SeparateFrames,
            Trim = false,
            Selection = ExportFrameSelection.Explicit([second]),
        }));
        using var explicitJson = JsonDocument.Parse(explicitMiddle.Artifacts.Single(item => item.MediaType == "application/json").Content);
        Assert.Single(explicitJson.RootElement.GetProperty("clips").EnumerateArray());
        Assert.Single(explicitJson.RootElement.GetProperty("tags").EnumerateArray());
    }

    [Fact]
    public void Metadata_ExportsGameplayTracksSlicesAndDocumentCoordinateSpace()
    {
        var document = PixelDocumentFactory.CreateBlank(8, 8);
        var frame = document.FrameOrder.Single();
        var bus = new CommandBus(document);
        var sliceId = SliceId.New();
        bus.Execute(new SetPivotKeyframeCommand(frame, new IntPoint(3, 4)));
        bus.Execute(new SetHitboxesKeyframeCommand(frame, new BoxFrameValue([new NamedBox("body", new IntRect(1, 2, 3, 4))])));
        bus.Execute(new SetHurtboxesKeyframeCommand(frame, new BoxFrameValue([new NamedBox("hurt", new IntRect(2, 3, 2, 2))])));
        bus.Execute(new SetSocketsKeyframeCommand(frame, new SocketFrameValue([new SocketPose("hand", new IntPoint(6, 5))])));
        bus.Execute(new SetAnimationEventsKeyframeCommand(frame, new EventFrameValue([new AnimationEventMarker("swing", "heavy")])));
        bus.Execute(new UpsertSpriteSliceCommand(new SpriteSlice(
            sliceId,
            "ui",
            new IntRect(1, 1, 6, 6),
            new IntPoint(3, 3),
            new NineSliceInsets(1, 1, 1, 1))));

        var bundle = ExportPipeline.CreateDefault().Execute(new ExportRequest(DocumentSnapshot.Capture(document), new ExportPreset
        {
            Layout = ExportLayout.SeparateFrames,
            Trim = false,
        }));
        using var json = JsonDocument.Parse(bundle.Artifacts.Single(item => item.MediaType == "application/json").Content);
        var root = json.RootElement;
        var metadataFrame = root.GetProperty("frames")[0];

        Assert.Equal("document", root.GetProperty("coordinateSpace").GetString());
        Assert.Equal(3, metadataFrame.GetProperty("pivot").GetProperty("x").GetInt32());
        Assert.Equal("body", metadataFrame.GetProperty("hitboxes")[0].GetProperty("name").GetString());
        Assert.Equal("hurt", metadataFrame.GetProperty("hurtboxes")[0].GetProperty("name").GetString());
        Assert.Equal("hand", metadataFrame.GetProperty("sockets")[0].GetProperty("name").GetString());
        Assert.Equal("swing", metadataFrame.GetProperty("events")[0].GetProperty("name").GetString());
        Assert.Equal("heavy", metadataFrame.GetProperty("events")[0].GetProperty("payload").GetString());
        Assert.Equal(sliceId.ToString(), root.GetProperty("slices")[0].GetProperty("id").GetString());
        Assert.Equal(1, root.GetProperty("slices")[0].GetProperty("nineSlice").GetProperty("left").GetInt32());
    }

    [Fact]
    public void CropTrimScaleAndExtrude_ProduceExpectedSheetGeometry()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var cel = document.Cels.Single();
        new CommandBus(document).Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(2, 2, new Rgba32(10, 20, 30, 255))]));
        var bundle = ExportPipeline.CreateDefault().Execute(new ExportRequest(DocumentSnapshot.Capture(document), new ExportPreset
        {
            Layout = ExportLayout.SpriteSheet,
            Crop = new IntRect(1, 1, 3, 3),
            Trim = true,
            Scale = 2,
            Extrude = 1,
            ImageBaseName = "sheet",
        }));
        var decoded = PngCodec.Decode(bundle.Artifacts.Single(item => item.MediaType == "image/png").Content.Span);
        Assert.Equal(new IntSize(4, 4), decoded.Size);
        Assert.Equal(new Rgba32(10, 20, 30, 255), decoded.GetPixel(1, 1));
        Assert.Equal(new Rgba32(10, 20, 30, 255), decoded.GetPixel(0, 0));
        using var json = JsonDocument.Parse(bundle.Artifacts.Single(item => item.MediaType == "application/json").Content);
        var metadataFrame = json.RootElement.GetProperty("frames")[0];
        Assert.Equal(2, metadataFrame.GetProperty("sourceRect").GetProperty("x").GetInt32());
        Assert.Equal(2, metadataFrame.GetProperty("sourceRect").GetProperty("y").GetInt32());
    }

    [Fact]
    public void PngImporter_CreatesIndependentEditableDocument()
    {
        var png = PngCodec.Encode(new ExportImage(new IntSize(2, 1), [1, 2, 3, 255, 4, 5, 6, 128]));
        var document = ImportPipeline.CreateDefault().Execute(BuiltinImporterIds.Png, new ImportRequest("input.png", png));
        Assert.Equal(new IntSize(2, 1), document.Canvas.Size);
        var surface = document.Resources.GetSurface(document.Cels.Single().SurfaceId);
        Assert.Equal(new Rgba32(1, 2, 3, 255), surface.GetPixel(0, 0));
        Assert.Equal(new Rgba32(4, 5, 6, 128), surface.GetPixel(1, 0));
    }

    [Fact]
    public void PresetJson_RoundTripsTypedSelectionAndOptions()
    {
        var frameId = FrameId.New();
        var preset = new ExportPreset
        {
            Name = "game",
            Layout = ExportLayout.Atlas,
            Selection = ExportFrameSelection.Explicit([frameId]),
            Crop = new IntRect(1, 2, 3, 4),
            Scale = 3,
            Padding = 2,
            Extrude = 1,
            PowerOfTwoAtlas = true,
        };
        var loaded = ExportPresetJson.Deserialize(ExportPresetJson.Serialize(preset));
        Assert.Equal(preset.Name, loaded.Name);
        Assert.Equal(preset.Layout, loaded.Layout);
        Assert.Equal(frameId, loaded.Selection.FrameIds.Single());
        Assert.Equal(preset.Crop, loaded.Crop);
        Assert.Equal(3, loaded.Scale);
        Assert.True(loaded.PowerOfTwoAtlas);
    }

    private static void AssertMetadataFrameIds(ExportBundle bundle, params FrameId[] expected)
    {
        Assert.Equal(expected.Length, bundle.Artifacts.Count(item => item.MediaType == "image/png"));
        using var json = JsonDocument.Parse(bundle.Artifacts.Single(item => item.MediaType == "application/json").Content);
        var ids = json.RootElement.GetProperty("frames").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .ToArray();
        Assert.Equal(expected.Select(id => id.ToString()).ToArray(), ids);
    }
}
