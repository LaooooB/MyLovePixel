using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Validation;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Core.Tests;

public sealed class DocumentCoreTests
{
    [Fact]
    public void BlankDocument_IsValid()
    {
        var document = PixelDocumentFactory.CreateBlank(32, 32);
        Assert.Empty(DocumentValidator.Validate(document));
        Assert.Single(document.Cels);
        Assert.Single(document.LayerOrder);
        Assert.Single(document.FrameOrder);
    }

    [Fact]
    public void PixelPatch_UndoRedo_RestoresExactPixel()
    {
        var document = PixelDocumentFactory.CreateBlank(8, 8);
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        var bus = new CommandBus(document);
        var red = new Rgba32(255, 0, 0, 255);

        bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(2, 3, red)]));
        Assert.Equal(red, surface.GetPixel(2, 3));

        bus.Undo();
        Assert.Equal(Rgba32.Transparent, surface.GetPixel(2, 3));

        bus.Redo();
        Assert.Equal(red, surface.GetPixel(2, 3));
        Assert.Empty(DocumentValidator.Validate(document));
    }

    [Fact]
    public void Transaction_MultipleCommands_UsesOneUndoEntry()
    {
        var document = PixelDocumentFactory.CreateBlank(8, 8);
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        var bus = new CommandBus(document);

        using (var transaction = bus.BeginTransaction("Stroke"))
        {
            bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(1, 1, new Rgba32(1, 2, 3))]));
            bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(2, 1, new Rgba32(4, 5, 6))]));
            transaction.Commit();
        }

        Assert.Equal(1, bus.UndoCount);
        bus.Undo();
        Assert.Equal(Rgba32.Transparent, surface.GetPixel(1, 1));
        Assert.Equal(Rgba32.Transparent, surface.GetPixel(2, 1));
    }

    [Fact]
    public void Snapshot_IsIndependentFromLiveSurfaceMutation()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var cel = document.Cels.Single();
        var snapshot = DocumentSnapshot.Capture(document);
        var bus = new CommandBus(document);

        bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, new Rgba32(9, 9, 9))]));

        Assert.Equal(Rgba32.Transparent, snapshot.Surfaces[cel.SurfaceId].GetPixel(0, 0));
        Assert.Equal(new Rgba32(9, 9, 9), document.Resources.GetSurface(cel.SurfaceId).GetPixel(0, 0));
    }

    [Fact]
    public void Transaction_DisposeWithoutCommit_RollsBackAndDoesNotTouchHistory()
    {
        var document = PixelDocumentFactory.CreateBlank(8, 8);
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        var bus = new CommandBus(document);

        using (bus.BeginTransaction("Cancelled Stroke"))
        {
            bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(1, 1, new Rgba32(12, 34, 56))]));
            Assert.Equal(new Rgba32(12, 34, 56), surface.GetPixel(1, 1));
        }

        Assert.Equal(Rgba32.Transparent, surface.GetPixel(1, 1));
        Assert.Equal(0, bus.UndoCount);
        Assert.Equal(0, bus.RedoCount);
    }

    [Fact]
    public void PixelPatch_RejectsEmptyWriteSet()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var cel = document.Cels.Single();
        Assert.Throws<ArgumentException>(() => new PixelPatchCommand(cel.SurfaceId, Array.Empty<PixelWrite>()));
    }

    [Fact]
    public void UnlinkCel_CloneIsIndependent_AndUndoRestoresReference()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var originalCel = document.Cels.Single();
        var originalSurface = originalCel.SurfaceId;

        // Create a second frame/cel as a deliberately linked Cel using internal test access.
        var frame = new Frame(FrameId.New());
        document.AddFrame(frame);
        var linkedCel = new Cel(CelId.New(), originalCel.LayerId, frame.Id, originalSurface);
        document.AddCel(linkedCel);

        var bus = new CommandBus(document);
        bus.Execute(new UnlinkCelCommand(linkedCel.Id));

        Assert.NotEqual(originalSurface, linkedCel.SurfaceId);
        Assert.True(document.Resources.ContainsSurface(linkedCel.SurfaceId));

        bus.Execute(new PixelPatchCommand(linkedCel.SurfaceId, [new PixelWrite(0, 0, new Rgba32(255, 255, 255))]));
        Assert.Equal(Rgba32.Transparent, document.Resources.GetSurface(originalSurface).GetPixel(0, 0));

        bus.Undo(); // Undo pixel patch.
        bus.Undo(); // Undo unlink.
        Assert.Equal(originalSurface, linkedCel.SurfaceId);
        Assert.Empty(DocumentValidator.Validate(document));
    }
}
