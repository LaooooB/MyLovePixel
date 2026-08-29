using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Tools.Tests;

public sealed class SpecialBrushToolTests
{
    [Fact]
    public void ArcTool_CommitsPixelsAwayFromStraightChord()
    {
        var fixture = CreateHost(12, 12, new ArcTool(), new Rgba32(255, 0, 0, 255));
        fixture.Host.SetOption(SpecialToolOptionIds.Bend, 70);

        fixture.Host.Dispatch(Pointer(1, PointerEventKind.Pressed, 1, 6, PointerButtons.Primary));
        fixture.Host.Dispatch(Pointer(1, PointerEventKind.Released, 10, 6));

        Assert.Equal(1, fixture.Reader.SurfaceSnapshotCaptureCount);
        Assert.True(Enumerable.Range(0, 12).Any(y =>
            y != 6 && Enumerable.Range(0, 12).Any(x => fixture.Surface.GetPixel(x, y).A != 0)));
    }

    [Fact]
    public void FadeBrush_ReducesAlpha_AndCreatesSingleUndoEntry()
    {
        var fixture = CreateSeededHost(
            5,
            5,
            new FadeBrushTool(),
            new PixelWrite(2, 2, new Rgba32(100, 150, 200, 255)));
        var undoBefore = fixture.Bus.UndoCount;
        fixture.Host.SetOption(SpecialToolOptionIds.Strength, 50);
        fixture.Host.SetOption(ToolOptionIds.BrushSize, 1);

        fixture.Host.Dispatch(Pointer(2, PointerEventKind.Pressed, 2, 2, PointerButtons.Primary));
        fixture.Host.Dispatch(Pointer(2, PointerEventKind.Released, 2, 2));

        var result = fixture.Surface.GetPixel(2, 2);
        Assert.Equal((byte)100, result.R);
        Assert.Equal((byte)150, result.G);
        Assert.Equal((byte)200, result.B);
        Assert.InRange(result.A, (byte)127, (byte)128);
        Assert.Equal(undoBefore + 1, fixture.Bus.UndoCount);
        Assert.Equal(1, fixture.Reader.SurfaceSnapshotCaptureCount);
    }

    [Fact]
    public void ShadowBrush_DarkensWithoutChangingAlpha()
    {
        var fixture = CreateSeededHost(
            5,
            5,
            new ShadowBrushTool(),
            new PixelWrite(2, 2, new Rgba32(200, 160, 120, 255)));
        fixture.Host.SetOption(SpecialToolOptionIds.Strength, 50);
        fixture.Host.SetOption(ToolOptionIds.BrushSize, 1);

        fixture.Host.Dispatch(Pointer(3, PointerEventKind.Pressed, 2, 2, PointerButtons.Primary));
        fixture.Host.Dispatch(Pointer(3, PointerEventKind.Released, 2, 2));

        Assert.Equal(new Rgba32(100, 80, 60, 255), fixture.Surface.GetPixel(2, 2));
    }

    [Fact]
    public void BlurBrush_CapturesOriginalSurfaceOnlyOncePerStroke()
    {
        var fixture = CreateSeededHost(
            8,
            3,
            new BlurBrushTool(),
            new PixelWrite(1, 1, new Rgba32(255, 0, 0, 255)),
            new PixelWrite(4, 1, new Rgba32(0, 0, 255, 255)));
        fixture.Host.SetOption(ToolOptionIds.BrushSize, 1);
        fixture.Host.SetOption(SpecialToolOptionIds.Radius, 2);
        fixture.Host.SetOption(SpecialToolOptionIds.Strength, 60);

        fixture.Host.Dispatch(Pointer(4, PointerEventKind.Pressed, 1, 1, PointerButtons.Primary));
        fixture.Host.Dispatch(Pointer(4, PointerEventKind.Moved, 2, 1, PointerButtons.Primary));
        fixture.Host.Dispatch(Pointer(4, PointerEventKind.Moved, 3, 1, PointerButtons.Primary));
        fixture.Host.Dispatch(Pointer(4, PointerEventKind.Released, 4, 1));

        Assert.Equal(1, fixture.Reader.SurfaceSnapshotCaptureCount);
        Assert.True(fixture.Bus.UndoCount >= 2);
    }

    private static ToolFixture CreateHost(int width, int height, ITool tool, Rgba32 primaryColor)
    {
        var document = PixelDocumentFactory.CreateBlank(width, height);
        return CreateHost(document, tool, primaryColor);
    }

    private static ToolFixture CreateSeededHost(int width, int height, ITool tool, params PixelWrite[] writes)
    {
        var document = PixelDocumentFactory.CreateBlank(width, height);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(cel.SurfaceId, writes, "Seed"));
        return CreateHost(document, tool, new Rgba32(255, 255, 255, 255), bus);
    }

    private static ToolFixture CreateHost(PixelDocument document, ITool tool, Rgba32 primaryColor, CommandBus? existingBus = null)
    {
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        var bus = existingBus ?? new CommandBus(document);
        var reader = new PixelDocumentToolReader(document);
        var host = new ToolHost(reader, bus, ToolTarget.FromCel(cel), tool, primaryColor, Rgba32.Transparent);
        return new ToolFixture(surface, bus, reader, host);
    }

    private static PointerEvent Pointer(
        long pointerId,
        PointerEventKind kind,
        int x,
        int y,
        PointerButtons buttons = PointerButtons.None) =>
        new(pointerId, PointerDeviceKind.Mouse, kind, new IntPoint(x, y), 1d, buttons, KeyModifiers.None, 0);

    private sealed record ToolFixture(
        PixelSurface Surface,
        CommandBus Bus,
        PixelDocumentToolReader Reader,
        ToolHost Host);
}
