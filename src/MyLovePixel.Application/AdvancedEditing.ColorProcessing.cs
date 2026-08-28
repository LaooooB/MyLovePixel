using MyLovePixel.Color;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Commands.Resources;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Application;

public static partial class AdvancedEditingExtensions
{
    public static PaletteId QuantizeCurrentSurface(this DocumentSession session, int maxColors, bool reserveTransparentIndex)
    {
        ArgumentNullException.ThrowIfNull(session);
        var (cel, surface) = ResolveColorSurface(session);
        if (surface.Format != PixelFormat.Rgba32)
            throw new InvalidOperationException("Quantize requires an RGBA32 Cel.");

        var quantized = MedianCutQuantizationStrategy.Instance.Quantize(surface, maxColors, reserveTransparentIndex);
        var addPalette = new AddPaletteCommand(quantized.Colors, quantized.TransparentIndex);
        using var transaction = session.Commands.BeginTransaction("Quantize Surface");
        try
        {
            session.Execute(addPalette);
            session.Execute(new ReplacePixelSurfaceCommand(
                cel.SurfaceId,
                PixelFormat.Indexed8,
                addPalette.PaletteId,
                quantized.Indices,
                "Quantize Surface"));
            transaction.Commit();
            return addPalette.PaletteId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public static void DitherCurrentSurface(this DocumentSession session, PaletteId paletteId, bool bayer4x4, int strength)
    {
        ArgumentNullException.ThrowIfNull(session);
        var (cel, surface) = ResolveColorSurface(session);
        if (surface.Format != PixelFormat.Rgba32)
            throw new InvalidOperationException("Dither requires an RGBA32 Cel.");
        var snapshot = session.CaptureSnapshot();
        var palette = snapshot.GetPalette(paletteId);
        var matrix = bayer4x4 ? OrderedDitherMatrix.Bayer4x4 : OrderedDitherMatrix.Bayer2x2;
        var dithered = OrderedPaletteDitherStrategy.Instance.Dither(surface, palette, new DitherOptions(matrix, strength));
        session.Execute(new ReplacePixelSurfaceCommand(
            cel.SurfaceId,
            PixelFormat.Indexed8,
            paletteId,
            dithered.Indices,
            "Dither Surface"));
    }

    public static void ShadeCurrentIndexedSurface(this DocumentSession session, IReadOnlyList<byte> rampIndices, int stepDelta)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(rampIndices);
        var (cel, surface) = ResolveColorSurface(session);
        if (surface.Format != PixelFormat.Indexed8 || surface.PaletteId is not { } paletteId)
            throw new InvalidOperationException("Ramp shading requires an Indexed8 Cel.");

        var palette = session.CaptureSnapshot().GetPalette(paletteId);
        foreach (var index in rampIndices)
            if (index >= palette.Count)
                throw new ArgumentOutOfRangeException(nameof(rampIndices), $"Palette index {index} is outside the current palette.");

        var ink = new ColorRampShadingInk(new ColorRamp(rampIndices), stepDelta);
        var transformed = surface.Bytes.ToArray();
        for (var i = 0; i < transformed.Length; i++) transformed[i] = ink.Apply(transformed[i]);
        session.Execute(new ReplacePixelSurfaceCommand(
            cel.SurfaceId,
            PixelFormat.Indexed8,
            paletteId,
            transformed,
            stepDelta >= 0 ? "Shade Indexed Surface" : "Lighten Indexed Surface"));
    }

    private static (Core.Document.CelSnapshot Cel, PixelSurfaceSnapshot Surface) ResolveColorSurface(DocumentSession session)
    {
        var snapshot = session.CaptureSnapshot();
        var cel = snapshot.Cels.FirstOrDefault(value => value.LayerId == session.CurrentLayerId && value.FrameId == session.CurrentFrameId)
            ?? throw new InvalidOperationException("Current Layer/Frame has no Cel.");
        return (cel, snapshot.GetSurface(cel.SurfaceId));
    }
}
