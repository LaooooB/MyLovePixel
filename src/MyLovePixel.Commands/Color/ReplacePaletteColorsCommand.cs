using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Commands.Color;

public sealed class ReplacePaletteColorsCommand : ICommand
{
    private readonly PaletteId _paletteId;
    private readonly Rgba32[] _colors;

    public ReplacePaletteColorsCommand(PaletteId paletteId, IEnumerable<Rgba32> colors, string name = "Replace Palette Colors")
    {
        _paletteId = paletteId;
        ArgumentNullException.ThrowIfNull(colors);
        _colors = colors.ToArray();
        if (_colors.Length is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(colors));
        Name = string.IsNullOrWhiteSpace(name) ? "Replace Palette Colors" : name.Trim();
    }

    public string Name { get; }

    public CommandApplication Apply(PixelDocument document)
    {
        var palette = document.Resources.GetPalette(_paletteId);
        var before = palette.Snapshot();
        var transparent = before.TransparentIndex;
        if (transparent is { } t && t >= _colors.Length)
            throw new InvalidOperationException("Palette resize would remove the transparent index.");
        ValidateIndexedReferences(document, _colors.Length);
        palette.ReplaceResizedState(_colors, transparent);
        return new CommandApplication(
            new Undo(before.Colors.ToArray(), before.TransparentIndex),
            FullDirty(document));
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        var palette = document.Resources.GetPalette(_paletteId);
        palette.ReplaceResizedState(undo.Colors, undo.TransparentIndex);
        return FullDirty(document);
    }

    private void ValidateIndexedReferences(PixelDocument document, int colorCount)
    {
        foreach (var id in document.Resources.SurfaceIds)
        {
            var surface = document.Resources.GetSurface(id);
            if (surface.Format != PixelFormat.Indexed8 || surface.PaletteId != _paletteId) continue;
            foreach (var index in surface.Snapshot().Bytes.Span)
                if (index >= colorCount)
                    throw new InvalidOperationException($"Palette resize would invalidate index {index} in surface '{id}'.");
        }
    }

    private DocumentChange FullDirty(PixelDocument document)
    {
        var dirty = document.Resources.SurfaceIds
            .Select(id => (Id: id, Surface: document.Resources.GetSurface(id)))
            .Where(pair => pair.Surface.Format == PixelFormat.Indexed8 && pair.Surface.PaletteId == _paletteId)
            .Select(pair => new DirtySurfaceRegion(pair.Id, new IntRect(0, 0, pair.Surface.Size.Width, pair.Surface.Size.Height)))
            .ToArray();
        return dirty.Length == 0 ? DocumentChange.Empty : new DocumentChange(dirty);
    }

    private sealed record Undo(Rgba32[] Colors, byte? TransparentIndex) : IUndoToken;
}
