using System.Text.Json;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Export.Tests;

public sealed class GameReadyExporterTests
{
    [Fact]
    public void GameReadyExporter_ZeroesHiddenRgb_AddsSrgb_AndPreservesPartialAlpha()
    {
        var sourceImage = new ExportImage(
            new IntSize(2, 1),
            [
                91, 72, 53, 0,
                100, 50, 25, 128,
            ]);
        var exporter = new GameReadyExporter(new StubExporter(PngCodec.Encode(sourceImage)));
        var document = PixelDocumentFactory.CreateBlank(2, 1);

        var bundle = exporter.Export(new ExportRequest(
            DocumentSnapshot.Capture(document),
            new ExportPreset { Layout = ExportLayout.SeparateFrames, Trim = false, ImageBaseName = "hero" }));

        var png = bundle.Artifacts.Single(item => item.MediaType == "image/png");
        Assert.True(ContainsChunk(png.Content.Span, "sRGB"u8));
        var decoded = PngCodec.Decode(png.Content.Span);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, decoded.Bytes.Span[..4].ToArray());
        Assert.Equal(new byte[] { 100, 50, 25, 128 }, decoded.Bytes.Span[4..8].ToArray());
    }

    [Fact]
    public void GameReadyExporter_EmitsCrossEngineImportManifest()
    {
        var image = new ExportImage(new IntSize(3, 5), new byte[3 * 5 * 4]);
        var exporter = new GameReadyExporter(new StubExporter(PngCodec.Encode(image)));
        var document = PixelDocumentFactory.CreateBlank(3, 5);

        var bundle = exporter.Export(new ExportRequest(
            DocumentSnapshot.Capture(document),
            new ExportPreset
            {
                Layout = ExportLayout.Atlas,
                Trim = true,
                Padding = 2,
                Extrude = 0,
                PowerOfTwoAtlas = false,
                ImageBaseName = "enemy",
                MetadataFileName = "enemy.json",
            }));

        var manifest = bundle.Artifacts.Single(item => item.MediaType == "application/vnd.mylovepixel.game-import+json");
        using var json = JsonDocument.Parse(manifest.Content);
        var root = json.RootElement;
        Assert.Equal("RGBA8", root.GetProperty("source").GetProperty("pixelFormat").GetString());
        Assert.Equal("straight-unassociated", root.GetProperty("source").GetProperty("alphaMode").GetString());
        Assert.Equal("RGBA(0,0,0,0)", root.GetProperty("source").GetProperty("fullyTransparentTexel").GetString());
        Assert.Equal("Point", root.GetProperty("engines").GetProperty("unity").GetProperty("filterMode").GetString());
        Assert.Equal("Lossless", root.GetProperty("engines").GetProperty("godot").GetProperty("compression").GetString());
        Assert.Equal("Point", root.GetProperty("engines").GetProperty("unreal").GetProperty("filter").GetString());
        Assert.Contains(root.GetProperty("warnings").EnumerateArray(), warning =>
            warning.GetString()!.Contains("power-of-two", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefaultPipeline_AlwaysIncludesGameImportManifest()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var bundle = ExportPipeline.CreateDefault().Execute(new ExportRequest(
            DocumentSnapshot.Capture(document),
            new ExportPreset { Layout = ExportLayout.SeparateFrames, Trim = false }));

        Assert.Contains(bundle.Artifacts, item => item.MediaType == "image/png");
        Assert.Contains(bundle.Artifacts, item => item.MediaType == "application/json");
        Assert.Contains(bundle.Artifacts, item => item.MediaType == "application/vnd.mylovepixel.game-import+json");
    }

    private static bool ContainsChunk(ReadOnlySpan<byte> png, ReadOnlySpan<byte> type)
    {
        if (png.Length < 8) return false;
        var offset = 8;
        while (offset + 12 <= png.Length)
        {
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(png.Slice(offset, 4));
            offset += 4;
            if (png.Slice(offset, 4).SequenceEqual(type)) return true;
            offset += 4;
            if (length > int.MaxValue || offset + (int)length + 4 > png.Length) return false;
            offset += (int)length + 4;
        }
        return false;
    }

    private sealed class StubExporter : IExporter
    {
        private readonly byte[] _png;

        public StubExporter(byte[] png) => _png = png;
        public string Id => BuiltinExporterIds.GameAssets;

        public ExportBundle Export(ExportRequest request) =>
            new([new ExportArtifact("stub.png", "image/png", _png)]);
    }
}
