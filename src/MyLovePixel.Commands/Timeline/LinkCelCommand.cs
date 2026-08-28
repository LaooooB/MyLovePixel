using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Commands.Timeline;

public sealed class LinkCelCommand : ICommand
{
    private readonly CelId _celId;
    private readonly CelId _sourceCelId;

    public LinkCelCommand(CelId celId, CelId sourceCelId)
    {
        if (celId == sourceCelId) throw new ArgumentException("A Cel cannot be linked to itself.", nameof(sourceCelId));
        _celId = celId;
        _sourceCelId = sourceCelId;
    }

    public string Name => "Link Cel";

    public CommandApplication Apply(PixelDocument document)
    {
        var cel = document.GetCel(_celId);
        var source = document.GetCel(_sourceCelId);
        if (cel.SurfaceId == source.SurfaceId)
            throw new InvalidOperationException("The Cels are already linked to the same Surface.");

        var oldSurfaceId = cel.SurfaceId;
        cel.SurfaceId = source.SurfaceId;

        PixelSurface? removedSurface = null;
        if (!document.IsSurfaceReferenced(oldSurfaceId))
            removedSurface = document.Resources.RemoveSurface(oldSurfaceId);

        return new CommandApplication(
            new Undo(oldSurfaceId, removedSurface),
            DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        if (undo.RemovedSurface is not null)
            document.Resources.AddSurface(undo.OldSurfaceId, undo.RemovedSurface);
        document.GetCel(_celId).SurfaceId = undo.OldSurfaceId;
        return DocumentChange.Empty;
    }

    private sealed record Undo(ResourceId OldSurfaceId, PixelSurface? RemovedSurface) : IUndoToken;
}
