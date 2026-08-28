using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Commands.Document;

public sealed class SetLayerNameCommand : ICommand
{
    private readonly LayerId _layerId;
    private readonly string _name;

    public SetLayerNameCommand(LayerId layerId, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Layer name cannot be empty.", nameof(name));
        _layerId = layerId;
        _name = name.Trim();
    }

    public string Name => "Rename Layer";

    public CommandApplication Apply(PixelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var layer = document.GetLayer(_layerId);
        var previous = layer.Name;
        layer.Name = _name;
        return new CommandApplication(new Undo(previous), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.GetLayer(_layerId).Name = undo.Previous;
        return DocumentChange.Empty;
    }

    private sealed record Undo(string Previous) : IUndoToken;
}

public sealed class SetLayerVisibilityCommand(LayerId layerId, bool visible) : ICommand
{
    public string Name => visible ? "Show Layer" : "Hide Layer";

    public CommandApplication Apply(PixelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var layer = document.GetLayer(layerId);
        var previous = layer.Visible;
        layer.Visible = visible;
        return new CommandApplication(new Undo(previous), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.GetLayer(layerId).Visible = undo.Previous;
        return DocumentChange.Empty;
    }

    private sealed record Undo(bool Previous) : IUndoToken;
}

public sealed class SetLayerLockCommand(LayerId layerId, bool locked) : ICommand
{
    public string Name => locked ? "Lock Layer" : "Unlock Layer";

    public CommandApplication Apply(PixelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var layer = document.GetLayer(layerId);
        var previous = layer.Locked;
        layer.Locked = locked;
        return new CommandApplication(new Undo(previous), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.GetLayer(layerId).Locked = undo.Previous;
        return DocumentChange.Empty;
    }

    private sealed record Undo(bool Previous) : IUndoToken;
}

public sealed class SetLayerOpacityCommand(LayerId layerId, byte opacity) : ICommand
{
    public string Name => "Set Layer Opacity";

    public CommandApplication Apply(PixelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var layer = document.GetLayer(layerId);
        var previous = layer.Opacity;
        layer.Opacity = opacity;
        return new CommandApplication(new Undo(previous), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.GetLayer(layerId).Opacity = undo.Previous;
        return DocumentChange.Empty;
    }

    private sealed record Undo(byte Previous) : IUndoToken;
}
