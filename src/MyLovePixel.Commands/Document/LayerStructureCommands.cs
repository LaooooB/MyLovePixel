using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Commands.Document;

public sealed class AddPixelLayerCommand : ICommand
{
    private readonly string _name;
    private readonly FrameId _frameId;
    private readonly int? _index;
    private readonly LayerId _layerId = LayerId.New();
    private readonly ResourceId _surfaceId = ResourceId.New();
    private readonly CelId _celId = CelId.New();

    public AddPixelLayerCommand(string name, FrameId frameId, int? index = null)
    {
        _name = string.IsNullOrWhiteSpace(name) ? "Layer" : name.Trim();
        _frameId = frameId;
        _index = index;
    }

    public string Name => "Add Layer";
    public LayerId LayerId => _layerId;

    public CommandApplication Apply(PixelDocument document)
    {
        document.GetFrame(_frameId);
        var index = _index ?? document.LayerOrder.Count;
        if ((uint)index > (uint)document.LayerOrder.Count) throw new ArgumentOutOfRangeException(nameof(_index));
        var surface = new PixelSurface(document.Canvas.Size);
        document.Resources.AddSurface(_surfaceId, surface);
        try
        {
            document.InsertLayer(index, new PixelLayer(_layerId, _name));
            document.AddCel(new Cel(_celId, _layerId, _frameId, _surfaceId));
        }
        catch
        {
            if (document.Cels.Any(cel => cel.Id == _celId)) document.RemoveCel(_celId);
            if (document.LayerOrder.Contains(_layerId)) document.RemoveLayer(_layerId);
            if (document.Resources.ContainsSurface(_surfaceId)) document.Resources.RemoveSurface(_surfaceId);
            throw;
        }
        return new CommandApplication(new Undo(index), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.RemoveCel(_celId);
        document.RemoveLayer(_layerId);
        document.Resources.RemoveSurface(_surfaceId);
        return DocumentChange.Empty;
    }

    private sealed record Undo(int Index) : IUndoToken;
}

public sealed class EnsureCelCommand : ICommand
{
    private readonly LayerId _layerId;
    private readonly FrameId _frameId;
    private readonly ResourceId _surfaceId = ResourceId.New();
    private readonly CelId _celId = CelId.New();

    public EnsureCelCommand(LayerId layerId, FrameId frameId)
    {
        _layerId = layerId;
        _frameId = frameId;
    }

    public string Name => "Create Cel";

    public CommandApplication Apply(PixelDocument document)
    {
        document.GetLayer(_layerId);
        document.GetFrame(_frameId);
        if (document.FindCel(_layerId, _frameId) is not null)
            return new CommandApplication(new Undo(false), DocumentChange.Empty);
        document.Resources.AddSurface(_surfaceId, new PixelSurface(document.Canvas.Size));
        try { document.AddCel(new Cel(_celId, _layerId, _frameId, _surfaceId)); }
        catch
        {
            document.Resources.RemoveSurface(_surfaceId);
            throw;
        }
        return new CommandApplication(new Undo(true), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        if (!undo.Created) return DocumentChange.Empty;
        document.RemoveCel(_celId);
        document.Resources.RemoveSurface(_surfaceId);
        return DocumentChange.Empty;
    }

    private sealed record Undo(bool Created) : IUndoToken;
}

public sealed class MoveLayerCommand(LayerId layerId, int newIndex) : ICommand
{
    public string Name => "Move Layer";

    public CommandApplication Apply(PixelDocument document)
    {
        var oldIndex = document.GetLayerIndex(layerId);
        document.MoveLayer(layerId, newIndex);
        return new CommandApplication(new Undo(oldIndex), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.MoveLayer(layerId, undo.OldIndex);
        return DocumentChange.Empty;
    }

    private sealed record Undo(int OldIndex) : IUndoToken;
}

public sealed class RemoveLayerCommand(LayerId layerId) : ICommand
{
    public string Name => "Remove Layer";

    public CommandApplication Apply(PixelDocument document)
    {
        if (document.LayerOrder.Count <= 1) throw new InvalidOperationException("A document must keep at least one layer.");
        var index = document.GetLayerIndex(layerId);
        var layer = document.GetLayer(layerId);
        var cels = document.Cels.Where(cel => cel.LayerId == layerId).ToArray();
        foreach (var cel in cels) document.RemoveCel(cel.Id);

        var surfaces = new Dictionary<ResourceId, PixelSurface>();
        foreach (var surfaceId in cels.Select(cel => cel.SurfaceId).Distinct())
        {
            if (document.IsSurfaceReferenced(surfaceId)) continue;
            surfaces.Add(surfaceId, document.Resources.RemoveSurface(surfaceId));
        }
        document.RemoveLayer(layerId);
        return new CommandApplication(new Undo(index, layer, cels, surfaces), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        foreach (var pair in undo.Surfaces) document.Resources.AddSurface(pair.Key, pair.Value);
        document.InsertLayer(undo.Index, undo.Layer);
        foreach (var cel in undo.Cels) document.AddCel(cel);
        return DocumentChange.Empty;
    }

    private sealed record Undo(int Index, Layer Layer, Cel[] Cels, IReadOnlyDictionary<ResourceId, PixelSurface> Surfaces) : IUndoToken;
}
