using MyLovePixel.Commands.Pixel;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Application;

public static partial class AdvancedEditingExtensions
{
    public static void ClearCurrentCanvas(this DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        session.CancelToolInteraction();
        var frameId = session.CurrentFrameId;
        var initial = session.CaptureSnapshot();
        var celIds = initial.Cels
            .Where(value => value.FrameId == frameId)
            .Select(value => value.Id)
            .ToArray();
        if (celIds.Length == 0) return;

        using var transaction = session.Commands.BeginTransaction("Clear Canvas");
        try
        {
            var clearedSurfaces = new HashSet<ResourceId>();
            foreach (var celId in celIds)
            {
                var snapshot = session.CaptureSnapshot();
                var cel = snapshot.Cels.First(value => value.Id == celId);

                // Linked frame copies share the same pixel surface. Detach the current
                // frame before clearing so this destructive action never wipes pixels
                // from another frame as a side effect.
                var sharedWithAnotherFrame = snapshot.Cels.Any(value =>
                    value.Id != cel.Id &&
                    value.FrameId != frameId &&
                    value.SurfaceId == cel.SurfaceId);
                if (sharedWithAnotherFrame)
                {
                    session.Execute(new UnlinkCelCommand(cel.Id));
                    snapshot = session.CaptureSnapshot();
                    cel = snapshot.Cels.First(value => value.Id == celId);
                }

                if (!clearedSurfaces.Add(cel.SurfaceId)) continue;
                ClearSurface(session, snapshot, cel);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void ClearSurface(DocumentSession session, DocumentSnapshot snapshot, CelSnapshot cel)
    {
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
            // Indexed pixels without a transparent entry cannot represent an empty
            // surface, so this Cel is converted to transparent RGBA when cleared.
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
