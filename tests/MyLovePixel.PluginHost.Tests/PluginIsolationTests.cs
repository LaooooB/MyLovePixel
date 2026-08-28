using System.Text;
using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Export;
using MyLovePixel.PluginSdk;
using Xunit;

namespace MyLovePixel.PluginHost.Tests;

public sealed class PluginIsolationTests
{
    [Fact]
    public void CollectibleAssemblyLoader_LoadsSdkOnlyPluginAndUnloadCleansRegistries()
    {
        var host = new PluginHost();
        using var loader = new PluginAssemblyLoader(host);
        var path = typeof(MyLovePixel.TestPlugin.TestPlugin).Assembly.Location;

        var loaded = loader.Load(path);

        Assert.True(loaded.Succeeded, loaded.Diagnostic?.Message);
        Assert.Equal(MyLovePixel.TestPlugin.TestPlugin.Id, loaded.PluginId);
        Assert.Contains("com.mylovepixel.test-plugin.dot", host.Tools.Ids);
        Assert.True(loader.Unload(MyLovePixel.TestPlugin.TestPlugin.Id));
        Assert.Empty(host.Tools.Ids);
        Assert.Empty(host.Effects.Ids);
        Assert.Empty(host.Exporters.Ids);
        Assert.Empty(host.Panels.Ids);
    }

    [Fact]
    public void ThrowingTool_DoesNotMutateDocumentAndProducesDiagnostic()
    {
        var host = new PluginHost();
        Assert.True(host.Load(new ThrowingPlugin()).Succeeded);
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document);
        var beforeRevision = document.Resources.GetSurface(cel.SurfaceId).Revision;

        var result = PluginToolExecution.Execute(
            host,
            "com.mylovepixel.throwing.tool",
            new PluginMutationGateway(document, bus),
            cel.SurfaceId.Value,
            new PluginPointerEvent(1, PluginPointerKind.Released, new PluginIntPoint(0, 0), 1, PluginPointerButtons.Primary, 1));

        Assert.False(result.Succeeded);
        Assert.False(result.Committed);
        Assert.Equal(beforeRevision, document.Resources.GetSurface(cel.SurfaceId).Revision);
        Assert.Equal(0, bus.UndoCount);
        Assert.Contains(host.Diagnostics, value => value.Code == PluginDiagnosticCode.ExecutionFailed);
    }

    [Fact]
    public void ThrowingExporter_IsWrappedWithoutChangingSnapshot()
    {
        var host = new PluginHost();
        Assert.True(host.Load(new ThrowingPlugin()).Succeeded);
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var snapshot = DocumentSnapshot.Capture(document);
        var preset = new ExportPreset
        {
            Name = "Throw",
            ExporterId = "com.mylovepixel.throwing.exporter",
            Trim = false,
        };

        var error = Assert.Throws<AssetPipelineException>(() =>
            host.CreateExportPipeline().Execute(new ExportRequest(snapshot, preset)));

        Assert.Equal(AssetPipelineErrorCode.ExportFailed, error.Code);
        Assert.Equal(snapshot.GetSurface(document.Cels.Single().SurfaceId).Revision, document.Resources.GetSurface(document.Cels.Single().SurfaceId).Revision);
        Assert.Contains(host.Diagnostics, value => value.Code == PluginDiagnosticCode.ExecutionFailed);
    }

    private sealed class ThrowingPlugin : IPlugin
    {
        public PluginManifest Manifest { get; } = new(
            new PluginId("com.mylovepixel.throwing"),
            "Throwing Test",
            "1.0.0",
            new PluginApiVersion(1, 0),
            new PluginApiVersion(1, 0),
            PluginCapability.Tool | PluginCapability.Exporter);

        public void Register(IPluginRegistrationContext context)
        {
            context.RegisterTool(new ThrowTool());
            context.RegisterExporter(new ThrowExporter());
        }
    }

    private sealed class ThrowTool : IPluginTool
    {
        public string Id => "com.mylovepixel.throwing.tool";
        public string DisplayName => "Throw Tool";
        public PluginToolResult Handle(PluginPointerEvent pointerEvent, PluginRasterTarget target) =>
            throw new InvalidOperationException("tool exploded");
    }

    private sealed class ThrowExporter : IPluginExporter
    {
        public string Id => "com.mylovepixel.throwing.exporter";
        public string DisplayName => "Throw Exporter";
        public PluginExportBundle Export(PluginExportRequest request) =>
            throw new InvalidOperationException("export exploded");
    }
}
