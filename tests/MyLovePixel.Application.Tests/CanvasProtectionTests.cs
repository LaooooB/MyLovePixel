using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class CanvasProtectionTests
{
    [Fact]
    public void Clear_frame_keeps_locked_layers_and_is_one_undo_step()
    {
        var session = new EditorWorkspace().NewDocument(8, 8);
        var locked = session.CurrentLayerId;
        var lockedCel = session.CaptureSnapshot().Cels.Single();
        var red = new Rgba32(200, 30, 40, 255);
        session.Execute(new PixelPatchCommand(lockedCel.SurfaceId, [new PixelWrite(1, 1, red)]));
        session.SetLayerLocked(locked, true);
        session.AddLayer(); session.EnsureEditableCel();
        var editableCel = session.CaptureSnapshot().Cels.Single(c => c.LayerId == session.CurrentLayerId);
        session.Execute(new PixelPatchCommand(editableCel.SurfaceId, [new PixelWrite(2, 2, red)]));
        var count = session.Commands.UndoCount;
        session.ClearCurrentCanvas();
        var cleared = session.CaptureSnapshot();
        Assert.Equal(red, cleared.GetSurface(lockedCel.SurfaceId).GetPixel(1, 1));
        Assert.Equal(Rgba32.Transparent, cleared.GetSurface(editableCel.SurfaceId).GetPixel(2, 2));
        Assert.Equal(count + 1, session.Commands.UndoCount);
        session.Undo();
        Assert.Equal(red, session.CaptureSnapshot().GetSurface(editableCel.SurfaceId).GetPixel(2, 2));
    }

    [Fact]
    public void Clearing_an_already_empty_frame_does_not_dirty_it()
    {
        var session = new EditorWorkspace().NewDocument(8, 8);
        session.ClearCurrentCanvas();
        Assert.False(session.IsDirty);
        Assert.False(session.CanUndo);
    }
}
