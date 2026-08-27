using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Commands.Timeline;

public sealed class UnlinkCelCommand : ICommand
{
    private readonly CelId _celId;
    private readonly ResourceId _newSurfaceId = ResourceId.New();

    public UnlinkCelCommand(CelId celId) => _celId = celId;

    public string Name => "Unlink Cel";

    public CommandApplication Apply(PixelDocument document)
    {
        var cel = document.GetCel(_celId);
        var oldSurfaceId = cel.SurfaceId;
        var clone = document.Resources.GetSurface(oldSurfaceId).Clone();
        document.Resources.AddSurface(_newSurfaceId, clone);
        cel.SurfaceId = _newSurfaceId;
        return new CommandApplication(new Undo(oldSurfaceId), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        var cel = document.GetCel(_celId);
        cel.SurfaceId = undo.OldSurfaceId;
        document.Resources.RemoveSurface(_newSurfaceId);
        return DocumentChange.Empty;
    }

    private sealed record Undo(ResourceId OldSurfaceId) : IUndoToken;
}
