using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Commands.Pixel;

public sealed class ReplacePixelSurfaceCommand : ICommand
{
    private readonly ResourceId _surfaceId;
    private readonly PixelFormat _format;
    private readonly PaletteId? _paletteId;
    private readonly byte[] _bytes;

    public ReplacePixelSurfaceCommand(
        ResourceId surfaceId,
        PixelFormat format,
        PaletteId? paletteId,
        ReadOnlyMemory<byte> bytes,
        string name = "Replace Pixel Surface")
    {
        if (surfaceId.Value == Guid.Empty) throw new ArgumentException("ResourceId cannot be empty.", nameof(surfaceId));
        _surfaceId = surfaceId;
        _format = format;
        _paletteId = paletteId;
        _bytes = bytes.ToArray();
        Name = string.IsNullOrWhiteSpace(name) ? "Replace Pixel Surface" : name.Trim();
    }

    public string Name { get; }

    public CommandApplication Apply(PixelDocument document)
    {
        var surface = document.Resources.GetSurface(_surfaceId);
        var before = surface.Snapshot();
        Validate(document, surface.Size, _format, _paletteId, _bytes);
        surface.ReplaceState(_format, _paletteId, _bytes);
        return new CommandApplication(
            new Undo(before.Format, before.PaletteId, before.Bytes.ToArray()),
            DocumentChange.ForSurface(_surfaceId, new IntRect(0, 0, surface.Size.Width, surface.Size.Height)));
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        var surface = document.Resources.GetSurface(_surfaceId);
        Validate(document, surface.Size, undo.Format, undo.PaletteId, undo.Bytes);
        surface.ReplaceState(undo.Format, undo.PaletteId, undo.Bytes);
        return DocumentChange.ForSurface(_surfaceId, new IntRect(0, 0, surface.Size.Width, surface.Size.Height));
    }

    private static void Validate(PixelDocument document, IntSize size, PixelFormat format, PaletteId? paletteId, ReadOnlySpan<byte> bytes)
    {
        switch (format)
        {
            case PixelFormat.Rgba32:
                if (paletteId is not null) throw new ArgumentException("RGBA32 replacement cannot reference a palette.", nameof(paletteId));
                if (bytes.Length != checked(size.Width * size.Height * 4))
                    throw new ArgumentException("RGBA32 replacement byte length does not match the surface size.", nameof(bytes));
                break;

            case PixelFormat.Indexed8:
                if (paletteId is not { } id) throw new ArgumentException("Indexed8 replacement requires a palette.", nameof(paletteId));
                var palette = document.Resources.GetPalette(id);
                if (bytes.Length != checked(size.Width * size.Height))
                    throw new ArgumentException("Indexed8 replacement byte length does not match the surface size.", nameof(bytes));
                foreach (var index in bytes)
                    if (index >= palette.Count)
                        throw new ArgumentException($"Palette index {index} is outside palette '{id}'.", nameof(bytes));
                break;

            default:
                throw new NotSupportedException($"Pixel format '{format}' is not supported.");
        }
    }

    private sealed record Undo(PixelFormat Format, PaletteId? PaletteId, byte[] Bytes) : IUndoToken;
}
