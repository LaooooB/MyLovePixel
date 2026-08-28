using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Commands.Timeline;

public sealed class SetFrameDurationCommand(FrameId frameId, long durationTicks) : ICommand
{
    public string Name => "Set Frame Duration";

    public CommandApplication Apply(PixelDocument document)
    {
        if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));
        var frame = document.GetFrame(frameId);
        var previous = frame.DurationTicks;
        frame.DurationTicks = durationTicks;
        return new CommandApplication(new Undo(previous), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.GetFrame(frameId).DurationTicks = undo.DurationTicks;
        return DocumentChange.Empty;
    }

    private sealed record Undo(long DurationTicks) : IUndoToken;
}

public sealed class MoveFrameCommand(FrameId frameId, int newIndex) : ICommand
{
    public string Name => "Move Frame";

    public CommandApplication Apply(PixelDocument document)
    {
        var oldIndex = document.GetFrameIndex(frameId);
        document.MoveFrame(frameId, newIndex);
        return new CommandApplication(new Undo(oldIndex), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.MoveFrame(frameId, undo.OldIndex);
        return DocumentChange.Empty;
    }

    private sealed record Undo(int OldIndex) : IUndoToken;
}

public enum FrameCopyMode
{
    Linked = 0,
    Independent = 1,
}

public sealed class CopyFrameCommand : ICommand
{
    private readonly FrameId _sourceFrameId;
    private readonly int? _insertIndex;
    private readonly FrameCopyMode _mode;
    private readonly FrameId _newFrameId = FrameId.New();
    private readonly Dictionary<CelId, CelId> _celIds = [];
    private readonly Dictionary<ResourceId, ResourceId> _surfaceIds = [];

    public CopyFrameCommand(
        FrameId sourceFrameId,
        FrameCopyMode mode = FrameCopyMode.Linked,
        int? insertIndex = null)
    {
        _sourceFrameId = sourceFrameId;
        _mode = mode;
        _insertIndex = insertIndex;
    }

    public string Name => _mode == FrameCopyMode.Linked ? "Copy Frame Linked" : "Copy Frame Independent";
    public FrameId NewFrameId => _newFrameId;

    public CommandApplication Apply(PixelDocument document)
    {
        var sourceFrame = document.GetFrame(_sourceFrameId);
        var sourceIndex = document.GetFrameIndex(_sourceFrameId);
        var targetIndex = _insertIndex ?? checked(sourceIndex + 1);
        if ((uint)targetIndex > (uint)document.FrameOrder.Count)
            throw new ArgumentOutOfRangeException(nameof(_insertIndex));

        var sourceCels = document.Cels
            .Where(cel => cel.FrameId == _sourceFrameId)
            .OrderBy(cel => document.LayerOrder.IndexOf(cel.LayerId))
            .ToArray();

        if (_celIds.Count == 0)
        {
            foreach (var cel in sourceCels) _celIds.Add(cel.Id, CelId.New());
            if (_mode == FrameCopyMode.Independent)
            {
                foreach (var surfaceId in sourceCels.Select(cel => cel.SurfaceId).Distinct())
                    _surfaceIds.Add(surfaceId, ResourceId.New());
            }
        }

        document.InsertFrame(targetIndex, new Frame(_newFrameId, sourceFrame.DurationTicks));
        CopyTrackValues(document.Animation, _sourceFrameId, _newFrameId);

        if (_mode == FrameCopyMode.Independent)
        {
            foreach (var pair in _surfaceIds)
            {
                var clone = document.Resources.GetSurface(pair.Key).Clone();
                document.Resources.AddSurface(pair.Value, clone);
            }
        }

        foreach (var sourceCel in sourceCels)
        {
            var surfaceId = _mode == FrameCopyMode.Linked
                ? sourceCel.SurfaceId
                : _surfaceIds[sourceCel.SurfaceId];
            var copied = new Cel(
                _celIds[sourceCel.Id],
                sourceCel.LayerId,
                _newFrameId,
                surfaceId)
            {
                Position = sourceCel.Position,
                Opacity = sourceCel.Opacity,
            };
            document.AddCel(copied);
        }

        return new CommandApplication(new Undo(), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));

        foreach (var cel in document.Cels.Where(cel => cel.FrameId == _newFrameId).ToArray())
            document.RemoveCel(cel.Id);
        RemoveTrackValues(document.Animation, _newFrameId);
        document.RemoveFrame(_newFrameId);

        if (_mode == FrameCopyMode.Independent)
        {
            foreach (var surfaceId in _surfaceIds.Values)
                document.Resources.RemoveSurface(surfaceId);
        }

        return DocumentChange.Empty;
    }

