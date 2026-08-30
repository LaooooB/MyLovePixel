using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Tools.Tests;

public sealed class TransparentColorToolTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    [Fact]
    public void FillTool_TransparentPrimary_ErasesConnectedRegion()
    {
        var fixture = CreateFilledHost(3, 1, new FillTool());

        var result = fixture.Host.Dispatch(Pointer(1, PointerEventKind.Pressed, 1, 0, PointerButtons.Primary));

        Assert.True(result.Committed);
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(0, 0));
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(1, 0));
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(2, 0));
    }

    [Fact]
    public void LineTool_TransparentPrimary_ErasesAlongLine()
    {
        var fixture = CreateFilledHost(3, 1, new LineTool());

        fixture.Host.Dispatch(Pointer(2, PointerEventKind.Pressed, 0, 0, PointerButtons.Primary));
        var result = fixture.Host.Dispatch(Pointer(2, PointerEventKind.Released, 2, 0));

        Assert.True(result.Committed);
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(0, 0));
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(1, 0));
        Assert.Equal(Rgba32.Transparent, fixture.Surface.GetPixel(2, 0));
    }

    private static Fixture CreateFilledHost(int width, int height, ITool tool)
    {
        var document = PixelDocumentFactory.CreateBlank(width, height);
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(
            cel.SurfaceId,
            Enumerable.Range(0, width * height)
                .Select(index => new PixelWrite(index % width, index / width, Red))
                .ToArray(),
            "Seed Pixels"));
        var reader = new PixelDocumentToolReader(document);
        var host = new ToolHost(
            reader,
            bus,
            ToolTarget.FromCel(cel),
            tool,
            Rgba32.Transparent,
            Red);
        return new Fixture(surface, host);
    }

    private static PointerEvent Pointer(
        long pointerId,
        PointerEventKind kind,
        int x,
        int y,
        PointerButtons buttons = PointerButtons.None) =>
        new(
            pointerId,
            PointerDeviceKind.Mouse,
            kind,
            new IntPoint(x, y),
            1.0,
            buttons,
            KeyModifiers.None,
            0);

    private sealed record Fixture(PixelSurface Surface, ToolHost Host);
}
