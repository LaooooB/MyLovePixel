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

    [Fact]
    public void LoadedPlugin_ExposesEveryExecutableExtensionKind()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(2, 2);
        var runtime = workspace.Plugins();
        Assert.True(runtime.LoadAssembly(typeof(MyLovePixel.TestPlugin.TestPlugin).Assembly.Location).Succeeded);

        Assert.Contains(runtime.Commands, value => value.Id == "com.mylovepixel.test-plugin.command-dot");
        Assert.Contains(runtime.GetEffectTypes(), value => value == "com.mylovepixel.test-plugin.invert");
        Assert.Contains(runtime.Exporters, value => value.Id == "com.mylovepixel.test-plugin.summary");
        Assert.Contains(runtime.Importers, value => value.Id == "com.mylovepixel.test-plugin.tiny-import");
        Assert.Contains(runtime.PaletteAlgorithms, value => value.Id == "com.mylovepixel.test-plugin.reverse-palette");
        Assert.Contains(runtime.DitherAlgorithms, value => value.Id == "com.mylovepixel.test-plugin.identity-dither");
        Assert.Contains(runtime.AutoTileRules, value => value.Id == "com.mylovepixel.test-plugin.mask-variant");
        Assert.Contains(runtime.GetTools(session), value => value.Id == "com.mylovepixel.test-plugin.dot");
        Assert.Single(runtime.GetPanels(session));
    }

    [Fact]
    public void PluginCommand_MutatesThroughCommandBusAndCanUndo()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(2, 2);
        var runtime = workspace.Plugins();
        Assert.True(runtime.LoadAssembly(typeof(MyLovePixel.TestPlugin.TestPlugin).Assembly.Location).Succeeded);

        var result = runtime.ExecuteCommand(session, "com.mylovepixel.test-plugin.command-dot");

        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.Mutated);
        Assert.Equal(new byte[] { 0, 255, 255, 255 }, Pixel(session.RenderCanvas().Rgba.Span, new IntSize(2, 2), 0, 0));
        Assert.Equal(1, session.Commands.UndoCount);

        session.Undo();
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, Pixel(session.RenderCanvas().Rgba.Span, new IntSize(2, 2), 0, 0));
    }

    [Fact]
    public void PluginEffect_UsesPluginAwareApplicationRenderer()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(2, 2);
        var runtime = workspace.Plugins();
        Assert.True(runtime.LoadAssembly(typeof(MyLovePixel.TestPlugin.TestPlugin).Assembly.Location).Succeeded);
        Assert.True(runtime.ExecuteCommand(session, "com.mylovepixel.test-plugin.command-dot").Succeeded);

        runtime.AddEffect(session, "com.mylovepixel.test-plugin.invert");
        var rendered = runtime.RenderCanvas(session);

        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(rendered.Rgba.Span, rendered.Size, 0, 0));
    }

    [Fact]
    public void PluginImport_CreatesDirtyUntitledNonRecoverySession()
    {
        var workspace = new EditorWorkspace();
        workspace.NewDocument(2, 2);
        var runtime = workspace.Plugins();
        Assert.True(runtime.LoadAssembly(typeof(MyLovePixel.TestPlugin.TestPlugin).Assembly.Location).Succeeded);
        var path = Path.Combine(Path.GetTempPath(), $"mylovepixel-{Guid.NewGuid():N}.mlpx");
        try
        {
            File.WriteAllBytes(path, [1, 2, 3]);
            var imported = runtime.ImportFile("com.mylovepixel.test-plugin.tiny-import", path);

            Assert.Same(imported, workspace.CurrentSession);
            Assert.Null(imported.FilePath);
            Assert.Null(imported.RecoverySourcePath);
            Assert.False(imported.IsRecovered);
            Assert.True(imported.IsDirty);
            var rendered = imported.RenderCanvas();
            Assert.Equal(new IntSize(1, 1), rendered.Size);
            Assert.Equal(new byte[] { 255, 0, 255, 255 }, rendered.Rgba.ToArray());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void PluginPaletteAndDitherAlgorithms_UseUndoableApplicationPaths()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(2, 2);
        var runtime = workspace.Plugins();
        Assert.True(runtime.LoadAssembly(typeof(MyLovePixel.TestPlugin.TestPlugin).Assembly.Location).Succeeded);
        var paletteId = session.AddDefaultPalette();
        var before = session.GetPaletteEditors().Single(value => value.Id == paletteId).Colors.Select(value => value.Color).ToArray();

        runtime.ApplyPaletteAlgorithm(session, paletteId, "com.mylovepixel.test-plugin.reverse-palette");
        var reversed = session.GetPaletteEditors().Single(value => value.Id == paletteId).Colors.Select(value => value.Color).ToArray();
        Assert.Equal(before.Reverse(), reversed);

        session.Undo();
        Assert.Equal(before, session.GetPaletteEditors().Single(value => value.Id == paletteId).Colors.Select(value => value.Color).ToArray());

        var undoBeforeDither = session.Commands.UndoCount;
        runtime.ApplyDitherAlgorithm(session, paletteId, "com.mylovepixel.test-plugin.identity-dither");
        Assert.Equal(undoBeforeDither + 1, session.Commands.UndoCount);
        Assert.Equal(PixelFormat.Rgba32, session.GetCurrentSurfaceFormat());
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
