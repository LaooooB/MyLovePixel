using MyLovePixel.Cli;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Export.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public void ImportThenExport_UsesSharedHeadlessPipeline()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyLovePixel.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var inputPath = Path.Combine(root, "input.png");
            var projectPath = Path.Combine(root, "asset.pixelproj");
            var presetPath = Path.Combine(root, "preset.json");
            var outputDirectory = Path.Combine(root, "out");
            var sourceColor = new Rgba32(23, 45, 67, 255);
            File.WriteAllBytes(inputPath, PngCodec.Encode(new ExportImage(
                new IntSize(1, 1),
                [sourceColor.R, sourceColor.G, sourceColor.B, sourceColor.A])));

            using var importOutput = new StringWriter();
            using var importError = new StringWriter();
            var importCode = CliApplication.Run(
                ["import-png", inputPath, projectPath],
                importOutput,
                importError);

            Assert.Equal(0, importCode);
            Assert.True(File.Exists(projectPath));
            Assert.Equal(string.Empty, importError.ToString());

            File.WriteAllBytes(presetPath, ExportPresetJson.Serialize(new ExportPreset
            {
                Layout = ExportLayout.SeparateFrames,
                Trim = false,
                ImageBaseName = "cli",
                MetadataFileName = "cli.json",
            }));

            using var exportOutput = new StringWriter();
            using var exportError = new StringWriter();
            var exportCode = CliApplication.Run(
                ["export", projectPath, presetPath, outputDirectory],
                exportOutput,
                exportError);

            Assert.Equal(0, exportCode);
            Assert.Equal(string.Empty, exportError.ToString());
            Assert.True(File.Exists(Path.Combine(outputDirectory, "cli.json")));
            var pngPath = Directory.GetFiles(outputDirectory, "*.png").Single();
            var decoded = PngCodec.Decode(File.ReadAllBytes(pngPath));
            Assert.Equal(sourceColor, decoded.GetPixel(0, 0));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PresetTemplate_ProducesAReadableDefaultPreset()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyLovePixel.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "preset.json");
            using var output = new StringWriter();
            using var error = new StringWriter();

            var code = CliApplication.Run(["preset-template", path], output, error);

            Assert.Equal(0, code);
            Assert.Equal(string.Empty, error.ToString());
            var preset = ExportPresetJson.Deserialize(File.ReadAllBytes(path));
            Assert.Equal(ExportLayout.SpriteSheet, preset.Layout);
            Assert.Equal(BuiltinExporterIds.GameAssets, preset.ExporterId);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
