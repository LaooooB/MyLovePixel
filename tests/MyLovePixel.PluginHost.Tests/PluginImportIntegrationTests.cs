using MyLovePixel.Core.Pixel;
using MyLovePixel.Export;
using MyLovePixel.PluginHost;
using MyLovePixel.PluginSdk;
using Xunit;

namespace MyLovePixel.PluginHost.Tests;

public sealed class PluginImportIntegrationTests
{
    [Fact]
    public void PluginImporter_ConvertsImmutableImageToStandardRgbaDocument()
    {
        var host = new PluginHost();
        Assert.True(host.Load(new ImporterPlugin(new GoodImporter())).Succeeded);
        var pipeline = host.CreateImportPipeline();

        var document = pipeline.Execute(
            GoodImporter.ExtensionId,
            new ImportRequest("sample.mlpraw", new byte[] { (byte)'M', (byte)'L', (byte)'P', (byte)'1' }));

        Assert.Equal(2, document.Canvas.Size.Width);
        Assert.Equal(1, document.Canvas.Size.Height);
        var surface = document.Resources.GetSurface(document.Cels.Single().SurfaceId);
        Assert.Equal(PixelFormat.Rgba32, surface.Format);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface.GetPixel(0, 0));
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface.GetPixel(1, 0));
    }

    [Fact]
    public void PluginImporterFailure_BecomesStructuredPipelineFailureAndDiagnostic()
    {
        var host = new PluginHost();
        Assert.True(host.Load(new ImporterPlugin(new ThrowingImporter())).Succeeded);
        var pipeline = host.CreateImportPipeline();

        var error = Assert.Throws<AssetPipelineException>(() => pipeline.Execute(
            ThrowingImporter.ExtensionId,
            new ImportRequest("sample.fail", new byte[] { 1, 2, 3, 4 })));

        Assert.Equal(AssetPipelineErrorCode.ImportFailed, error.Code);
        Assert.Contains(host.Diagnostics, value =>
            value.Code == PluginDiagnosticCode.ExecutionFailed &&
            value.ExtensionId == ThrowingImporter.ExtensionId);
    }

    [Fact]
    public void PluginImporterMetadata_IsRejectedRatherThanSilentlyDiscarded()
    {
        var host = new PluginHost();
        Assert.True(host.Load(new ImporterPlugin(new MetadataImporter())).Succeeded);

        var error = Assert.Throws<AssetPipelineException>(() => host.CreateImportPipeline().Execute(
            MetadataImporter.ExtensionId,
            new ImportRequest("sample.meta", new byte[] { 1 })));

        Assert.Equal(AssetPipelineErrorCode.ImportFailed, error.Code);
    }

    private sealed class ImporterPlugin(IPluginImporter importer) : IPlugin
    {
        public PluginManifest Manifest { get; } = new(
            new PluginId("com.mylovepixel.import-test"),
            "Import Test",
            "1.0.0",
            new PluginApiVersion(1, 0),
            new PluginApiVersion(1, 0),
            PluginCapability.Importer);

        public void Register(IPluginRegistrationContext context) => context.RegisterImporter(importer);
    }

    private sealed class GoodImporter : IPluginImporter
    {
        public const string ExtensionId = "com.mylovepixel.import-test.raw";
        public string Id => ExtensionId;
        public string DisplayName => "Raw Test";

        public bool CanImport(string name, ReadOnlySpan<byte> header) =>
            name.EndsWith(".mlpraw", StringComparison.OrdinalIgnoreCase) &&
            header.Length >= 4 &&
            header[..4].SequenceEqual(new byte[] { (byte)'M', (byte)'L', (byte)'P', (byte)'1' });

        public PluginImportResult Import(PluginImportRequest request) => new(
            new PluginImage(
                new PluginIntSize(2, 1),
                new byte[]
                {
                    255, 0, 0, 255,
                    0, 255, 0, 255,
                }));
    }

    private sealed class ThrowingImporter : IPluginImporter
    {
        public const string ExtensionId = "com.mylovepixel.import-test.throw";
        public string Id => ExtensionId;
        public string DisplayName => "Throwing Importer";
        public bool CanImport(string name, ReadOnlySpan<byte> header) => true;
        public PluginImportResult Import(PluginImportRequest request) => throw new InvalidOperationException("boom");
    }

    private sealed class MetadataImporter : IPluginImporter
    {
        public const string ExtensionId = "com.mylovepixel.import-test.metadata";
        public string Id => ExtensionId;
        public string DisplayName => "Metadata Importer";
        public bool CanImport(string name, ReadOnlySpan<byte> header) => true;
        public PluginImportResult Import(PluginImportRequest request) => new(
            new PluginImage(new PluginIntSize(1, 1), new byte[] { 0, 0, 0, 0 }),
            new Dictionary<string, string> { ["future"] = "value" });
    }
}