    private static void CopyTrackValues(AnimationMetadata animation, FrameId sourceFrameId, FrameId targetFrameId)
    {
        if (animation.PivotTrack.TryGetValue(sourceFrameId, out var pivot))
            animation.PivotTrack.Set(targetFrameId, pivot);
        if (animation.HitboxTrack.TryGetValue(sourceFrameId, out var hitboxes))
            animation.HitboxTrack.Set(targetFrameId, hitboxes);
        if (animation.HurtboxTrack.TryGetValue(sourceFrameId, out var hurtboxes))
            animation.HurtboxTrack.Set(targetFrameId, hurtboxes);
        if (animation.SocketTrack.TryGetValue(sourceFrameId, out var sockets))
            animation.SocketTrack.Set(targetFrameId, sockets);
        if (animation.EventTrack.TryGetValue(sourceFrameId, out var events))
            animation.EventTrack.Set(targetFrameId, events);
        if (animation.ColorCycleTrack.TryGetValue(sourceFrameId, out var colorCycles))
            animation.ColorCycleTrack.Set(targetFrameId, colorCycles);
    }

    private static void RemoveTrackValues(AnimationMetadata animation, FrameId frameId)
    {
        animation.PivotTrack.Remove(frameId, out _);
        animation.HitboxTrack.Remove(frameId, out _);
        animation.HurtboxTrack.Remove(frameId, out _);
        animation.SocketTrack.Remove(frameId, out _);
        animation.EventTrack.Remove(frameId, out _);
        animation.ColorCycleTrack.Remove(frameId, out _);
    }

    private sealed record Undo : IUndoToken;
}

public sealed class RemoveFrameCommand(FrameId frameId) : ICommand
{
    public string Name => "Remove Frame";

    public CommandApplication Apply(PixelDocument document)
    {
        if (document.FrameOrder.Count <= 1)
            throw new InvalidOperationException("A document must keep at least one frame.");

        var oldFrameOrder = document.FrameOrder.ToArray();
        var oldIndex = document.GetFrameIndex(frameId);
        var frame = document.GetFrame(frameId);
        var cels = document.Cels
            .Where(cel => cel.FrameId == frameId)
            .OrderBy(cel => document.LayerOrder.IndexOf(cel.LayerId))
            .ToArray();

        var clipChanges = AdjustClips(document, frameId, oldFrameOrder);
        var tagChanges = AdjustTags(document, frameId, oldFrameOrder);
        var trackState = RemoveTrackValues(document.Animation, frameId);

        foreach (var cel in cels) document.RemoveCel(cel.Id);

        var removedSurfaces = new Dictionary<ResourceId, PixelSurface>();
        foreach (var surfaceId in cels.Select(cel => cel.SurfaceId).Distinct())
        {
            if (!document.IsSurfaceReferenced(surfaceId))
                removedSurfaces.Add(surfaceId, document.Resources.RemoveSurface(surfaceId));
        }

        document.RemoveFrame(frameId);

        return new CommandApplication(
            new Undo(oldIndex, frame, cels, removedSurfaces, clipChanges, tagChanges, trackState),
            DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));

        foreach (var pair in undo.RemovedSurfaces)
            document.Resources.AddSurface(pair.Key, pair.Value);
        document.InsertFrame(undo.OldIndex, undo.Frame);
        foreach (var cel in undo.Cels) document.AddCel(cel);

