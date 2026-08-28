using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Effects;

namespace MyLovePixel.Commands.Effects;

public sealed class AddEffectCommand(
    CelId celId,
    string typeId,
    EffectInstanceId? effectId = null,
    int? index = null) : ICommand
{
    private readonly EffectInstanceId _effectId = effectId ?? EffectInstanceId.New();

    public string Name => "Add Effect";
    public EffectInstanceId EffectId => _effectId;

    public CommandApplication Apply(PixelDocument document)
    {
        if (string.IsNullOrWhiteSpace(typeId)) throw new ArgumentException("Effect type id cannot be empty.", nameof(typeId));
        var graph = document.GetCel(celId).Effects;
        var effect = new EffectInstance(_effectId, typeId);
        var insertionIndex = index ?? graph.EffectOrder.Count;
        graph.Insert(insertionIndex, effect);
        return new CommandApplication(new Undo(insertionIndex), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.GetCel(celId).Effects.Remove(_effectId, out _);
        return DocumentChange.Empty;
    }

    private sealed record Undo(int Index) : IUndoToken;
}

public sealed class RemoveEffectCommand(CelId celId, EffectInstanceId effectId) : ICommand
{
    public string Name => "Remove Effect";

    public CommandApplication Apply(PixelDocument document)
    {
        var graph = document.GetCel(celId).Effects;
        var effect = graph.Remove(effectId, out var index);
        return new CommandApplication(new Undo(index, effect.Snapshot()), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.GetCel(celId).Effects.Insert(undo.Index, EffectInstance.FromSnapshot(undo.Effect));
        return DocumentChange.Empty;
    }

    private sealed record Undo(int Index, EffectInstanceSnapshot Effect) : IUndoToken;
}

public sealed class MoveEffectCommand(CelId celId, EffectInstanceId effectId, int newIndex) : ICommand
{
    public string Name => "Move Effect";

    public CommandApplication Apply(PixelDocument document)
    {
        var graph = document.GetCel(celId).Effects;
        var oldIndex = graph.EffectOrder.ToList().IndexOf(effectId);
        if (oldIndex < 0) throw new KeyNotFoundException($"Effect instance '{effectId}' does not exist.");
        graph.Move(effectId, newIndex);
        return new CommandApplication(new Undo(oldIndex), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.GetCel(celId).Effects.Move(effectId, undo.OldIndex);
        return DocumentChange.Empty;
    }

    private sealed record Undo(int OldIndex) : IUndoToken;
}

public sealed class SetEffectEnabledCommand(CelId celId, EffectInstanceId effectId, bool enabled) : ICommand
{
    public string Name => enabled ? "Enable Effect" : "Disable Effect";

    public CommandApplication Apply(PixelDocument document)
    {
        var effect = document.GetCel(celId).Effects.GetEffect(effectId);
        var previous = effect.Enabled;
        effect.SetEnabled(enabled);
        return new CommandApplication(new Undo(previous), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.GetCel(celId).Effects.GetEffect(effectId).SetEnabled(undo.Previous);
        return DocumentChange.Empty;
    }

    private sealed record Undo(bool Previous) : IUndoToken;
}

public sealed class SetEffectParameterCommand : ICommand
{
    private readonly CelId _celId;
    private readonly EffectInstanceId _effectId;
    private readonly string _key;
    private readonly EffectValue _value;
    private readonly EffectDescriptor _descriptor;

    public SetEffectParameterCommand(
        CelId celId,
        EffectInstanceId effectId,
        string key,
        EffectValue value,
        EffectDescriptor descriptor)
    {
        _celId = celId;
        _effectId = effectId;
        _key = key;
        _value = value ?? throw new ArgumentNullException(nameof(value));
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public string Name => "Set Effect Parameter";

    public CommandApplication Apply(PixelDocument document)
    {
        var effect = ResolveEffect(document);
        ValidateDescriptor(effect);
        _descriptor.GetParameter(_key).Validate(_value);
        var changed = effect.SetParameter(_key, _value, out var previous);
        return new CommandApplication(new Undo(changed, previous), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        if (!undo.Changed) return DocumentChange.Empty;
        var effect = ResolveEffect(document);
        if (undo.Previous is { } previous)
            effect.SetParameter(_key, previous, out _);
        else
            effect.RemoveParameter(_key, out _);
        return DocumentChange.Empty;
    }

    private EffectInstance ResolveEffect(PixelDocument document) =>
        document.GetCel(_celId).Effects.GetEffect(_effectId);

    private void ValidateDescriptor(EffectInstance effect)
    {
        if (!string.Equals(effect.TypeId, _descriptor.TypeId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Effect instance type '{effect.TypeId}' does not match descriptor '{_descriptor.TypeId}'.");
    }

    private sealed record Undo(bool Changed, EffectValue? Previous) : IUndoToken;
}

public sealed class SetEffectParameterKeyframeCommand : ICommand
{
    private readonly CelId _celId;
    private readonly EffectInstanceId _effectId;
    private readonly FrameId _frameId;
    private readonly string _key;
    private readonly EffectValue _value;
    private readonly EffectDescriptor _descriptor;
    private readonly AnimationTrackId _newTrackId = AnimationTrackId.New();

    public SetEffectParameterKeyframeCommand(
        CelId celId,
        EffectInstanceId effectId,
        FrameId frameId,
        string key,
        EffectValue value,
        EffectDescriptor descriptor)
    {
        _celId = celId;
        _effectId = effectId;
        _frameId = frameId;
        _key = key;
        _value = value ?? throw new ArgumentNullException(nameof(value));
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public string Name => "Set Effect Parameter Keyframe";

    public CommandApplication Apply(PixelDocument document)
    {
        document.GetFrame(_frameId);
        var effect = document.GetCel(_celId).Effects.GetEffect(_effectId);
        if (!string.Equals(effect.TypeId, _descriptor.TypeId, StringComparison.Ordinal))
            throw new InvalidOperationException("Effect descriptor type does not match the instance.");
        var parameter = _descriptor.GetParameter(_key);
        if (!parameter.Animatable)
            throw new InvalidOperationException($"Effect parameter '{_key}' is not animatable.");
        parameter.Validate(_value);
        var changed = effect.SetKeyframe(
            _key,
            _frameId,
            _value,
            _newTrackId,
            out var previous,
            out var trackCreated);
        return new CommandApplication(
            new Undo(changed, previous, trackCreated),
            DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        if (!undo.Changed) return DocumentChange.Empty;
        var effect = document.GetCel(_celId).Effects.GetEffect(_effectId);
        if (undo.Previous is { } previous)
        {
            effect.SetKeyframe(
                _key,
                _frameId,
                previous,
                _newTrackId,
                out _,
                out _);
        }
        else
        {
            effect.RemoveKeyframe(_key, _frameId, out _);
            if (undo.TrackCreated)
                effect.RemoveParameterTrack(_key, out _);
        }
        return DocumentChange.Empty;
    }

    private sealed record Undo(
        bool Changed,
        EffectValue? Previous,
        bool TrackCreated) : IUndoToken;
}

public sealed class ClearEffectParameterKeyframeCommand(
    CelId celId,
    EffectInstanceId effectId,
    FrameId frameId,
    string key) : ICommand
{
    public string Name => "Clear Effect Parameter Keyframe";

    public CommandApplication Apply(PixelDocument document)
    {
        var effect = document.GetCel(celId).Effects.GetEffect(effectId);
        var changed = effect.RemoveKeyframe(key, frameId, out var previous);
        return new CommandApplication(
            new Undo(changed, changed ? previous : null),
            DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        if (!undo.Changed) return DocumentChange.Empty;
        var effect = document.GetCel(celId).Effects.GetEffect(effectId);
        if (!effect.ParameterTracks.TryGetValue(key, out var track))
            throw new InvalidOperationException($"Effect parameter track '{key}' no longer exists.");
        effect.SetKeyframe(key, frameId, undo.Previous!, track.Id, out _, out _);
        return DocumentChange.Empty;
    }

    private sealed record Undo(bool Changed, EffectValue? Previous) : IUndoToken;
}

public sealed class BakeEffectsCommand : ICommand
{
    private readonly EffectBakePlan _plan;
    private readonly ResourceId _bakedSurfaceId = ResourceId.New();

    public BakeEffectsCommand(EffectBakePlan plan)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
    }

    public string Name => "Bake Effects";
    public ResourceId BakedSurfaceId => _bakedSurfaceId;

    public CommandApplication Apply(PixelDocument document)
    {
        ValidatePlan(document);
        var cel = document.GetCel(_plan.CelId);
        var originalGraph = cel.Effects.Snapshot();
        var originalSurfaceId = cel.SurfaceId;
        var originalPosition = cel.Position;
        var baked = new PixelSurface(_plan.Image.Size);
        var writes = new PixelWrite[checked(_plan.Image.Size.Width * _plan.Image.Size.Height)];
        var cursor = 0;
        for (var y = 0; y < _plan.Image.Size.Height; y++)
        for (var x = 0; x < _plan.Image.Size.Width; x++)
            writes[cursor++] = new PixelWrite(x, y, _plan.Image.GetPixel(x, y));
        baked.SetPixels(writes);

        document.Resources.AddSurface(_bakedSurfaceId, baked);
        cel.SurfaceId = _bakedSurfaceId;
        cel.Position = new IntPoint(
            checked(cel.Position.X + _plan.Image.Origin.X),
            checked(cel.Position.Y + _plan.Image.Origin.Y));
        cel.Effects = new EffectGraph();

        return new CommandApplication(
            new Undo(originalSurfaceId, originalPosition, originalGraph),
            DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        var cel = document.GetCel(_plan.CelId);
        if (cel.SurfaceId != _bakedSurfaceId)
            throw new InvalidOperationException("Cannot undo baked effects after the Cel surface reference changed.");
        cel.SurfaceId = undo.OriginalSurfaceId;
        cel.Position = undo.OriginalPosition;
        cel.Effects = EffectGraph.FromSnapshot(undo.OriginalEffects);
        if (document.IsSurfaceReferenced(_bakedSurfaceId))
            throw new InvalidOperationException("Cannot remove baked surface while it is still referenced.");
        document.Resources.RemoveSurface(_bakedSurfaceId);
        return DocumentChange.Empty;
    }

    private void ValidatePlan(PixelDocument document)
    {
        var cel = document.GetCel(_plan.CelId);
        if (cel.FrameId != _plan.FrameId)
            throw new InvalidOperationException("Effect bake plan frame no longer matches the Cel.");
        if (cel.SurfaceId != _plan.SourceSurfaceId)
            throw new InvalidOperationException("Effect bake plan source surface is stale.");
        if (cel.Position != _plan.SourcePosition)
            throw new InvalidOperationException("Effect bake plan Cel position is stale.");
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        if (surface.Revision != _plan.SourceSurfaceRevision)
            throw new InvalidOperationException("Effect bake plan source pixels are stale.");
        if (cel.Effects.Revision != _plan.EffectGraphRevision)
            throw new InvalidOperationException("Effect bake plan graph structure is stale.");
        if (cel.Effects.EffectOrder.Count != _plan.Effects.Count)
            throw new InvalidOperationException("Effect bake plan effect order is stale.");
        for (var index = 0; index < _plan.Effects.Count; index++)
        {
            var expected = _plan.Effects[index];
            if (cel.Effects.EffectOrder[index] != expected.EffectId ||
                cel.Effects.GetEffect(expected.EffectId).Revision != expected.Revision)
                throw new InvalidOperationException("Effect bake plan parameters are stale.");
        }

        var paletteIds = document.Resources.PaletteIds.OrderBy(id => id.Value).ToArray();
        if (paletteIds.Length != _plan.Palettes.Count)
            throw new InvalidOperationException("Effect bake plan palette dependencies are stale.");
        for (var index = 0; index < paletteIds.Length; index++)
        {
            var expected = _plan.Palettes[index];
            if (paletteIds[index] != expected.PaletteId ||
                document.Resources.GetPalette(expected.PaletteId).Revision != expected.Revision)
                throw new InvalidOperationException("Effect bake plan palette dependencies are stale.");
        }

        document.Animation.ColorCycleTrack.TryGetValue(_plan.FrameId, out var colorCycles);
        if (!Equals(colorCycles, _plan.ColorCycles))
            throw new InvalidOperationException("Effect bake plan color-cycle metadata is stale.");
    }

    private sealed record Undo(
        ResourceId OriginalSurfaceId,
        IntPoint OriginalPosition,
        EffectGraphSnapshot OriginalEffects) : IUndoToken;
}
