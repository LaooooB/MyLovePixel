using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Render.Tests;

public sealed class ThumbnailCacheTests
{
    [Fact]
    public void SameVisualKey_HitsCacheAndReturnsImmutableThumbnail()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var frameId = document.FrameOrder.Single();
        var cache = new ThumbnailCache(new ThumbnailCacheOptions(MaxEntries: 4, MaxBytes: 4096));
        var snapshot = DocumentSnapshot.Capture(document);

        var first = cache.Get(snapshot, frameId, new IntSize(2, 2));
        var second = cache.Get(snapshot, frameId, new IntSize(2, 2));

        Assert.Same(first, second);
        Assert.Equal(1, cache.Diagnostics.HitCount);
        Assert.Equal(1, cache.Diagnostics.MissCount);
        Assert.Equal(0.5, cache.Diagnostics.HitRatio, 8);
    }

    [Fact]
    public void SurfaceRevisionChange_ProducesCacheMissAndNewPixels()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var cel = document.Cels.Single();
        var frameId = document.FrameOrder.Single();
        var cache = new ThumbnailCache(new ThumbnailCacheOptions(MaxEntries: 4, MaxBytes: 4096));
        var before = cache.Get(DocumentSnapshot.Capture(document), frameId, new IntSize(2, 2));

        new CommandBus(document).Execute(new PixelPatchCommand(
            cel.SurfaceId,
            [new PixelWrite(0, 0, new Rgba32(255, 0, 0, 255))]));
        var after = cache.Get(DocumentSnapshot.Capture(document), frameId, new IntSize(2, 2));

        Assert.NotSame(before, after);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, before.Rgba.Slice(0, 4).ToArray());
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, after.Rgba.Slice(0, 4).ToArray());
        Assert.Equal(2, cache.Diagnostics.MissCount);
    }

    [Fact]
    public void EntryBudget_UsesTrueLeastRecentlyUsedEviction()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var first = document.FrameOrder.Single();
        var bus = new CommandBus(document);

        var beforeFirstCopy = document.FrameOrder.ToHashSet();
        bus.Execute(new CopyFrameCommand(first, FrameCopyMode.Linked));
        var second = document.FrameOrder.Single(id => !beforeFirstCopy.Contains(id));

        var beforeSecondCopy = document.FrameOrder.ToHashSet();
        bus.Execute(new CopyFrameCommand(first, FrameCopyMode.Linked));
        var third = document.FrameOrder.Single(id => !beforeSecondCopy.Contains(id));

        var snapshot = DocumentSnapshot.Capture(document);
        var cache = new ThumbnailCache(new ThumbnailCacheOptions(MaxEntries: 2, MaxBytes: 4096));

        cache.Get(snapshot, first, new IntSize(1, 1));
        cache.Get(snapshot, second, new IntSize(1, 1));
        cache.Get(snapshot, first, new IntSize(1, 1)); // first becomes MRU; second becomes LRU.
        cache.Get(snapshot, third, new IntSize(1, 1));
        var missesBefore = cache.Diagnostics.MissCount;
        cache.Get(snapshot, second, new IntSize(1, 1));

        Assert.Equal(missesBefore + 1, cache.Diagnostics.MissCount);
        Assert.True(cache.Diagnostics.EvictionCount >= 2);
        Assert.Equal(2, cache.Diagnostics.EntryCount);
    }

    [Fact]
    public void OversizeThumbnail_BypassesCacheWithoutBreakingBudget()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var frameId = document.FrameOrder.Single();
        var snapshot = DocumentSnapshot.Capture(document);
        var cache = new ThumbnailCache(new ThumbnailCacheOptions(MaxEntries: 4, MaxBytes: 8));

        cache.Get(snapshot, frameId, new IntSize(4, 4));
        cache.Get(snapshot, frameId, new IntSize(4, 4));

        Assert.Equal(0, cache.Diagnostics.EntryCount);
        Assert.Equal(2, cache.Diagnostics.MissCount);
        Assert.Equal(2, cache.Diagnostics.OversizeBypassCount);
        Assert.Equal(0, cache.Diagnostics.ByteCount);
    }
}
