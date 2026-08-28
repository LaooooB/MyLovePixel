using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Render;
using MyLovePixel.Selection;
using SkiaSharp;
using Xunit;

namespace MyLovePixel.Render.Tests;

public sealed class RenderGraphTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Green = new(0, 255, 0, 255);
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    [Fact]
    public void DocumentSnapshot_CapturesRenderableStructure_AndIsolatedState()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 3);
        var layerId = document.LayerOrder.Single();
        var frameId = document.FrameOrder.Single();
        var layer = document.GetLayer(layerId);
        var snapshot = DocumentSnapshot.Capture(document);

        layer.Visible = false;
        layer.Opacity = 17;
        document.GetFrame(frameId).DurationTicks = 5;

        Assert.True(snapshot.GetLayer(layerId).Visible);
        Assert.Equal(byte.MaxValue, snapshot.GetLayer(layerId).Opacity);
        Assert.Equal(Frame.DefaultDurationTicks, snapshot.GetFrame(frameId).DurationTicks);
        Assert.Equal(LayerSnapshotKind.Pixel, snapshot.GetLayer(layerId).Kind);

        var layerList = Assert.IsAssignableFrom<IList<LayerId>>(snapshot.LayerOrder);
        Assert.Throws<NotSupportedException>(() => layerList.Add(LayerId.New()));
    }

    [Fact]
    public void CpuCompositor_UsesLayerOrder_Position_AndStraightAlpha()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 1);
        var firstLayerId = document.LayerOrder.Single();
        var frameId = document.FrameOrder.Single();
        var firstSurfaceId = document.Cels.Single().SurfaceId;
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(
            firstSurfaceId,
            [new PixelWrite(0, 0, Red), new PixelWrite(1, 0, Red)]));

        var secondLayer = new PixelLayer(LayerId.New(), "Top");
        secondLayer.Opacity = 128;
        document.AddLayer(secondLayer);
        var secondSurfaceId = document.Resources.AddSurface(
            new PixelSurface(new IntSize(1, 1), Green));
        var secondCel = new Cel(
            CelId.New(),
            secondLayer.Id,
            frameId,
            secondSurfaceId)
        {
            Position = new IntPoint(1, 0),
        };
        document.AddCel(secondCel);

        var renderer = new FrameRenderer();
        var result = renderer.Render(
            DocumentSnapshot.Capture(document),
            new FrameRenderRequest(frameId));

        Assert.Equal(Red, result.Surface.GetPixel(0, 0));
        Assert.Equal(new Rgba32(127, 128, 0, 255), result.Surface.GetPixel(1, 0));
        Assert.Equal(RenderCacheOutcome.FullRecompose, result.CacheOutcome);
        Assert.Equal(TextureUploadMode.Full, result.UploadPlan.Mode);
        Assert.Equal(firstLayerId, document.LayerOrder[0]);
    }

    [Fact]
    public void SixteenBySixteenPatch_RecomposesOnlyDirtyCanvasRegion()
    {
        var document = PixelDocumentFactory.CreateBlank(64, 64);
        var frameId = document.FrameOrder.Single();
        var surfaceId = document.Cels.Single().SurfaceId;
        var surface = document.Resources.GetSurface(surfaceId);
        var renderer = new FrameRenderer();
        var bus = new CommandBus(document);

        renderer.Render(
            DocumentSnapshot.Capture(document),
            new FrameRenderRequest(frameId));

        var beforeRevision = surface.Revision;
        var writes = new List<PixelWrite>(16 * 16);
        for (var y = 20; y < 36; y++)
        for (var x = 12; x < 28; x++)
            writes.Add(new PixelWrite(x, y, Blue));

        var change = bus.Execute(new PixelPatchCommand(surfaceId, writes));
        var dirty = change.DirtySurfaces.Single();
        var afterRevision = surface.Revision;

        var result = renderer.Render(
            DocumentSnapshot.Capture(document),
            new FrameRenderRequest(
                frameId,
                [new SurfaceInvalidation(
                    surfaceId,
                    beforeRevision,
                    afterRevision,
                    dirty.Region)]));

        Assert.Equal(RenderCacheOutcome.PartialRecompose, result.CacheOutcome);
        Assert.True(result.UsedPartialRecompose);
        Assert.Equal(TextureUploadMode.Partial, result.UploadPlan.Mode);
        Assert.Equal(16 * 16, result.UploadPlan.PixelCount);
        Assert.Equal(16 * 16, result.Diagnostics.LastRecomposedPixelCount);
        Assert.Equal(Blue, result.Surface.GetPixel(12, 20));
        Assert.Equal(Rgba32.Transparent, result.Surface.GetPixel(11, 20));

        var hit = renderer.Render(
            DocumentSnapshot.Capture(document),
            new FrameRenderRequest(frameId));

        Assert.Equal(RenderCacheOutcome.CacheHit, hit.CacheOutcome);
        Assert.Equal(TextureUploadMode.None, hit.UploadPlan.Mode);
        Assert.Equal(0, hit.Diagnostics.LastRecomposedPixelCount);
    }

    [Fact]
    public void RevisionChangeWithoutCompleteDirtyHistory_FallsBackToFullRecompose()
    {
        var document = PixelDocumentFactory.CreateBlank(8, 8);
        var frameId = document.FrameOrder.Single();
        var surfaceId = document.Cels.Single().SurfaceId;
        var renderer = new FrameRenderer();
        var bus = new CommandBus(document);

        renderer.Render(
            DocumentSnapshot.Capture(document),
            new FrameRenderRequest(frameId));

        bus.Execute(new PixelPatchCommand(
            surfaceId,
            [new PixelWrite(2, 2, Red)]));

        var result = renderer.Render(
            DocumentSnapshot.Capture(document),
            new FrameRenderRequest(frameId));

        Assert.Equal(RenderCacheOutcome.FullRecompose, result.CacheOutcome);
        Assert.Equal(TextureUploadMode.Full, result.UploadPlan.Mode);
        Assert.Equal(8 * 8, result.Diagnostics.LastRecomposedPixelCount);
        Assert.Equal(Red, result.Surface.GetPixel(2, 2));
    }

    [Fact]
    public void DirtyRegionForLinkedSurface_MapsToEveryVisibleCel()
    {
        var document = PixelDocumentFactory.CreateBlank(32, 8);
        var frameId = document.FrameOrder.Single();
        var firstCel = document.Cels.Single();
        var sharedSurfaceId = firstCel.SurfaceId;

        var linkedLayer = new PixelLayer(LayerId.New(), "Linked");
        document.AddLayer(linkedLayer);
        document.AddCel(new Cel(
            CelId.New(),
            linkedLayer.Id,
            frameId,
            sharedSurfaceId)
        {
            Position = new IntPoint(10, 0),
        });

        var renderer = new FrameRenderer();
        var bus = new CommandBus(document);
        renderer.Render(
            DocumentSnapshot.Capture(document),
            new FrameRenderRequest(frameId));

        var surface = document.Resources.GetSurface(sharedSurfaceId);
        var beforeRevision = surface.Revision;
        var change = bus.Execute(new PixelPatchCommand(
            sharedSurfaceId,
            [new PixelWrite(0, 0, Green)]));
        var afterRevision = surface.Revision;

        var result = renderer.Render(
            DocumentSnapshot.Capture(document),
            new FrameRenderRequest(
                frameId,
                [new SurfaceInvalidation(
                    sharedSurfaceId,
                    beforeRevision,
                    afterRevision,
                    change.DirtySurfaces.Single().Region)]));

        Assert.Equal(RenderCacheOutcome.PartialRecompose, result.CacheOutcome);
        Assert.Equal(2, result.UploadPlan.PixelCount);
        Assert.Equal(Green, result.Surface.GetPixel(0, 0));
        Assert.Equal(Green, result.Surface.GetPixel(10, 0));
    }

    [Fact]
    public void OverlayChanges_DoNotInvalidateFrameCompositeCache()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var frameId = document.FrameOrder.Single();
        var snapshot = DocumentSnapshot.Capture(document);
        var renderer = new FrameRenderer();

        renderer.Render(snapshot, new FrameRenderRequest(frameId));

        var selection = SelectionMask.FromCoverage(
            snapshot.Canvas.Size,
            SelectionMaskFormat.Bit1,
            new byte[]
            {
                0, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
            });

        IRenderOverlayPass[] passes =
        [
            new PixelGridOverlayPass(minimumScale: 1),
            new GuideOverlayPass([new GuideLine(GuideOrientation.Vertical, 2)]),
            new SelectionOutlineOverlayPass(selection),
            new ToolPreviewOverlayPass(
                [new ToolPreviewPixel(new IntPoint(3, 3), Red)]),
        ];

        var result = renderer.Render(
            snapshot,
            new FrameRenderRequest(
                frameId,
                View: new ViewTransform(8),
                Viewport: new ViewRect(0, 0, 32, 32),
                OverlayPasses: passes));

        Assert.Equal(RenderCacheOutcome.CacheHit, result.CacheOutcome);
        Assert.Equal(TextureUploadMode.None, result.UploadPlan.Mode);
        Assert.NotEmpty(result.Overlays.Commands);
    }

    [Fact]
    public void CacheClear_RebuildsSamePixelsFromSnapshot()
    {
        var document = PixelDocumentFactory.CreateBlank(3, 2);
        var frameId = document.FrameOrder.Single();
        var surfaceId = document.Cels.Single().SurfaceId;
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(
            surfaceId,
            [new PixelWrite(1, 1, Blue)]));

        var snapshot = DocumentSnapshot.Capture(document);
        var renderer = new FrameRenderer();
        var first = renderer.Render(snapshot, new FrameRenderRequest(frameId));
        var firstBytes = first.Surface.Bytes.ToArray();

        renderer.ClearCaches();
        var rebuilt = renderer.Render(snapshot, new FrameRenderRequest(frameId));

        Assert.Equal(RenderCacheOutcome.FullRecompose, rebuilt.CacheOutcome);
        Assert.Equal(firstBytes, rebuilt.Surface.Bytes.ToArray());
    }

    [Fact]
    public void ViewTransform_RoundTripsCanvasCoordinates()
    {
        var view = new ViewTransform(8, 13, -5);
        var point = view.CanvasToView(3, 7);
        var canvas = view.ViewToCanvas(point.X, point.Y);

        Assert.Equal(3, canvas.X);
        Assert.Equal(7, canvas.Y);
        Assert.Equal(new IntPoint(3, 7), view.ViewToCanvasPixel(point.X, point.Y));
    }

    [Fact]
    public void SkiaCache_ReuploadsFullSurfaceAfterBackendCacheLoss()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 1);
        var frameId = document.FrameOrder.Single();
        var surfaceId = document.Cels.Single().SurfaceId;
        var bus = new CommandBus(document);
        bus.Execute(new PixelPatchCommand(
            surfaceId,
            [new PixelWrite(0, 0, Red)]));

        var snapshot = DocumentSnapshot.Capture(document);
        var renderer = new FrameRenderer();
        var first = renderer.Render(snapshot, new FrameRenderRequest(frameId));

        using var skiaCache = new SkiaFrameCache();
        var bitmap = skiaCache.Update(document.Id, frameId, first);
        AssertPixelBytes(bitmap, 0, 0, Red);

        var hit = renderer.Render(snapshot, new FrameRenderRequest(frameId));
        Assert.Equal(TextureUploadMode.None, hit.UploadPlan.Mode);

        skiaCache.ClearCaches();
        var rebuiltBitmap = skiaCache.Update(document.Id, frameId, hit);

        Assert.Equal(1, skiaCache.Count);
        AssertPixelBytes(rebuiltBitmap, 0, 0, Red);
        Assert.Equal(SKFilterMode.Nearest, SkiaCanvasPresenter.NearestSampling.Filter);
    }

    private static void AssertPixelBytes(
        SKBitmap bitmap,
        int x,
        int y,
        Rgba32 expected)
    {
        var bytes = bitmap.GetPixelSpan(x, y);
        Assert.Equal(expected.R, bytes[0]);
        Assert.Equal(expected.G, bytes[1]);
        Assert.Equal(expected.B, bytes[2]);
        Assert.Equal(expected.A, bytes[3]);
    }
}
