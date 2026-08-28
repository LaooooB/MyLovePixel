using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Commands.Color;

public sealed class IndexedPixelPatchCommand : ICommand
{
    private readonly ResourceId _surfaceId;
    private readonly IndexedPixelWrite[] _writes;
    private readonly IntRect _dirtyRegion;

    public IndexedPixelPatchCommand(
        ResourceId surfaceId,
        IEnumerable<IndexedPixelWrite> writes,
        string name = "Indexed Pixel Patch")
    {
        _surfaceId = surfaceId;
        Name = name;
        ArgumentNullException.ThrowIfNull(writes);
        _writes = writes
            .GroupBy(write => (write.X, write.Y))
            .Select(group => group.Last())
            .ToArray();
        if (_writes.Length == 0)
            throw new ArgumentException("Indexed pixel patch must contain at least one write.", nameof(writes));
        _dirtyRegion = CalculateBounds(_writes);
    }

    public string Name { get; }

    public CommandApplication Apply(PixelDocument document)
    {
        var surface = document.Resources.GetSurface(_surfaceId);
        if (surface.Format != PixelFormat.Indexed8 || surface.PaletteId is not { } paletteId)
            throw new InvalidOperationException($"Surface '{_surfaceId}' is not an Indexed8 surface with a palette.");
        var palette = document.Resources.GetPalette(paletteId);
        var before = new IndexedPixelWrite[_writes.Length];

        for (var index = 0; index < _writes.Length; index++)
        {
            var write = _writes[index];
            if (write.Index >= palette.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(_writes),
                    $"Palette index {write.Index} is outside palette '{paletteId}' with {palette.Count} entries.");
            before[index] = new IndexedPixelWrite(write.X, write.Y, surface.GetIndex(write.X, write.Y));
        }

        surface.SetIndices(_writes);
        return new CommandApplication(new Undo(before), DocumentChange.ForSurface(_surfaceId, _dirtyRegion));
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo)
            throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.Resources.GetSurface(_surfaceId).SetIndices(undo.Before);
        return DocumentChange.ForSurface(_surfaceId, _dirtyRegion);
    }

    private static IntRect CalculateBounds(IReadOnlyList<IndexedPixelWrite> writes)
    {
        var minX = writes[0].X;
        var minY = writes[0].Y;
        var maxX = minX;
        var maxY = minY;
        for (var index = 1; index < writes.Count; index++)
        {
            minX = Math.Min(minX, writes[index].X);
            minY = Math.Min(minY, writes[index].Y);
            maxX = Math.Max(maxX, writes[index].X);
            maxY = Math.Max(maxY, writes[index].Y);
        }
        return new IntRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private sealed record Undo(IndexedPixelWrite[] Before) : IUndoToken;
}

public sealed class SetPaletteColorCommand(
    PaletteId paletteId,
    byte index,
    Rgba32 color) : ICommand
{
    public string Name => "Set Palette Color";

    public CommandApplication Apply(PixelDocument document)
    {
        var palette = document.Resources.GetPalette(paletteId);
        var previous = palette.GetColor(index);
        palette.SetColor(index, color);
        return new CommandApplication(new Undo(previous), PaletteCommandHelper.FullDirty(document, paletteId));
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo)
            throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.Resources.GetPalette(paletteId).SetColor(index, undo.Previous);
        return PaletteCommandHelper.FullDirty(document, paletteId);
    }

    private sealed record Undo(Rgba32 Previous) : IUndoToken;
}

public sealed class ReorderPaletteCommand : ICommand
{
    private readonly PaletteId _paletteId;
    private readonly byte[] _newOrderOldIndices;

    public ReorderPaletteCommand(PaletteId paletteId, IEnumerable<byte> newOrderOldIndices)
    {
        _paletteId = paletteId;
        ArgumentNullException.ThrowIfNull(newOrderOldIndices);
        _newOrderOldIndices = newOrderOldIndices.ToArray();
    }

    public string Name => "Reorder Palette";

