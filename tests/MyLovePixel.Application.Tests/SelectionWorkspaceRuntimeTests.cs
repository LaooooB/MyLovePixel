using MyLovePixel.Commands.Pixel;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class SelectionWorkspaceRuntimeTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    [Fact]
    public void SwitchingFrame_InvalidatesSelectionAndCannotMutatePreviousFrame()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(4, 1);
        var initial = session.CaptureSnapshot();
        var firstFrame = initial.FrameOrder.Single();
        var firstCel = initial.Cels.Single();
        session.Execute(new PixelPatchCommand(firstCel.SurfaceId, [new PixelWrite(0, 0, Red)]));

        var selection = new SelectionWorkspaceRuntime();
        selection.SelectRectangle(session, 0, 0, 0, 0);
        Assert.NotNull(selection.GetOverlay(session));

        var copy = new CopyFrameCommand(firstFrame, FrameCopyMode.Independent);
        session.Execute(copy);
        session.SelectFrame(copy.NewFrameId);

        Assert.Null(selection.GetOverlay(session));
        Assert.Throws<InvalidOperationException>(() => selection.Move(session, 1, 0));

        var after = session.CaptureSnapshot();
        var firstSurface = after.GetSurface(firstCel.SurfaceId);
        Assert.Equal(Red, firstSurface.GetPixel(0, 0));
        Assert.Equal(Rgba32.Transparent, firstSurface.GetPixel(1, 0));
    }
}
