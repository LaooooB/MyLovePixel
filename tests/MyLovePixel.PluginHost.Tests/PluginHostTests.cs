using System.Text;
using MyLovePixel.Commands;
using MyLovePixel.Commands.Effects;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Export;
using MyLovePixel.Persistence;
using MyLovePixel.PluginHost;
using MyLovePixel.PluginSdk;
using Xunit;

namespace MyLovePixel.PluginHost.Tests;

public sealed class PluginHostTests
{
    [Fact]
    public void OutOfTreePlugin_ReferencesOnlyPublicSdkAndRegistersExtensions()
    {
        var pluginAssembly = typeof(MyLovePixel.TestPlugin.TestPlugin).Assembly;
        var references = pluginAssembly.GetReferencedAssemblies().Select(value => value.Name).ToArray();
        Assert.Contains("MyLovePixel.PluginSdk", references);
        Assert.DoesNotContain("MyLovePixel.Core", references);
        Assert.DoesNotContain("MyLovePixel.Commands", references);
        Assert.DoesNotContain("MyLovePixel.PluginHost", references);
        Assert.DoesNotContain("Avalonia", references);

        var host = new PluginHost();
        var plugin = new MyLovePixel.TestPlugin.TestPlugin();
        var result = host.Load(plugin);

        Assert.True(result.Succeeded);
        Assert.Contains("com.mylovepixel.test-plugin.dot", host.Tools.Ids);
        Assert.Contains("com.mylovepixel.test-plugin.invert", host.Effects.Ids);
        Assert.Contains("com.mylovepixel.test-plugin.summary", host.Exporters.Ids);
        Assert.Contains("com.mylovepixel.test-plugin.info-panel", host.Panels.Ids);
    }

    [Fact]
    public void PluginTool_ProducesCommandBackedMutationAndUndo()
    {
        var host = CreateLoadedHost();
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document);
        var gateway = new PluginMutationGateway(document, bus);

        var preview = PluginToolExecution.Execute(
            host,
            "com.mylovepixel.test-plugin.dot",
            gateway,
            cel.SurfaceId.Value,
            new PluginPointerEvent(1, PluginPointerKind.Moved, new PluginIntPoint(1, 2), 1, PluginPointerButtons.Primary, 1));
        Assert.True(preview.Succeeded);
        Assert.False(preview.Committed);
        Assert.Single(preview.PreviewWrites);
        Assert.Equal(new Rgba32(0, 0, 0, 0), document.Resources.GetSurface(cel.SurfaceId).GetPixel(1, 2));

        var commit = PluginToolExecution.Execute(
            host,
            "com.mylovepixel.test-plugin.dot",
            gateway,
            cel.SurfaceId.Value,
            new PluginPointerEvent(1, PluginPointerKind.Released, new PluginIntPoint(1, 2), 1, PluginPointerButtons.Primary, 2));

