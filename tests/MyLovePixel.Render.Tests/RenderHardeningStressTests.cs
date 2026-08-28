using MyLovePixel.Commands;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Render.Tests;

public sealed class RenderHardeningStressTests
{
    [Fact]
    public void ThousandFrameThumbnailSweep_RemainsInsideLruBudgets()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var first = document.FrameOrder.Single();
        var bus = new CommandBus(document);
        for (var index = 1; index < 1000; index++)
            bus.Execute(new CopyFrameCommand(first, FrameCopyMode.Linked));

        var snapshot = DocumentSnapshot.Capture(document);
        var cache = new ThumbnailCache(new ThumbnailCacheOptions(MaxEntries: 64, MaxBytes: 256));
        foreach (var frameId in snapshot.FrameOrder)
            cache.Get(snapshot, frameId, new IntSize(1, 1));

        var diagnostics = cache.Diagnostics;
        Assert.Equal(1000, diagnostics.MissCount);
        Assert.Equal(64, diagnostics.EntryCount);
        Assert.Equal(256, diagnostics.ByteCount);
        Assert.Equal(936, diagnostics.EvictionCount);
        Assert.Equal(0, diagnostics.OversizeBypassCount);
    }

    [Fact]
    public void RenderCacheRates_ReportHitsAndMissesWithoutChangingCacheSemantics()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var snapshot = DocumentSnapshot.Capture(document);
        var frameId = snapshot.FrameOrder.Single();
        var renderer = new FrameRenderer();

        var first = renderer.Render(snapshot, new FrameRenderRequest(frameId));
        var second = renderer.Render(snapshot, new FrameRenderRequest(frameId));
        var rates = second.Diagnostics.GetRates();

        Assert.Equal(RenderCacheOutcome.FullRecompose, first.CacheOutcome);
        Assert.Equal(RenderCacheOutcome.CacheHit, second.CacheOutcome);
        Assert.Equal(2, rates.RequestCount);
        Assert.Equal(1, rates.MissCount);
        Assert.Equal(1, rates.HitCount);
        Assert.Equal(0.5, rates.HitRatio, 8);
    }
}
