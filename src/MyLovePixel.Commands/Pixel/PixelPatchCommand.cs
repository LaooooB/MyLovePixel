using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Commands.Pixel;

public sealed class PixelPatchCommand : ICommand
{
    private readonly ResourceId _surfaceId;
    private readonly PixelWrite[] _writes;
    private readonly IntRect _dirtyRegion;

    public PixelPatchCommand(ResourceId surfaceId, IEnumerable<PixelWrite> writes, string name = "Pixel Patch")
    {
        _surfaceId = surfaceId;
        Name = name;
        ArgumentNullException.ThrowIfNull(writes);

        // Last write wins per coordinate. This prevents repeated samples in one patch
        // from bloating undo state and makes command semantics deterministic.
        _writes = writes
            .GroupBy(x => (x.X, x.Y))
            .Select(group => group.Last())
            .ToArray();

        if (_writes.Length == 0)
            throw new ArgumentException("Pixel patch must contain at least one write.", nameof(writes));

        _dirtyRegion = CalculateBounds(_writes);
    }

    public string Name { get; }

    public CommandApplication Apply(PixelDocument document)
    {
        var surface = document.Resources.GetSurface(_surfaceId);
        var before = new PixelWrite[_writes.Length];
        for (var i = 0; i < _writes.Length; i++)
        {
            var write = _writes[i];
            before[i] = new PixelWrite(write.X, write.Y, surface.GetPixel(write.X, write.Y));
        }

        surface.SetPixels(_writes);
        return new CommandApplication(new Undo(before), DocumentChange.ForSurface(_surfaceId, _dirtyRegion));
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.Resources.GetSurface(_surfaceId).SetPixels(undo.Before);
        return DocumentChange.ForSurface(_surfaceId, _dirtyRegion);
    }

    private static IntRect CalculateBounds(IReadOnlyList<PixelWrite> writes)
    {
        if (writes.Count == 0) return default;
        var minX = writes[0].X;
        var minY = writes[0].Y;
        var maxX = minX;
        var maxY = minY;
        for (var i = 1; i < writes.Count; i++)
        {
            minX = Math.Min(minX, writes[i].X);
            minY = Math.Min(minY, writes[i].Y);
            maxX = Math.Max(maxX, writes[i].X);
            maxY = Math.Max(maxY, writes[i].Y);
        }
        return new IntRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private sealed record Undo(PixelWrite[] Before) : IUndoToken;
}
