using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Tools.Tests;

public sealed class ToolHostTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Green = new(0, 255, 0, 255);
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    [Fact]
    public void PencilStroke_PreviewsWithoutMutation_CommitsOneUndo_AndCapturesSurfaceOnce()
    {
        var fixture = CreateHost(128, 1, new PencilTool(), Red);

        fixture.Host.Dispatch(Pointer(7, PointerEventKind.Pressed, 0, 0, PointerButtons.Primary));
        for (var x = 1; x < 128; x++)
            fixture.Host.Dispatch(Pointer(7, PointerEventKind.Moved, x, 0, PointerButtons.Primary));

        Assert.NotNull(fixture.Host.Preview);
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(0, 0));
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(127, 0));
        Assert.Equal(0, fixture.Bus.UndoCount);
        Assert.Equal(1, fixture.Reader.SurfaceSnapshotCaptureCount);

        var released = fixture.Host.Dispatch(Pointer(7, PointerEventKind.Released, 127, 0));

        Assert.True(released.Committed);
        Assert.Null(fixture.Host.Preview);
        Assert.Equal(Red, fixture.Surface.GetPixel(0, 0));
        Assert.Equal(Red, fixture.Surface.GetPixel(64, 0));
        Assert.Equal(Red, fixture.Surface.GetPixel(127, 0));
        Assert.Equal(1, fixture.Bus.UndoCount);
        Assert.Equal(1, fixture.Reader.SurfaceSnapshotCaptureCount);

        fixture.Bus.Undo();
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(0, 0));
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(127, 0));
    }

    [Fact]
    public void CancelInteraction_DropsPreviewWithoutMutationOrUndo()
    {
        var fixture = CreateHost(8, 2, new PencilTool(), Red);

        fixture.Host.Dispatch(Pointer(1, PointerEventKind.Pressed, 1, 0, PointerButtons.Primary));
        fixture.Host.Dispatch(Pointer(1, PointerEventKind.Moved, 6, 0, PointerButtons.Primary));
        Assert.NotNull(fixture.Host.Preview);

        fixture.Host.Dispatch(Pointer(1, PointerEventKind.Cancelled, 6, 0));

        Assert.Null(fixture.Host.Preview);
        Assert.Equal(0, fixture.Bus.UndoCount);
        for (var x = 0; x < 8; x++)
            Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(x, 0));
    }

    [Fact]
    public void KeyboardShift_ConstrainsLineBeforeCommit()
    {
        var fixture = CreateHost(6, 4, new LineTool(), Red);
        fixture.Host.SetKeyboardModifiers(KeyModifiers.Shift);

        fixture.Host.Dispatch(Pointer(3, PointerEventKind.Pressed, 1, 1, PointerButtons.Primary));
        fixture.Host.Dispatch(Pointer(3, PointerEventKind.Moved, 4, 2, PointerButtons.Primary));

        Assert.NotNull(fixture.Host.Preview);
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(4, 1));

        fixture.Host.Dispatch(Pointer(3, PointerEventKind.Released, 4, 2));

        Assert.Equal(Red, fixture.Surface.GetPixel(1, 1));
        Assert.Equal(Red, fixture.Surface.GetPixel(4, 1));
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(4, 2));
        Assert.Equal(1, fixture.Bus.UndoCount);
        Assert.Equal(1, fixture.Reader.SurfaceSnapshotCaptureCount);
    }

    [Fact]
    public void ShapeTool_PreviewsShiftConstrainedSquare_ThenCommits()
    {
        var fixture = CreateHost(6, 6, new ShapeTool(), Green);
        fixture.Host.SetOption(ToolOptionIds.Filled, true);
        fixture.Host.SetKeyboardModifiers(KeyModifiers.Shift);

        fixture.Host.Dispatch(Pointer(9, PointerEventKind.Pressed, 1, 1, PointerButtons.Primary));
        fixture.Host.Dispatch(Pointer(9, PointerEventKind.Moved, 3, 2, PointerButtons.Primary));

        Assert.NotNull(fixture.Host.Preview);
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(3, 3));

        fixture.Host.Dispatch(Pointer(9, PointerEventKind.Released, 3, 2));

        Assert.Equal(Green, fixture.Surface.GetPixel(1, 1));
        Assert.Equal(Green, fixture.Surface.GetPixel(3, 3));
        Assert.Equal(1, fixture.Bus.UndoCount);
    }

    [Fact]
    public void ShapeTool_EllipseOptionUsesEllipseRasterizer()
    {
        var fixture = CreateHost(5, 5, new ShapeTool(), Blue);
        fixture.Host.SetOption(ToolOptionIds.ShapeKind, ToolOptionValues.Ellipse);
        fixture.Host.SetOption(ToolOptionIds.Filled, true);

        fixture.Host.Dispatch(Pointer(4, PointerEventKind.Pressed, 0, 0, PointerButtons.Primary));
        fixture.Host.Dispatch(Pointer(4, PointerEventKind.Released, 4, 4));

        Assert.Equal(Blue, fixture.Surface.GetPixel(2, 2));
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(0, 0));
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(4, 4));
        Assert.Equal(1, fixture.Bus.UndoCount);
    }

    [Fact]
    public void FillTool_CommitsOneUndoEntry_AndUndoRestores()
    {
        var fixture = CreateHost(3, 3, new FillTool(), Green);

        var result = fixture.Host.Dispatch(Pointer(2, PointerEventKind.Pressed, 1, 1, PointerButtons.Primary));

        Assert.True(result.Committed);
        Assert.Equal(1, fixture.Bus.UndoCount);
        Assert.Equal(1, fixture.Reader.SurfaceSnapshotCaptureCount);
        for (var y = 0; y < 3; y++)
        for (var x = 0; x < 3; x++)
            Assert.Equal(Green, fixture.Surface.GetPixel(x, y));

        fixture.Bus.Undo();
        for (var y = 0; y < 3; y++)
        for (var x = 0; x < 3; x++)
            Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(x, y));
    }

    [Fact]
    public void EraserStroke_RemovesPixelsAsSingleAdditionalUndoEntry()
    {
        var document = PixelDocumentFactory.CreateBlank(3, 1);
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(cel.SurfaceId,
        [
            new PixelWrite(0, 0, Red),
            new PixelWrite(1, 0, Red),
            new PixelWrite(2, 0, Red),
        ]));
        var reader = new PixelDocumentToolReader(document);
        var host = new ToolHost(reader, bus, ToolTarget.FromCel(cel), new EraserTool());
        var undoBefore = bus.UndoCount;

        host.Dispatch(Pointer(12, PointerEventKind.Pressed, 0, 0, PointerButtons.Primary));
        host.Dispatch(Pointer(12, PointerEventKind.Moved, 2, 0, PointerButtons.Primary));
        host.Dispatch(Pointer(12, PointerEventKind.Released, 2, 0));

        Assert.Equal(Rgba32.Transparent, surface.GetPixel(0, 0));
        Assert.Equal(Rgba32.Transparent, surface.GetPixel(1, 0));
        Assert.Equal(Rgba32.Transparent, surface.GetPixel(2, 0));
        Assert.Equal(undoBefore + 1, bus.UndoCount);
    }

    [Fact]
    public void StaleInteractionRevision_IsRejectedWithoutOverwritingNewerEdit()
    {
        var fixture = CreateHost(4, 1, new PencilTool(), Red);

        fixture.Host.Dispatch(Pointer(5, PointerEventKind.Pressed, 0, 0, PointerButtons.Primary));
        fixture.Host.Dispatch(Pointer(5, PointerEventKind.Moved, 2, 0, PointerButtons.Primary));

        fixture.Bus.Execute(new PixelPatchCommand(
            fixture.Cel.SurfaceId,
            [new PixelWrite(3, 0, Blue)],
            "External Edit"));

        Assert.Throws<ToolInteractionConflictException>(() =>
            fixture.Host.Dispatch(Pointer(5, PointerEventKind.Released, 2, 0)));

        Assert.Null(fixture.Host.Preview);
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(0, 0));
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(2, 0));
        Assert.Equal(Blue, fixture.Surface.GetPixel(3, 0));
        Assert.Equal(1, fixture.Bus.UndoCount);
    }

    [Fact]
    public void TargetOrigin_IsAppliedWhenConvertingCanvasPointerToSurfaceCoordinates()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 1);
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        var bus = new CommandBus(document);
        var reader = new PixelDocumentToolReader(document);
        var target = new ToolTarget(
            cel.Id,
            cel.LayerId,
            cel.FrameId,
            cel.SurfaceId,
            new IntPoint(10, 20));
        var host = new ToolHost(reader, bus, target, new PencilTool(), Red);

        host.Dispatch(Pointer(20, PointerEventKind.Pressed, 11, 20, PointerButtons.Primary));
        host.Dispatch(Pointer(20, PointerEventKind.Released, 11, 20));

        Assert.Equal(Rgba32.Transparent, surface.GetPixel(0, 0));
        Assert.Equal(Red, surface.GetPixel(1, 0));
        Assert.Equal(1, bus.UndoCount);
    }

    [Fact]
    public void ToolOptions_RejectInvalidValues_AndCannotChangeDuringInteraction()
    {
        var fixture = CreateHost(4, 1, new PencilTool(), Red);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.Host.SetOption(ToolOptionIds.BrushSize, 0));

        fixture.Host.Dispatch(Pointer(6, PointerEventKind.Pressed, 0, 0, PointerButtons.Primary));
        Assert.Throws<InvalidOperationException>(() =>
            fixture.Host.SetOption(ToolOptionIds.Spacing, 2));
        fixture.Host.CancelInteraction();
    }

    private static ToolFixture CreateHost(int width, int height, ITool tool, Rgba32 primaryColor)
    {
        var document = PixelDocumentFactory.CreateBlank(width, height);
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        var bus = new CommandBus(document);
        var reader = new PixelDocumentToolReader(document);
        var host = new ToolHost(
            reader,
            bus,
            ToolTarget.FromCel(cel),
            tool,
            primaryColor,
            Rgba32.Transparent);
        return new ToolFixture(document, cel, surface, bus, reader, host);
    }

    private static PointerEvent Pointer(
        long pointerId,
        PointerEventKind kind,
        int x,
        int y,
        PointerButtons buttons = PointerButtons.None,
        KeyModifiers modifiers = KeyModifiers.None) =>
        new(
            pointerId,
            PointerDeviceKind.Mouse,
            kind,
            new IntPoint(x, y),
            1.0,
            buttons,
            modifiers,
            0);

    private sealed record ToolFixture(
        PixelDocument Document,
        Cel Cel,
        PixelSurface Surface,
        CommandBus Bus,
        PixelDocumentToolReader Reader,
        ToolHost Host);
}
