using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Application;

public static partial class AdvancedEditingExtensions
{
    public static void ClearCurrentCanvas(this DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        session.CancelToolInteraction();
        session.EnsureEditableCel();

        var snapshot = session.CaptureSnapshot();
        var cel = snapshot.Cels.FirstOrDefault(value =>
            value.LayerId == session.CurrentLayerId && value.FrameId == session.CurrentFrameId)
            ?? throw new InvalidOperationException("Current Layer/Frame has no editable Cel.");
        var surface = snapshot.GetSurface(cel.SurfaceId);

        PixelFormat format;
        PaletteId? paletteId;
        ReadOnlyMemory<byte> bytes;

        if (surface.Format == PixelFormat.Rgba32)
        {
            format = PixelFormat.Rgba32;
            paletteId = null;
            bytes = new byte[checked(surface.Size.Width * surface.Size.Height * 4)];
        }
        else if (surface.PaletteId is { } indexedPaletteId &&
                 snapshot.GetPalette(indexedPaletteId).TransparentIndex is { } transparentIndex)
        {
            format = PixelFormat.Indexed8;
            paletteId = indexedPaletteId;
            var indices = new byte[checked(surface.Size.Width * surface.Size.Height)];
            Array.Fill(indices, transparentIndex);
            bytes = indices;
        }
        else
        {
            // An indexed surface without a transparent palette entry cannot represent
            // an empty canvas. Convert that one Cel to transparent RGBA instead.
            format = PixelFormat.Rgba32;
            paletteId = null;
            bytes = new byte[checked(surface.Size.Width * surface.Size.Height * 4)];
        }

        session.Execute(new ReplacePixelSurfaceCommand(
            cel.SurfaceId,
            format,
            paletteId,
            bytes,
            "Clear Canvas"));
    }
}
