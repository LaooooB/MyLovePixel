using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Effects;

namespace MyLovePixel.Render;

public sealed class FrameCompositeRenderNode : IRenderNode
{
    public FrameCompositeRenderNode(EffectEngine? effects = null)
    {
        Effects = effects ?? EffectEngine.CreateDefault();
    }

    public string Id => "core.frame-composite";
    public long Revision => Effects.ConfigurationRevision;
    public EffectEngine Effects { get; }

    public void Execute(RenderNodeContext context, IRenderTarget target, IntRect region)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(target);

        if (target.Size != context.Snapshot.Canvas.Size)
            throw new ArgumentException("Render target size must match the document canvas.", nameof(target));
        if (context.Snapshot.Canvas.PixelFormat != PixelFormat.Rgba32)
            throw new NotSupportedException($"Canvas compositing format '{context.Snapshot.Canvas.PixelFormat}' is not supported by the CPU compositor.");

        var clippedRegion = RenderMath.Intersect(region, RenderMath.Bounds(target.Size));
        if (clippedRegion.IsEmpty) return;

        target.Clear(clippedRegion);

        var celsByLayer = context.Snapshot.Cels
            .Where(cel => cel.FrameId == context.FrameId)
            .ToDictionary(cel => cel.LayerId);
        context.Snapshot.Animation.ColorCycleTrack.Values.TryGetValue(context.FrameId, out var colorCycles);

        foreach (var layerId in context.Snapshot.LayerOrder)
        {
            var layer = context.Snapshot.GetLayer(layerId);
            if (!layer.Visible || layer.Opacity == 0) continue;
            if (layer.Kind != LayerSnapshotKind.Pixel)
                throw new NotSupportedException($"Layer snapshot kind '{layer.Kind}' has no CPU compositor implementation.");
            if (!celsByLayer.TryGetValue(layerId, out var cel) || cel.Opacity == 0) continue;

            if (cel.Effects.EffectOrder.Count != 0)
            {
                var evaluated = Effects.EvaluateCel(context.Snapshot, context.FrameId, cel);
                CompositeEffectImage(evaluated.Image, cel, layer, target, clippedRegion);
                continue;
            }

            var surface = context.Snapshot.GetSurface(cel.SurfaceId);
            CompositeCel(context.Snapshot, surface, cel, layer, colorCycles, target, clippedRegion);
        }
    }

    private static void CompositeEffectImage(
        EffectImage image,
        CelSnapshot cel,
        LayerSnapshot layer,
        IRenderTarget target,
        IntRect region)
    {
        var origin = new IntPoint(
            checked(cel.Position.X + image.Origin.X),
            checked(cel.Position.Y + image.Origin.Y));
        var bounds = new IntRect(origin.X, origin.Y, image.Size.Width, image.Size.Height);
        var drawRegion = RenderMath.Intersect(region, bounds);
        if (drawRegion.IsEmpty) return;

        for (var canvasY = drawRegion.Y; canvasY < drawRegion.Bottom; canvasY++)
        for (var canvasX = drawRegion.X; canvasX < drawRegion.Right; canvasX++)
        {
            var source = image.GetPixel(canvasX - origin.X, canvasY - origin.Y);
            CompositeSourcePixel(source, cel.Opacity, layer.Opacity, target, canvasX, canvasY);
        }
    }

    private static void CompositeCel(
        DocumentSnapshot snapshot,
        PixelSurfaceSnapshot surface,
        CelSnapshot cel,
        LayerSnapshot layer,
        ColorCycleFrameValue? colorCycles,
        IRenderTarget target,
        IntRect region)
    {
        var celBounds = new IntRect(cel.Position.X, cel.Position.Y, surface.Size.Width, surface.Size.Height);
        var drawRegion = RenderMath.Intersect(region, celBounds);
        if (drawRegion.IsEmpty) return;

        PaletteSnapshot? palette = null;
        PaletteId? paletteId = null;
        if (surface.Format == PixelFormat.Indexed8)
        {
            if (surface.PaletteId is not { } indexedPaletteId)
                throw new InvalidOperationException($"Indexed8 surface '{cel.SurfaceId}' has no palette reference.");
            paletteId = indexedPaletteId;
            palette = snapshot.GetPalette(indexedPaletteId);
        }

        for (var canvasY = drawRegion.Y; canvasY < drawRegion.Bottom; canvasY++)
        for (var canvasX = drawRegion.X; canvasX < drawRegion.Right; canvasX++)
        {
            var sourceX = canvasX - cel.Position.X;
            var sourceY = canvasY - cel.Position.Y;
            var source = surface.Format switch
            {
                PixelFormat.Rgba32 => surface.GetPixel(sourceX, sourceY),
                PixelFormat.Indexed8 => ResolveIndexed(surface, sourceX, sourceY, paletteId!.Value, palette!, colorCycles),
                _ => throw new NotSupportedException($"Surface pixel format '{surface.Format}' has no CPU compositor implementation."),
            };
            CompositeSourcePixel(source, cel.Opacity, layer.Opacity, target, canvasX, canvasY);
        }
    }

    private static void CompositeSourcePixel(
        Rgba32 source,
        byte celOpacity,
        byte layerOpacity,
        IRenderTarget target,
        int canvasX,
        int canvasY)
    {
        if (source.A == 0) return;
        var effectiveAlpha = RenderMath.ScaleByte(source.A, celOpacity);
        effectiveAlpha = RenderMath.ScaleByte(effectiveAlpha, layerOpacity);
        if (effectiveAlpha == 0) return;
        var effectiveSource = new Rgba32(source.R, source.G, source.B, effectiveAlpha);
        var destination = target.GetPixel(canvasX, canvasY);
        target.SetPixel(canvasX, canvasY, RenderMath.SourceOver(destination, effectiveSource));
    }

    private static Rgba32 ResolveIndexed(
        PixelSurfaceSnapshot surface,
        int x,
        int y,
        PaletteId paletteId,
        PaletteSnapshot palette,
        ColorCycleFrameValue? colorCycles)
    {
        var index = surface.GetIndex(x, y);
        var resolvedIndex = colorCycles?.ResolveIndex(paletteId, index) ?? index;
        return palette.ResolveColor(resolvedIndex);
    }
}