    public CommandApplication Apply(PixelDocument document)
    {
        var palette = document.Resources.GetPalette(_paletteId);
        ValidatePermutation(palette.Count);
        var referenced = PaletteCommandHelper.GetReferencedIndexedSurfaces(document, _paletteId);
        PrevalidateRevisions(palette, referenced);

        var oldPalette = palette.Snapshot();
        var oldToNew = new byte[palette.Count];
        var newColors = new Rgba32[palette.Count];
        for (var newIndex = 0; newIndex < _newOrderOldIndices.Length; newIndex++)
        {
            var oldIndex = _newOrderOldIndices[newIndex];
            newColors[newIndex] = oldPalette.GetColor(oldIndex);
            oldToNew[oldIndex] = checked((byte)newIndex);
        }

        var newTransparentIndex = oldPalette.TransparentIndex is { } transparentIndex
            ? oldToNew[transparentIndex]
            : null;
        var surfaceStates = new SurfaceState[referenced.Count];
        for (var index = 0; index < referenced.Count; index++)
        {
            var pair = referenced[index];
            var before = pair.Surface.Snapshot().Bytes.ToArray();
            var after = new byte[before.Length];
            for (var pixel = 0; pixel < before.Length; pixel++)
            {
                var oldIndex = before[pixel];
                if (oldIndex >= palette.Count)
                    throw new InvalidOperationException(
                        $"Indexed8 surface '{pair.Id}' contains invalid palette index {oldIndex}.");
                after[pixel] = oldToNew[oldIndex];
            }
            surfaceStates[index] = new SurfaceState(pair.Id, before, after);
        }

        palette.ReplaceState(newColors, newTransparentIndex);
        foreach (var state in surfaceStates)
            document.Resources.GetSurface(state.SurfaceId).ReplaceIndices(state.After);

        return new CommandApplication(
            new Undo(oldPalette.Colors.ToArray(), oldPalette.TransparentIndex, surfaceStates),
            PaletteCommandHelper.FullDirty(document, _paletteId));
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo)
            throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        var palette = document.Resources.GetPalette(_paletteId);
        var referenced = undo.Surfaces
            .Select(state => (state.SurfaceId, document.Resources.GetSurface(state.SurfaceId)))
            .ToArray();
        PrevalidateRevisions(palette, referenced);

        palette.ReplaceState(undo.Colors, undo.TransparentIndex);
        foreach (var state in undo.Surfaces)
            document.Resources.GetSurface(state.SurfaceId).ReplaceIndices(state.Before);
        return PaletteCommandHelper.FullDirty(document, _paletteId);
    }

    private void ValidatePermutation(int paletteCount)
    {
        if (_newOrderOldIndices.Length != paletteCount)
            throw new ArgumentException(
                $"Palette reorder must contain exactly {paletteCount} entries.",
                nameof(_newOrderOldIndices));
        var seen = new bool[paletteCount];
        foreach (var oldIndex in _newOrderOldIndices)
        {
            if (oldIndex >= paletteCount || seen[oldIndex])
                throw new ArgumentException(
                    "Palette reorder must be a permutation of every existing palette index exactly once.",
                    nameof(_newOrderOldIndices));
            seen[oldIndex] = true;
        }
    }

    private static void PrevalidateRevisions(
        Palette palette,
        IReadOnlyList<(ResourceId Id, PixelSurface Surface)> surfaces)
    {
        if (palette.Revision == long.MaxValue)
            throw new OverflowException("Palette revision cannot advance beyond Int64.MaxValue.");
        foreach (var pair in surfaces)
        {
            if (pair.Surface.Format != PixelFormat.Indexed8)
                throw new InvalidOperationException($"Surface '{pair.Id}' is not Indexed8.");
            if (pair.Surface.Revision == long.MaxValue)
                throw new OverflowException($"Surface '{pair.Id}' revision cannot advance beyond Int64.MaxValue.");
        }
    }

    private sealed record SurfaceState(ResourceId SurfaceId, byte[] Before, byte[] After);
    private sealed record Undo(
        Rgba32[] Colors,
        byte? TransparentIndex,
        SurfaceState[] Surfaces) : IUndoToken;
}

file static class PaletteCommandHelper
{
    public static IReadOnlyList<(ResourceId Id, PixelSurface Surface)> GetReferencedIndexedSurfaces(
        PixelDocument document,
        PaletteId paletteId) =>
        document.Resources.SurfaceIds
            .OrderBy(id => id.Value)
            .Select(id => (Id: id, Surface: document.Resources.GetSurface(id)))
            .Where(pair =>
                pair.Surface.Format == PixelFormat.Indexed8 &&
                pair.Surface.PaletteId == paletteId)
            .ToArray();

    public static DocumentChange FullDirty(PixelDocument document, PaletteId paletteId)
    {
        var dirty = GetReferencedIndexedSurfaces(document, paletteId)
            .Select(pair => new DirtySurfaceRegion(
                pair.Id,
                new IntRect(0, 0, pair.Surface.Size.Width, pair.Surface.Size.Height)))
            .ToArray();
        return dirty.Length == 0 ? DocumentChange.Empty : new DocumentChange(dirty);
    }
}