        Assert.True(commit.Succeeded);
        Assert.True(commit.Committed);
        Assert.Equal(new Rgba32(255, 0, 0, 255), document.Resources.GetSurface(cel.SurfaceId).GetPixel(1, 2));
        Assert.Equal(1, bus.UndoCount);
        bus.Undo();
        Assert.Equal(new Rgba32(0, 0, 0, 0), document.Resources.GetSurface(cel.SurfaceId).GetPixel(1, 2));
    }

    [Fact]
    public void PluginEffect_ParticipatesInExistingFrameRenderer()
    {
        var host = CreateLoadedHost();
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var cel = document.Cels.Single();
        var frameId = document.FrameOrder.Single();
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, new Rgba32(10, 20, 30, 255))]));
        bus.Execute(new AddEffectCommand(cel.Id, "com.mylovepixel.test-plugin.invert"));

        var rendered = host.CreateFrameRenderer().Render(DocumentSnapshot.Capture(document), new MyLovePixel.Render.FrameRenderRequest(frameId));

        Assert.Equal(new byte[] { 245, 235, 225, 255 }, rendered.Surface.Bytes.Slice(0, 4).ToArray());
    }

    [Fact]
    public void PluginExporter_UsesSharedSnapshotRenderPipeline()
    {
        var host = CreateLoadedHost();
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var preset = new ExportPreset
        {
            Name = "Plugin Export",
            ExporterId = "com.mylovepixel.test-plugin.summary",
            Layout = ExportLayout.SeparateFrames,
            Trim = false,
        };

        var bundle = host.CreateExportPipeline().Execute(new ExportRequest(DocumentSnapshot.Capture(document), preset));

        var artifact = Assert.Single(bundle.Artifacts);
        Assert.Equal("plugin-summary.txt", artifact.RelativePath);
        Assert.Contains("frames=1", Encoding.UTF8.GetString(artifact.Content.Span));
    }

    [Fact]
    public void NamespacedPluginProjectData_RoundTripsWithoutPluginImplementation()
    {
        var host = CreateLoadedHost();
        var project = new PixelProject(PixelDocumentFactory.CreateBlank(2, 2));
        var session = host.OpenProjectData(project, MyLovePixel.TestPlugin.TestPlugin.Id);
        session.Set("state/data.bin", new byte[] { 4, 5, 6 });
        var path = Path.Combine(Path.GetTempPath(), $"mylovepixel-plugin-{Guid.NewGuid():N}.pixelproj");
        try
        {
            PixelProjectFile.Save(path, project);
            var loaded = PixelProjectFile.Load(path);
            Assert.True(PluginProjectData.TryGet(
                loaded,
                MyLovePixel.TestPlugin.TestPlugin.Id.Value,
                "state/data.bin",
                out var data));
            Assert.Equal(new byte[] { 4, 5, 6 }, data.ToArray());

            host.Unload(MyLovePixel.TestPlugin.TestPlugin.Id);
            var second = Path.Combine(Path.GetTempPath(), $"mylovepixel-plugin-{Guid.NewGuid():N}.pixelproj");
            try
            {
                PixelProjectFile.Save(second, loaded);
                var reloaded = PixelProjectFile.Load(second);
                Assert.True(PluginProjectData.TryGet(
                    reloaded,
                    MyLovePixel.TestPlugin.TestPlugin.Id.Value,
                    "state/data.bin",
                    out var preserved));
                Assert.Equal(new byte[] { 4, 5, 6 }, preserved.ToArray());
            }
            finally
            {
                if (File.Exists(second)) File.Delete(second);
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Unload_RemovesAllRegistrationsAndInvokesLifecycle()
    {
        var host = new PluginHost();
        var plugin = new MyLovePixel.TestPlugin.TestPlugin();
        Assert.True(host.Load(plugin).Succeeded);

        Assert.True(host.Unload(MyLovePixel.TestPlugin.TestPlugin.Id));

        Assert.True(plugin.Unloaded);
        Assert.Empty(host.Tools.Ids);
        Assert.Empty(host.Effects.Ids);
        Assert.Empty(host.Exporters.Ids);
        Assert.Empty(host.Panels.Ids);
        Assert.Empty(host.LoadedPlugins);
    }

    [Fact]
    public void RegistrationFailure_RollsBackEarlierExtensions()
    {
        var host = new PluginHost();
        var result = host.Load(new MissingCapabilityPlugin());

        Assert.False(result.Succeeded);
        Assert.Empty(host.Tools.Ids);
        Assert.Empty(host.Effects.Ids);
        Assert.Contains(host.Diagnostics, value => value.Code == PluginDiagnosticCode.MissingCapability);
    }

    [Fact]
    public void IncompatibleApi_IsRejectedBeforeRegistration()
    {
        var host = new PluginHost();
        var result = host.Load(new IncompatiblePlugin());
        Assert.False(result.Succeeded);
        Assert.Equal(PluginDiagnosticCode.IncompatibleApi, result.Diagnostic?.Code);
        Assert.Empty(host.LoadedPlugins);
    }

    private static PluginHost CreateLoadedHost()
    {
        var host = new PluginHost();
        Assert.True(host.Load(new MyLovePixel.TestPlugin.TestPlugin()).Succeeded);
        return host;
    }

    private sealed class MissingCapabilityPlugin : IPlugin
    {
        public PluginManifest Manifest { get; } = new(
            new PluginId("com.mylovepixel.bad-capability"),
            "Bad Capability",
            "1.0.0",
            new PluginApiVersion(1, 0),
            new PluginApiVersion(1, 0),
            PluginCapability.Tool);

        public void Register(IPluginRegistrationContext context)
        {
            context.RegisterTool(new NoOpTool());
            context.RegisterEffect(new NoOpEffect());
        }
    }

    private sealed class IncompatiblePlugin : IPlugin
    {
        public PluginManifest Manifest { get; } = new(
            new PluginId("com.mylovepixel.future-plugin"),
            "Future",
            "1.0.0",
            new PluginApiVersion(2, 0),
            new PluginApiVersion(2, 9),
            PluginCapability.None);
        public void Register(IPluginRegistrationContext context) => throw new InvalidOperationException("Should never be called.");
    }

    private sealed class NoOpTool : IPluginTool
    {
        public string Id => "com.mylovepixel.bad-capability.tool";
        public string DisplayName => "No-op";
        public PluginToolResult Handle(PluginPointerEvent pointerEvent, PluginRasterTarget target) => PluginToolResult.Ignored;
    }

    private sealed class NoOpEffect : IPluginEffectEvaluator
    {
        public string Id => Descriptor.TypeId;
        public string DisplayName => Descriptor.DisplayName;
        public PluginEffectDescriptor Descriptor { get; } = new("com.mylovepixel.bad-capability.effect", "No-op");
        public PluginImage Evaluate(PluginEffectRequest request) => request.Source;
    }
}
