using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class PluginApplicationTests
{
    [Fact]
    public void LoadedPluginTool_UsesUnifiedPresentationPreviewAndUndoPath()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(4, 4);
        var runtime = workspace.Plugins();
        var load = runtime.LoadAssembly(typeof(MyLovePixel.TestPlugin.TestPlugin).Assembly.Location);
        Assert.True(load.Succeeded, load.Error);

        var tool = Assert.Single(runtime.GetTools(session), value => value.Id == "com.mylovepixel.test-plugin.dot");
        Assert.False(tool.IsActive);
        runtime.SelectTool(session, tool.Id);
        Assert.True(Assert.Single(runtime.GetTools(session), value => value.Id == tool.Id).IsActive);

        var move = runtime.DispatchPointer(session, Pointer(EditorPointerKind.Moved));
        Assert.True(move.Consumed);
        Assert.False(move.Committed);
        var preview = runtime.DecorateCanvas(session, session.RenderCanvas());
        var previewPixel = Assert.Single(preview.PreviewPixels, value => value.Point == new IntPoint(1, 2));
        Assert.Equal(new Rgba32(255, 0, 0, 255), previewPixel.Color);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, Pixel(preview.Rgba.Span, preview.Size, 1, 2));

        var release = runtime.DispatchPointer(session, Pointer(EditorPointerKind.Released));
        Assert.True(release.Committed);
        Assert.Equal(1, session.Commands.UndoCount);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(session.RenderCanvas().Rgba.Span, new IntSize(4, 4), 1, 2));

        session.Undo();
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, Pixel(session.RenderCanvas().Rgba.Span, new IntSize(4, 4), 1, 2));
    }

    [Fact]
    public void PluginPanel_IsApplicationPresentationWithoutAvaloniaTypes()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(2, 2);
        var runtime = workspace.Plugins();
        Assert.True(runtime.LoadAssembly(typeof(MyLovePixel.TestPlugin.TestPlugin).Assembly.Location).Succeeded);

        var panel = Assert.Single(runtime.GetPanels(session));
        Assert.Equal("com.mylovepixel.test-plugin.info-panel", panel.Id);
        Assert.Equal("SDK Info", panel.Title);
        Assert.NotEmpty(panel.Sections);
        Assert.StartsWith("MyLovePixel.Application", panel.GetType().Namespace, StringComparison.Ordinal);
        Assert.DoesNotContain("Avalonia", panel.GetType().Assembly.GetReferencedAssemblies().Select(value => value.Name));
    }

    private static EditorPointerEvent Pointer(EditorPointerKind kind) => new(
        1,
        EditorPointerDevice.Mouse,
        kind,
        new IntPoint(1, 2),
        1,
        EditorPointerButtons.Primary,
        EditorInputModifiers.None,
        1);

    private static byte[] Pixel(ReadOnlySpan<byte> rgba, IntSize size, int x, int y)
    {
        var offset = checked(((y * size.Width) + x) * 4);
        return rgba.Slice(offset, 4).ToArray();
    }
}