        RestoreClips(document.Animation, undo.ClipChanges);
        RestoreTags(document.Animation, undo.TagChanges);
        RestoreTrackValues(document.Animation, frameId, undo.TrackState);
        return DocumentChange.Empty;
    }

    private static AffectedClip[] AdjustClips(PixelDocument document, FrameId removedFrame, IReadOnlyList<FrameId> oldOrder)
    {
        var result = new List<AffectedClip>();
        foreach (var clipId in document.Animation.ClipOrder.ToArray())
        {
            var clip = document.Animation.GetClip(clipId);
            if (clip.StartFrameId != removedFrame && clip.EndFrameId != removedFrame) continue;
            var orderIndex = document.Animation.IndexOfClip(clip.Id);
            result.Add(new AffectedClip(orderIndex, clip));

            var startIndex = IndexOf(oldOrder, clip.StartFrameId);
            var endIndex = IndexOf(oldOrder, clip.EndFrameId);
            if (startIndex == endIndex)
            {
                document.Animation.RemoveClip(clip.Id, out _);
                continue;
            }

            var newStart = clip.StartFrameId == removedFrame ? oldOrder[startIndex + 1] : clip.StartFrameId;
            var newEnd = clip.EndFrameId == removedFrame ? oldOrder[endIndex - 1] : clip.EndFrameId;
            document.Animation.UpsertClip(new AnimationClip(clip.Id, clip.Name, newStart, newEnd, clip.LoopMode));
        }
        return result.ToArray();
    }

    private static AffectedTag[] AdjustTags(PixelDocument document, FrameId removedFrame, IReadOnlyList<FrameId> oldOrder)
    {
        var result = new List<AffectedTag>();
        foreach (var tagId in document.Animation.TagOrder.ToArray())
        {
            var tag = document.Animation.GetTag(tagId);
            if (tag.StartFrameId != removedFrame && tag.EndFrameId != removedFrame) continue;
            var orderIndex = document.Animation.IndexOfTag(tag.Id);
            result.Add(new AffectedTag(orderIndex, tag));

            var startIndex = IndexOf(oldOrder, tag.StartFrameId);
            var endIndex = IndexOf(oldOrder, tag.EndFrameId);
            if (startIndex == endIndex)
            {
                document.Animation.RemoveTag(tag.Id, out _);
                continue;
            }

            var newStart = tag.StartFrameId == removedFrame ? oldOrder[startIndex + 1] : tag.StartFrameId;
            var newEnd = tag.EndFrameId == removedFrame ? oldOrder[endIndex - 1] : tag.EndFrameId;
            document.Animation.UpsertTag(new AnimationTag(tag.Id, tag.Name, newStart, newEnd));
        }
        return result.ToArray();
    }

    private static TrackRemovalState RemoveTrackValues(AnimationMetadata animation, FrameId frameId)
    {
        var hasPivot = animation.PivotTrack.Remove(frameId, out var pivot);
        var hasHitboxes = animation.HitboxTrack.Remove(frameId, out var hitboxes);
        var hasHurtboxes = animation.HurtboxTrack.Remove(frameId, out var hurtboxes);
        var hasSockets = animation.SocketTrack.Remove(frameId, out var sockets);
        var hasEvents = animation.EventTrack.Remove(frameId, out var events);
        var hasColorCycles = animation.ColorCycleTrack.Remove(frameId, out var colorCycles);
        return new TrackRemovalState(
            hasPivot, pivot,
            hasHitboxes, hitboxes,
            hasHurtboxes, hurtboxes,
            hasSockets, sockets,
            hasEvents, events,
            hasColorCycles, colorCycles);
    }

    private static void RestoreClips(AnimationMetadata animation, IReadOnlyList<AffectedClip> changes)
    {
        foreach (var change in changes.OrderBy(item => item.OrderIndex))
        {
            if (animation.IndexOfClip(change.Clip.Id) < 0)
                animation.InsertClip(change.OrderIndex, change.Clip);
            else
                animation.UpsertClip(change.Clip);
        }
    }

    private static void RestoreTags(AnimationMetadata animation, IReadOnlyList<AffectedTag> changes)
    {
        foreach (var change in changes.OrderBy(item => item.OrderIndex))
        {
            if (animation.IndexOfTag(change.Tag.Id) < 0)
                animation.InsertTag(change.OrderIndex, change.Tag);
            else
                animation.UpsertTag(change.Tag);
        }
    }

    private static void RestoreTrackValues(AnimationMetadata animation, FrameId frameId, TrackRemovalState state)
    {
        if (state.HasPivot) animation.PivotTrack.Restore(frameId, state.Pivot);
        if (state.HasHitboxes) animation.HitboxTrack.Restore(frameId, state.Hitboxes!);
        if (state.HasHurtboxes) animation.HurtboxTrack.Restore(frameId, state.Hurtboxes!);
        if (state.HasSockets) animation.SocketTrack.Restore(frameId, state.Sockets!);
        if (state.HasEvents) animation.EventTrack.Restore(frameId, state.Events!);
        if (state.HasColorCycles) animation.ColorCycleTrack.Restore(frameId, state.ColorCycles!);
    }

    private static int IndexOf(IReadOnlyList<FrameId> values, FrameId value)
    {
        for (var index = 0; index < values.Count; index++)
            if (values[index] == value) return index;
        throw new InvalidOperationException($"Frame '{value}' is not in the captured frame order.");
    }

    private sealed record AffectedClip(int OrderIndex, AnimationClip Clip);
    private sealed record AffectedTag(int OrderIndex, AnimationTag Tag);
    private sealed record TrackRemovalState(
        bool HasPivot,
        IntPoint Pivot,
        bool HasHitboxes,
        BoxFrameValue? Hitboxes,
        bool HasHurtboxes,
        BoxFrameValue? Hurtboxes,
        bool HasSockets,
        SocketFrameValue? Sockets,
        bool HasEvents,
        EventFrameValue? Events,
        bool HasColorCycles,
        ColorCycleFrameValue? ColorCycles);

    private sealed record Undo(
        int OldIndex,
        Frame Frame,
        Cel[] Cels,
        IReadOnlyDictionary<ResourceId, PixelSurface> RemovedSurfaces,
        AffectedClip[] ClipChanges,
        AffectedTag[] TagChanges,
        TrackRemovalState TrackState) : IUndoToken;
}

file static class FrameCommandExtensions
{
    public static int IndexOf(this IReadOnlyList<LayerId> values, LayerId value)
    {
        for (var index = 0; index < values.Count; index++)
            if (values[index] == value) return index;
        return int.MaxValue;
    }
}
