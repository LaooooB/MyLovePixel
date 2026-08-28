using System.Text;
using MyLovePixel.PluginSdk;

namespace MyLovePixel.TestPlugin;

public sealed class TestPlugin : IPlugin, IPluginLifecycle
{
    public static PluginId Id { get; } = new("com.mylovepixel.test-plugin");

    public PluginManifest Manifest { get; } = new(
        Id,
        "MyLovePixel SDK Test Plugin",
        "1.0.0",
        new PluginApiVersion(1, 0),
        new PluginApiVersion(1, 0),
        PluginCapability.Tool |
        PluginCapability.Effect |
        PluginCapability.Exporter |
        PluginCapability.Panel |
        PluginCapability.ProjectData);

    public bool Unloaded { get; private set; }

    public void Register(IPluginRegistrationContext context)
    {
        context.RegisterTool(new DotTool());
        context.RegisterEffect(new InvertEffect());
        context.RegisterExporter(new TextExporter());
        context.RegisterPanel(new InfoPanel());
    }

    public void OnUnload() => Unloaded = true;

    private sealed class DotTool : IPluginTool
    {
        public string Id => "com.mylovepixel.test-plugin.dot";
        public string DisplayName => "SDK Dot";

        public PluginToolResult Handle(PluginPointerEvent pointerEvent, PluginRasterTarget target)
        {
            var write = new PluginPixelWrite(pointerEvent.Position.X, pointerEvent.Position.Y, new PluginRgba32(255, 0, 0, 255));
            return pointerEvent.Kind switch
            {
                PluginPointerKind.Moved => new PluginToolResult(true, [write], null),
                PluginPointerKind.Released => new PluginToolResult(
                    true,
                    Array.Empty<PluginPixelWrite>(),
                    new PluginPixelPatch(target.SurfaceId, target.Revision, [write], "SDK Dot")),
                _ => new PluginToolResult(true, Array.Empty<PluginPixelWrite>(), null),
            };
        }
    }

    private sealed class InvertEffect : IPluginEffectEvaluator
    {
        public string Id => Descriptor.TypeId;
        public string DisplayName => Descriptor.DisplayName;
        public PluginEffectDescriptor Descriptor { get; } = new(
            "com.mylovepixel.test-plugin.invert",
            "SDK Invert");

        public PluginImage Evaluate(PluginEffectRequest request)
        {
            var source = request.Source.Rgba.Span;
            var output = source.ToArray();
            for (var offset = 0; offset < output.Length; offset += 4)
            {
                output[offset] = (byte)(255 - output[offset]);
                output[offset + 1] = (byte)(255 - output[offset + 1]);
                output[offset + 2] = (byte)(255 - output[offset + 2]);
            }
            return new PluginImage(request.Source.Size, output, request.Source.Origin);
        }
    }

    private sealed class TextExporter : IPluginExporter
    {
        public string Id => "com.mylovepixel.test-plugin.summary";
        public string DisplayName => "SDK Summary";

        public PluginExportBundle Export(PluginExportRequest request)
        {
            var text = $"document={request.DocumentId:N}\nframes={request.Frames.Count}\n";
            return new PluginExportBundle([
                new PluginExportArtifact("plugin-summary.txt", "text/plain", Encoding.UTF8.GetBytes(text)),
            ]);
        }
    }

    private sealed class InfoPanel : IPluginPanelProvider
    {
        public string Id => "com.mylovepixel.test-plugin.info-panel";
        public string DisplayName => "SDK Info";

        public PluginPanelModel Build(PluginPanelContext context) => new(
            "SDK Info",
            [
                new PluginPanelSection(
                    "Context",
                    [new PluginPanelField("document", "Document", context.DocumentId?.ToString("N") ?? "none")],
                    Array.Empty<PluginPanelAction>()),
            ]);

        public PluginPixelPatch? Invoke(string actionId, PluginPanelContext context, PluginRasterTarget? target) => null;
    }
}
