using MyLovePixel.Commands.Pixel;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Export;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class ApplicationLayerTests
{
    [Fact]
    public async Task ActionRegistry_RoutesOnlyByActionId()
    {
        var workspace = new EditorWorkspace();
        var interaction = new FakeInteraction();
        var context = new EditorActionContext(workspace, interaction);
        var registry = new ActionRegistry();
        var calls = 0;
        var id = new ActionId("test.run");
        registry.Register(new ActionDescriptor(id, "Run", (_, _) =>
        {
            calls++;
            return Task.CompletedTask;
        }));

        await registry.ExecuteAsync(id, context);
        Assert.Equal(1, calls);
        Assert.Throws<InvalidOperationException>(() => registry.Register(new ActionDescriptor(id, "Duplicate", (_, _) => Task.CompletedTask)));
    }

    [Fact]
    public void ShortcutMap_ResolvesStableActionIds()
    {
        var map = ShortcutMap.CreateDefault();
        Assert.True(map.TryResolve(new ShortcutGesture("z", ShortcutModifiers.Control), out var actionId));
        Assert.Equal(BuiltinActionIds.Undo, actionId);
    }

    [Fact]
    public void Session_SnapshotAndCanvasPresentationAreIsolatedFromLaterMutation()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(2, 1);
        var cel = session.Document.Cels.Single();
        session.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, new Rgba32(255, 0, 0, 255))]));
        var snapshot = session.CaptureSnapshot();
        var canvas = session.RenderCanvas();

        session.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, new Rgba32(0, 255, 0, 255))]));

        Assert.Equal(new Rgba32(255, 0, 0, 255), snapshot.GetSurface(cel.SurfaceId).GetPixel(0, 0));
        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 0, 0, 0 }, canvas.Rgba.ToArray());
        Assert.Equal(new Rgba32(0, 255, 0, 255), session.Document.Resources.GetSurface(cel.SurfaceId).GetPixel(0, 0));
    }

    [Fact]
    public async Task BuiltinUndoRedoActions_TrackCommandBusState()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(1, 1);
        var cel = session.Document.Cels.Single();
        session.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, new Rgba32(10, 20, 30, 255))]));
        var registry = ActionRegistry.CreateDefault();
        var context = new EditorActionContext(workspace, new FakeInteraction());

        Assert.True(registry.CanExecute(BuiltinActionIds.Undo, context));
        await registry.ExecuteAsync(BuiltinActionIds.Undo, context);
        Assert.Equal(Rgba32.Transparent, session.Document.Resources.GetSurface(cel.SurfaceId).GetPixel(0, 0));
        Assert.True(registry.CanExecute(BuiltinActionIds.Redo, context));
        await registry.ExecuteAsync(BuiltinActionIds.Redo, context);
        Assert.Equal(new Rgba32(10, 20, 30, 255), session.Document.Resources.GetSurface(cel.SurfaceId).GetPixel(0, 0));
    }

    [Fact]
    public void TimelineWindow_ReturnsOnlyRequestedVirtualizedRange()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(1, 1);
        var first = session.Document.FrameOrder.Single();
        for (var index = 0; index < 120; index++)
            session.Execute(new CopyFrameCommand(first, FrameCopyMode.Linked));

        var window = session.GetTimelineWindow(50, 12);
        Assert.Equal(121, window.TotalCount);
        Assert.Equal(12, window.Items.Count);
        Assert.Equal(50, window.Items[0].Index);
        Assert.Equal(61, window.Items[^1].Index);
    }

    [Fact]
    public async Task SaveAndExportActions_DelegateToWorkspacePipelines()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyLovePixel-AppTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var projectPath = Path.Combine(root, "sample.pixelproj");
            var exportPath = Path.Combine(root, "out");
            var workspace = new EditorWorkspace();
            var session = workspace.NewDocument(2, 2);
            var cel = session.Document.Cels.Single();
            session.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(1, 1, new Rgba32(1, 2, 3, 255))]));
            Assert.True(session.IsDirty);

            var interaction = new FakeInteraction
            {
                SavePath = projectPath,
                ExportTarget = new ExportTarget(new ExportPreset { Trim = false, Layout = ExportLayout.SpriteSheet }, exportPath),
            };
            var registry = ActionRegistry.CreateDefault();
            var context = new EditorActionContext(workspace, interaction);

            await registry.ExecuteAsync(BuiltinActionIds.SaveProject, context);
            Assert.True(File.Exists(projectPath));
            Assert.False(session.IsDirty);
            Assert.Equal(Path.GetFullPath(projectPath), session.FilePath);

            await registry.ExecuteAsync(BuiltinActionIds.ExportProject, context);
            Assert.True(File.Exists(Path.Combine(exportPath, "sprite.png")));
            Assert.True(File.Exists(Path.Combine(exportPath, "sprite.json")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeInteraction : IEditorInteraction
    {
        public string? OpenPath { get; init; }
        public string? SavePath { get; init; }
        public ExportTarget? ExportTarget { get; init; }

        public Task<string?> PickOpenProjectAsync(CancellationToken cancellationToken) => Task.FromResult(OpenPath);
        public Task<string?> PickSaveProjectAsync(DocumentSession session, CancellationToken cancellationToken) => Task.FromResult(SavePath);
        public Task<ExportTarget?> PickExportTargetAsync(DocumentSession session, CancellationToken cancellationToken) => Task.FromResult(ExportTarget);
    }
}
