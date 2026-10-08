using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class BrushDefaultTests
{
    [Theory]
    [InlineData("core.pencil")]
    [InlineData("core.eraser")]
    [InlineData("core.line")]
    [InlineData("core.arc")]
    [InlineData("core.shape")]
    [InlineData("core.blur")]
    [InlineData("core.fade")]
    [InlineData("core.shadow")]
    [InlineData("core.highlight")]
    public void Every_brush_starts_at_one_pixel_and_stays_adjustable(string id)
    {
        var session = new EditorWorkspace().NewDocument(8, 8);
        session.SelectTool(id);
        var size = session.GetToolOptions().Single(x => x.Id == "brush-size");
        Assert.Equal(1, size.Value);
        session.SetToolOption("brush-size", 5);
        Assert.Equal(5, session.GetToolOptions().Single(x => x.Id == "brush-size").Value);
        Assert.False(session.IsDirty);
        Assert.Equal(0, session.Commands.UndoCount);
    }
}
