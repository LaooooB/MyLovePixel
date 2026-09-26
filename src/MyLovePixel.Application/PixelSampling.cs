using System.Runtime.CompilerServices;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Application;

public sealed record PixelSample(Rgba32 Color, PaletteId? PaletteId = null, byte? Index = null);

/// <summary>Samples artwork data, never canvas decorations or pending tool previews.</summary>
public static class PixelSampling
{
    private sealed class SampleState
    {
        public PixelSample? Primary;
        public PixelSample? Secondary;
    }

    private static readonly ConditionalWeakTable<DocumentSession, SampleState> States = new();

    public static PixelSample? ReadPixel(DocumentSession session, int x, int y, bool currentLayer,
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
            return new PixelSample(new Rgba32(bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]));
        }

        var cel = snapshot.Cels.FirstOrDefault(c => c.LayerId == session.CurrentLayerId && c.FrameId == session.CurrentFrameId);
        if (cel is null) return new PixelSample(Rgba32.Transparent);
        var surface = snapshot.GetSurface(cel.SurfaceId);
        var localX = (long)x - cel.Position.X;
        var localY = (long)y - cel.Position.Y;
        if (localX < 0 || localY < 0 || localX >= surface.Size.Width || localY >= surface.Size.Height)
            return new PixelSample(Rgba32.Transparent);
        if (surface.Format == PixelFormat.Rgba32)
            return new PixelSample(surface.GetPixel((int)localX, (int)localY));

        var index = surface.GetIndex((int)localX, (int)localY);
        var palette = snapshot.GetPalette(surface.PaletteId!.Value);
        var color = palette.GetColor(index);
        if (palette.TransparentIndex == index) color = new Rgba32(color.R, color.G, color.B, 0);
        return new PixelSample(color, surface.PaletteId, index);
    }

    public static PixelSample? GetSelectedSample(DocumentSession session, bool secondary)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!States.TryGetValue(session, out var state)) return null;
        var sample = secondary ? state.Secondary : state.Primary;
        var colors = session.GetToolColors();
        return sample?.Color == (secondary ? colors.Secondary : colors.Primary) ? sample : null;
    }

    /// <summary>Changes the tool color only. Unrepresentable indexed colors are rejected, never approximated.</summary>
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
                EffectiveColor(palette, original) == sample.Color)
                index = original;
            else
                for (var i = 0; i < palette.Count; i++)
                    if (EffectiveColor(palette, (byte)i) == sample.Color)
                    { index = (byte)i; break; }
            if (index is null)
                throw new InvalidOperationException("This color is not in the layer palette. Sample the current layer or choose a palette color.");
            sample = new PixelSample(sample.Color, paletteId, index);
        }
        var colors = session.GetToolColors();
        session.SetToolColors(secondary ? colors.Primary : sample.Color, secondary ? sample.Color : colors.Secondary);
        var state = States.GetOrCreateValue(session);
        if (secondary) state.Secondary = sample; else state.Primary = sample;
        return sample;
    }

    private static Rgba32 EffectiveColor(PaletteSnapshot palette, byte index)
    {
        var color = palette.GetColor(index);
        return palette.TransparentIndex == index ? new Rgba32(color.R, color.G, color.B, 0) : color;
    }

    public static string? EditingRestriction(this DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var snapshot = session.CaptureSnapshot();
        var layer = snapshot.GetLayer(session.CurrentLayerId);
        if (layer.Locked) return "Layer is locked. Unlock it to edit.";
        if (!layer.Visible) return "Layer is hidden. Show it to edit.";
        if (layer.Opacity == 0) return "Layer opacity is 0%. Increase it to see your drawing.";
        var cel = snapshot.Cels.FirstOrDefault(c => c.LayerId == session.CurrentLayerId && c.FrameId == session.CurrentFrameId);
        if (cel is not null && snapshot.GetSurface(cel.SurfaceId).Format != PixelFormat.Rgba32)
            return "Indexed layer. Convert to RGBA to use drawing tools.";
        return null;
    }

    public static void RequireWritableLayer(this DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var layer = session.CaptureSnapshot().GetLayer(session.CurrentLayerId);
        if (layer.Locked) throw new InvalidOperationException("Layer is locked. Unlock it to edit.");
        if (!layer.Visible) throw new InvalidOperationException("Layer is hidden. Show it to edit.");
    }
}
