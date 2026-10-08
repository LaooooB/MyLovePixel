using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class NavigationPerformanceTests
{
    [Fact]
    public void UnchangedDocumentReusesItsImmutableSnapshotAcrossViewAndColorChanges()
    {
        var session = new EditorWorkspace().NewDocument(1024, 1024);
        var before = session.CaptureSnapshot();
        for (var i = 0; i < 12; i++)
        {
            session.SetZoom(0.5 + i * 0.01);
            session.SetToolColors(new Rgba32((byte)i, 20, 30), Rgba32.Transparent);
            Assert.Same(before, session.CaptureSnapshot());
        }
    }

    [Fact]
    public void SnapshotReuseInvalidatesOnEditUndoAndRedoWithoutMutatingOldSnapshots()
    {
        var session = new EditorWorkspace().NewDocument(16, 16);
        var before = session.CaptureSnapshot();
        var id = before.Cels.Single().SurfaceId;
        var color = new Rgba32(10, 20, 30);
        session.Execute(new PixelPatchCommand(id, [new PixelWrite(1, 2, color)]));
        var after = session.CaptureSnapshot();
        Assert.NotSame(before, after);
        Assert.Equal(Rgba32.Transparent, before.GetSurface(id).GetPixel(1, 2));
        Assert.Equal(color, after.GetSurface(id).GetPixel(1, 2));
        session.Undo();
        Assert.Equal(Rgba32.Transparent, session.CaptureSnapshot().GetSurface(id).GetPixel(1, 2));
        session.Redo();
        Assert.Equal(color, session.CaptureSnapshot().GetSurface(id).GetPixel(1, 2));
    }

    [Fact]
    public void RenderCacheHitReusesPixelStorageRatherThanCopyingTheEntireCanvas()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(1024, 1024);
        using var plugins = workspace.Plugins();
        var before = plugins.RenderCanvas(session);
        var after = plugins.RenderCanvas(session);
        Assert.True(before.Rgba.Equals(after.Rgba), "A cache hit must reuse immutable pixel storage.");
    }

    [Theory]
    [InlineData("core.pencil")]
    [InlineData("core.eraser")]
    [InlineData("core.arc")]
    [InlineData("core.shadow")]
    [InlineData("core.highlight")]
    [InlineData("core.fade")]
    [InlineData("core.blur")]
    public void EveryBrushStartsAtOnePixel(string id)
    {
        var session = new EditorWorkspace().NewDocument(8, 8);
        session.SelectTool(id);
        var size = session.GetToolOptions().Single(o => o.Id == "brush-size");
        Assert.Equal(1, size.Value);
    }
}
