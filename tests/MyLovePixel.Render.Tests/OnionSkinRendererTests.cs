using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Render.Tests;

public sealed class OnionSkinRendererTests
{
    [Fact]
    public void RepeatedRender_ReusesFrameCache_WithoutMutatingSourceSurfaces()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var bus = new CommandBus(document);
        var layerId = document.LayerOrder[0];
        var frame0 = document.FrameOrder[0];
        var cel0 = document.FindCel(layerId, frame0)!;
        bus.Execute(new PixelPatchCommand(
            cel0.SurfaceId,
            [new PixelWrite(0, 0, new Rgba32(255, 0, 0, 255))]));

        var copy1 = new CopyFrameCommand(frame0, FrameCopyMode.Independent);
        bus.Execute(copy1);
        var frame1 = copy1.NewFrameId;
        var cel1 = document.FindCel(layerId, frame1)!;
        bus.Execute(new PixelPatchCommand(
            cel1.SurfaceId,
            [new PixelWrite(0, 0, Rgba32.Transparent)]));

        var copy2 = new CopyFrameCommand(frame1, FrameCopyMode.Independent);
        bus.Execute(copy2);
        var frame2 = copy2.NewFrameId;
        var cel2 = document.FindCel(layerId, frame2)!;
        bus.Execute(new PixelPatchCommand(
            cel2.SurfaceId,
            [new PixelWrite(0, 0, new Rgba32(0, 0, 255, 255))]));

        var liveRevisions = document.Resources.SurfaceIds.ToDictionary(
            id => id,
            id => document.Resources.GetSurface(id).Revision);
        var snapshot = DocumentSnapshot.Capture(document);
        var frameRenderer = new FrameRenderer();
        var onion = new OnionSkinRenderer(frameRenderer);
        var request = new FrameRenderRequest(frame1);
        var settings = new OnionSkinSettings(previousFrames: 1, nextFrames: 1, opacity: 128);

        var first = onion.Render(snapshot, request, settings);
        var afterFirst = frameRenderer.Diagnostics.Snapshot();
        var second = onion.Render(snapshot, request, settings);
        var afterSecond = frameRenderer.Diagnostics.Snapshot();

        Assert.Equal([frame0], first.PreviousFrameIds);
        Assert.Equal([frame2], first.NextFrameIds);
        Assert.Equal(Rgba32.Transparent, first.CurrentFrame.Surface.GetPixel(0, 0));
        Assert.NotEqual(Rgba32.Transparent, first.Surface.GetPixel(0, 0));
        Assert.Equal(first.Surface.Bytes.ToArray(), second.Surface.Bytes.ToArray());

        Assert.Equal(3, afterFirst.FullRecomposeCount);
        Assert.Equal(afterFirst.FullRecomposeCount, afterSecond.FullRecomposeCount);
        Assert.Equal(afterFirst.CacheHitCount + 3, afterSecond.CacheHitCount);

        foreach (var pair in liveRevisions)
            Assert.Equal(pair.Value, document.Resources.GetSurface(pair.Key).Revision);
    }
}
