using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Render;

public sealed class FrameCompositeRenderNode : IRenderNode
{
    public string Id => "core.frame-composite";
    public long Revision => 0;

    public void Execute(RenderNodeContext context, IRenderTarget target, IntRect region)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(target);

        if (target.Size != context.Snapshot.Canvas.Size)
            throw new ArgumentException("Render target size must match the document canvas.", nameof(target));
        if (context.Snapshot.Canvas.PixelFormat != PixelFormat.Rgba32)
            throw new NotSupportedException($"Canvas pixel format '{context.Snapshot.Canvas.PixelFormat}' is not supported by the CPU compositor.");

        var clippedRegion = RenderMath.Intersect(region, RenderMath.Bounds(target.Size));
        if (clippedRegion.IsEmpty) return;

        target.Clear(clippedRegion);

        var celsByLayer = context.Snapshot.Cels
            .Where(cel => cel.FrameId == context.FrameId)
            .ToDictionary(cel => cel.LayerId);

        foreach (var layerId in context.Snapshot.LayerOrder)
        {
            var layer = context.Snapshot.GetLayer(layerId);
            if (!layer.Visible || layer.Opacity == 0) continue;
            if (layer.Kind != LayerSnapshotKind.Pixel)
                throw new NotSupportedException($"Layer snapshot kind '{layer.Kind}' has no CPU compositor implementation.");
            if (!celsByLayer.TryGetValue(layerId, out var cel) || cel.Opacity == 0) continue;

            var surface = context.Snapshot.GetSurface(cel.SurfaceId);
            if (surface.Format != PixelFormat.Rgba32)
                throw new NotSupportedException($"Surface pixel format '{surface.Format}' is not supported by the CPU compositor.");

            CompositeCel(surface, cel, layer, target, clippedRegion);
        }
    }

    private static void CompositeCel(
        PixelSurfaceSnapshot surface,
        CelSnapshot cel,
        LayerSnapshot layer,
        IRenderTarget target,
        IntRect region)
    {
        var celBounds = new IntRect(cel.Position.X, cel.Position.Y, surface.Size.Width, surface.Size.Height);
        var drawRegion = RenderMath.Intersect(region, celBounds);
        if (drawRegion.IsEmpty) return;

        for (var canvasY = drawRegion.Y; canvasY < drawRegion.Bottom; canvasY++)
        for (var canvasX = drawRegion.X; canvasX < drawRegion.Right; canvasX++)
        {
            var sourceX = canvasX - cel.Position.X;
            var sourceY = canvasY - cel.Position.Y;
            var source = surface.GetPixel(sourceX, sourceY);
            if (source.A == 0) continue;

            var effectiveAlpha = RenderMath.ScaleByte(source.A, cel.Opacity);
            effectiveAlpha = RenderMath.ScaleByte(effectiveAlpha, layer.Opacity);
            if (effectiveAlpha == 0) continue;

            var effectiveSource = new Rgba32(source.R, source.G, source.B, effectiveAlpha);
            var destination = target.GetPixel(canvasX, canvasY);
            target.SetPixel(canvasX, canvasY, RenderMath.SourceOver(destination, effectiveSource));
        }
    }
}
