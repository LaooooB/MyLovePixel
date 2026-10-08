using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Application;

public sealed record PixelSample(Rgba32 Color, PaletteId? PaletteId = null, byte? Index = null);

/// <summary>Reads artwork bytes only: checkerboards, grids, hover and tool previews are excluded.</summary>
public static class PixelSampling
{
    public static PixelSample? ReadPixel(DocumentSession session, int x, int y, bool currentLayer = false,
        CanvasPresentation? composite = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        var snapshot = session.CaptureSnapshot();
        var size = snapshot.Canvas.Size;
        if ((uint)x >= (uint)size.Width || (uint)y >= (uint)size.Height) return null;
        if (!currentLayer)
        {
            composite ??= session.RenderCanvas();
            if (composite.FrameId != session.CurrentFrameId || composite.Size != size)
                throw new InvalidOperationException("The sampled frame changed. Pick the color again.");
            var bytes = composite.Rgba.Span;
            var offset = checked((y * size.Width + x) * 4);
            return new(new(bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]));
        }
        var cel = snapshot.Cels.FirstOrDefault(c => c.LayerId == session.CurrentLayerId && c.FrameId == session.CurrentFrameId);
        if (cel is null) return new(Rgba32.Transparent);
        var surface = snapshot.GetSurface(cel.SurfaceId);
        var localX = (long)x - cel.Position.X;
        var localY = (long)y - cel.Position.Y;
        if (localX < 0 || localY < 0 || localX >= surface.Size.Width || localY >= surface.Size.Height)
            return new(Rgba32.Transparent);
        if (surface.Format == PixelFormat.Rgba32) return new(surface.GetPixel((int)localX, (int)localY));
        var index = surface.GetIndex((int)localX, (int)localY);
        var palette = snapshot.GetPalette(surface.PaletteId!.Value);
        return new(EffectiveColor(palette, index), surface.PaletteId, index);
    }

    public static PixelSample ApplySample(DocumentSession session, PixelSample sample, bool secondary)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sample);
        var snapshot = session.CaptureSnapshot();
        var cel = snapshot.Cels.FirstOrDefault(c => c.LayerId == session.CurrentLayerId && c.FrameId == session.CurrentFrameId);
        if (cel is not null && snapshot.GetSurface(cel.SurfaceId).PaletteId is { } paletteId)
        {
            var palette = snapshot.GetPalette(paletteId);
            byte? index = null;
            if (sample.PaletteId == paletteId && sample.Index is { } original && original < palette.Count &&
                EffectiveColor(palette, original) == sample.Color) index = original;
            else
                for (var i = 0; i < palette.Count; i++)
                    if (EffectiveColor(palette, (byte)i) == sample.Color) { index = (byte)i; break; }
            if (index is null) throw new InvalidOperationException("This color is not in the indexed layer palette. Sample the current layer instead.");
            sample = new(sample.Color, paletteId, index);
        }
        var colors = session.GetToolColors();
        session.SetToolColors(secondary ? colors.Primary : sample.Color, secondary ? sample.Color : colors.Secondary);
        return sample;
    }

    private static Rgba32 EffectiveColor(PaletteSnapshot palette, byte index)
    {
        var color = palette.GetColor(index);
        return palette.TransparentIndex == index ? new(color.R, color.G, color.B, 0) : color;
    }
}
