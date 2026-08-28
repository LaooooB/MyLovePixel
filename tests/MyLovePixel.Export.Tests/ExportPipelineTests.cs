using System.Text.Json;
using MyLovePixel.Commands;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
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
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        surface.SetPixel(0, 0, new Rgba32(255, 0, 0, 255));
        var snapshot = DocumentSnapshot.Capture(document);
        surface.SetPixel(0, 0, new Rgba32(0, 255, 0, 255));

        var bundle = ExportPipeline.CreateDefault().Execute(new ExportRequest(snapshot, new ExportPreset
        {
            Layout = ExportLayout.SeparateFrames,
            Trim = false,
            ImageBaseName = "hero",
        }));
        var decoded = PngCodec.Decode(bundle.Artifacts.Single(item => item.MediaType == "image/png").Content.Span);
        Assert.Equal(new Rgba32(255, 0, 0, 255), decoded.GetPixel(0, 0));
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface.GetPixel(0, 0));
    }

    [Fact]
    public void Export_UsesRendererEffectSemantics()
    {
        var document = PixelDocumentFactory.CreateBlank(3, 3);
        var cel = document.Cels.Single();
        document.Resources.GetSurface(cel.SurfaceId).SetPixel(1, 1, new Rgba32(255, 0, 0, 255));
        var outline = new EffectInstance(EffectInstanceId.New(), "core.outline");
        outline.SetParameter("radius", EffectValue.Integer(1), out _);
        outline.SetParameter("color", EffectValue.Color(new Rgba32(0, 0, 0, 255)), out _);
        cel.Effects.Add(outline);
        var snapshot = DocumentSnapshot.Capture(document);

        var bundle = ExportPipeline.CreateDefault().Execute(new ExportRequest(snapshot, new ExportPreset
        {
            Layout = ExportLayout.SeparateFrames,
            Trim = false,
        }));
        var decoded = PngCodec.Decode(bundle.Artifacts.Single(item => item.MediaType == "image/png").Content.Span);
        Assert.Equal(new Rgba32(255, 0, 0, 255), decoded.GetPixel(1, 1));
        Assert.Equal(new Rgba32(0, 0, 0, 255), decoded.GetPixel(0, 1));
    }

    [Fact]
    public void AtlasPacking_IsDeterministicAndMetadataKeepsStableFrameIds()
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
        Assert.Contains(firstFrame.ToString(), ids);
        Assert.Contains(copy.NewFrameId.ToString(), ids);
    }

    [Fact]
    public void CropTrimScaleAndExtrude_ProduceExpectedSheetGeometry()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var surface = document.Resources.GetSurface(document.Cels.Single().SurfaceId);
        surface.SetPixel(2, 2, new Rgba32(10, 20, 30, 255));
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
        var frame = json.RootElement.GetProperty("frames")[0];
        Assert.Equal(2, frame.GetProperty("sourceRect").GetProperty("x").GetInt32());
        Assert.Equal(2, frame.GetProperty("sourceRect").GetProperty("y").GetInt32());
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
}
