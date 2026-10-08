using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Render;

namespace MyLovePixel.Application;

public sealed record PixelSample(Rgba32 Color, PaletteId? PaletteId = null, byte? PaletteIndex = null)
{
    public static PixelSample? FromRgba(IntSize size, ReadOnlyMemory<byte> rgba, int x, int y)
    {
        if ((uint)x >= (uint)size.Width || (uint)y >= (uint)size.Height) return null;
        var p = checked((y * size.Width + x) * 4);
        var data = rgba.Span;
        return new PixelSample(new Rgba32(data[p], data[p + 1], data[p + 2], data[p + 3]));
    }
}

public sealed partial class DocumentSession
{
    public long DocumentVersion { get; private set; }
    public PixelSample? PrimarySample { get; private set; }
    public PixelSample? SecondarySample { get; private set; }

    public string? DrawingBlockedReason
    {
        get
        {
            var snapshot = CaptureSnapshot();
            if (!snapshot.Layers.TryGetValue(CurrentLayerId, out var layer)) return "Select a layer to draw.";
            if (layer.Locked) return "This layer is locked. Unlock it to draw.";
            if (!layer.Visible) return "This layer is hidden. Show it to draw.";
            var cel = snapshot.Cels.FirstOrDefault(c => c.FrameId == CurrentFrameId && c.LayerId == CurrentLayerId);
            if (cel is not null && snapshot.GetSurface(cel.SurfaceId).Format != PixelFormat.Rgba32)
                return "Indexed layer. Convert it to RGBA before using a drawing tool.";
            return null;
        }
    }

    /// <summary>Reads document pixels, never overlays, preview strokes or UI backgrounds.</summary>
    public PixelSample? SampleCanvasPixel(int x, int y, bool currentLayerOnly)
    {
        var snapshot = CaptureSnapshot();
        if ((uint)x >= (uint)snapshot.Canvas.Size.Width || (uint)y >= (uint)snapshot.Canvas.Size.Height) return null;
        if (!currentLayerOnly)
        {
            var rendered = _renderer.Render(snapshot, new FrameRenderRequest(CurrentFrameId)).Surface;
            return PixelSample.FromRgba(rendered.Size, rendered.Bytes, x, y);
        }

        var cel = snapshot.Cels.FirstOrDefault(c => c.LayerId == CurrentLayerId && c.FrameId == CurrentFrameId);
        if (cel is null) return new PixelSample(Rgba32.Transparent);
        var localX = x - cel.Position.X;
        var localY = y - cel.Position.Y;
        var surface = snapshot.GetSurface(cel.SurfaceId);
        if ((uint)localX >= (uint)surface.Size.Width || (uint)localY >= (uint)surface.Size.Height)
            return new PixelSample(Rgba32.Transparent);
        if (surface.Format == PixelFormat.Indexed8 && surface.PaletteId is { } paletteId)
        {
            var index = surface.Bytes.Span[localY * surface.Size.Width + localX];
            return new PixelSample(snapshot.GetPalette(paletteId).ResolveColor(index), paletteId, index);
        }
        return new PixelSample(surface.GetPixel(localX, localY));
    }

    public void ApplySampledColor(PixelSample sample, bool secondary = false)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var snapshot = CaptureSnapshot();
        var cel = snapshot.Cels.FirstOrDefault(c => c.LayerId == CurrentLayerId && c.FrameId == CurrentFrameId);
        if (cel is not null)
        {
            var surface = snapshot.GetSurface(cel.SurfaceId);
            if (surface.Format == PixelFormat.Indexed8 && surface.PaletteId is { } targetId)
            {
                var palette = snapshot.GetPalette(targetId);
                byte? exact = sample.PaletteId == targetId && sample.PaletteIndex is { } i && i < palette.Count
                    && palette.ResolveColor(i) == sample.Color ? i : null;
                if (exact is null)
                    for (var candidate = 0; candidate < palette.Count; candidate++)
                        if (palette.ResolveColor((byte)candidate) == sample.Color) { exact = (byte)candidate; break; }
                if (exact is null) throw new InvalidOperationException("The sampled color is not in this layer's palette. Sample the current layer or choose a palette color.");
                sample = new PixelSample(sample.Color, targetId, exact);
            }
        }
        if (secondary) SecondarySample = sample;
        else PrimarySample = sample;
        SetToolColors(secondary ? _primaryColor : sample.Color, secondary ? sample.Color : _secondaryColor);
    }
}

public sealed partial class PluginWorkspaceRuntime
{
    public PixelSample? SampleCanvasPixel(DocumentSession session, int x, int y, bool currentLayerOnly)
    {
        EnsureOwned(session);
        if (currentLayerOnly) return session.SampleCanvasPixel(x, y, true);
        var snapshot = session.CaptureSnapshot();
        if ((uint)x >= (uint)snapshot.Canvas.Size.Width || (uint)y >= (uint)snapshot.Canvas.Size.Height) return null;
        // Use the exact renderer used by the canvas and exporters, without onion skin or transient preview.
        var result = _pluginRenderer.Render(snapshot, new FrameRenderRequest(session.CurrentFrameId)).Surface;
        return PixelSample.FromRgba(result.Size, result.Bytes, x, y);
    }

    public CanvasPresentation RenderFramePreview(DocumentSession session, FrameId frameId)
    {
        EnsureOwned(session);
        var result = _pluginRenderer.Render(session.CaptureSnapshot(), new FrameRenderRequest(frameId)).Surface;
        return new CanvasPresentation(frameId, result.Size, result.Bytes);
    }
}
