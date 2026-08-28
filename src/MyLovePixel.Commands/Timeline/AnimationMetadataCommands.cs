using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Commands.Timeline;

public sealed class UpsertAnimationClipCommand(AnimationClip clip) : ICommand
{
    public string Name => "Set Animation Clip";

    public CommandApplication Apply(PixelDocument document)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ValidateRange(document, clip.StartFrameId, clip.EndFrameId);
        var oldIndex = document.Animation.IndexOfClip(clip.Id);
        var hadPrevious = oldIndex >= 0;
        var previous = hadPrevious ? document.Animation.GetClip(clip.Id) : null;
        document.Animation.UpsertClip(clip);
        return new CommandApplication(new Undo(hadPrevious, oldIndex, previous), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        if (undo.HadPrevious)
            document.Animation.UpsertClip(undo.Previous!);
        else
            document.Animation.RemoveClip(clip.Id, out _);
        return DocumentChange.Empty;
    }

    private static void ValidateRange(PixelDocument document, FrameId start, FrameId end)
    {
        if (document.GetFrameIndex(start) > document.GetFrameIndex(end))
            throw new ArgumentException("Animation clip start frame cannot appear after its end frame.", nameof(clip));
    }

    private sealed record Undo(bool HadPrevious, int OldIndex, AnimationClip? Previous) : IUndoToken;
}

public sealed class RemoveAnimationClipCommand(AnimationClipId clipId) : ICommand
{
    public string Name => "Remove Animation Clip";

    public CommandApplication Apply(PixelDocument document)
    {
        var index = document.Animation.IndexOfClip(clipId);
        if (index < 0) throw new KeyNotFoundException($"Animation clip '{clipId}' does not exist.");
        document.Animation.RemoveClip(clipId, out var clip);
        return new CommandApplication(new Undo(index, clip), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.Animation.InsertClip(undo.Index, undo.Clip);
        return DocumentChange.Empty;
    }

    private sealed record Undo(int Index, AnimationClip Clip) : IUndoToken;
}

public sealed class UpsertAnimationTagCommand(AnimationTag tag) : ICommand
{
    public string Name => "Set Animation Tag";

    public CommandApplication Apply(PixelDocument document)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (document.GetFrameIndex(tag.StartFrameId) > document.GetFrameIndex(tag.EndFrameId))
            throw new ArgumentException("Animation tag start frame cannot appear after its end frame.", nameof(tag));
        var oldIndex = document.Animation.IndexOfTag(tag.Id);
        var hadPrevious = oldIndex >= 0;
        var previous = hadPrevious ? document.Animation.GetTag(tag.Id) : null;
        document.Animation.UpsertTag(tag);
        return new CommandApplication(new Undo(hadPrevious, previous), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        if (undo.HadPrevious)
            document.Animation.UpsertTag(undo.Previous!);
        else
            document.Animation.RemoveTag(tag.Id, out _);
        return DocumentChange.Empty;
    }

    private sealed record Undo(bool HadPrevious, AnimationTag? Previous) : IUndoToken;
}

public sealed class RemoveAnimationTagCommand(AnimationTagId tagId) : ICommand
{
    public string Name => "Remove Animation Tag";

    public CommandApplication Apply(PixelDocument document)
    {
        var index = document.Animation.IndexOfTag(tagId);
        if (index < 0) throw new KeyNotFoundException($"Animation tag '{tagId}' does not exist.");
        document.Animation.RemoveTag(tagId, out var tag);
        return new CommandApplication(new Undo(index, tag), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.Animation.InsertTag(undo.Index, undo.Tag);
        return DocumentChange.Empty;
    }

    private sealed record Undo(int Index, AnimationTag Tag) : IUndoToken;
}

public sealed class UpsertSpriteSliceCommand(SpriteSlice slice) : ICommand
{
    public string Name => "Set Sprite Slice";

    public CommandApplication Apply(PixelDocument document)
    {
        ArgumentNullException.ThrowIfNull(slice);
        var oldIndex = document.Animation.IndexOfSlice(slice.Id);
        var hadPrevious = oldIndex >= 0;
        var previous = hadPrevious ? document.Animation.GetSlice(slice.Id) : null;
        document.Animation.UpsertSlice(slice);
        return new CommandApplication(new Undo(hadPrevious, previous), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        if (undo.HadPrevious)
            document.Animation.UpsertSlice(undo.Previous!);
        else
            document.Animation.RemoveSlice(slice.Id, out _);
        return DocumentChange.Empty;
    }

    private sealed record Undo(bool HadPrevious, SpriteSlice? Previous) : IUndoToken;
}

public sealed class RemoveSpriteSliceCommand(SliceId sliceId) : ICommand
{
    public string Name => "Remove Sprite Slice";

    public CommandApplication Apply(PixelDocument document)
    {
        var index = document.Animation.IndexOfSlice(sliceId);
        if (index < 0) throw new KeyNotFoundException($"Sprite slice '{sliceId}' does not exist.");
        document.Animation.RemoveSlice(sliceId, out var slice);
        return new CommandApplication(new Undo(index, slice), DocumentChange.Empty);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        document.Animation.InsertSlice(undo.Index, undo.Slice);
        return DocumentChange.Empty;
    }

    private sealed record Undo(int Index, SpriteSlice Slice) : IUndoToken;
}

public sealed class SetPivotKeyframeCommand(FrameId frameId, IntPoint pivot) : ICommand
{
    public string Name => "Set Pivot Keyframe";
    public CommandApplication Apply(PixelDocument document) => AnimationTrackCommandHelper.Set(document, frameId, document.Animation.PivotTrack, pivot);
    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken) => AnimationTrackCommandHelper.Revert(document.Animation.PivotTrack, frameId, undoToken);
}

public sealed class ClearPivotKeyframeCommand(FrameId frameId) : ICommand
{
    public string Name => "Clear Pivot Keyframe";
    public CommandApplication Apply(PixelDocument document) => AnimationTrackCommandHelper.Clear(document, frameId, document.Animation.PivotTrack);
    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken) => AnimationTrackCommandHelper.Revert(document.Animation.PivotTrack, frameId, undoToken);
}

public sealed class SetHitboxesKeyframeCommand(FrameId frameId, BoxFrameValue value) : ICommand
{
    public string Name => "Set Hitboxes Keyframe";
    public CommandApplication Apply(PixelDocument document) => AnimationTrackCommandHelper.Set(document, frameId, document.Animation.HitboxTrack, value);
    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken) => AnimationTrackCommandHelper.Revert(document.Animation.HitboxTrack, frameId, undoToken);
}

public sealed class ClearHitboxesKeyframeCommand(FrameId frameId) : ICommand
{
    public string Name => "Clear Hitboxes Keyframe";
    public CommandApplication Apply(PixelDocument document) => AnimationTrackCommandHelper.Clear(document, frameId, document.Animation.HitboxTrack);
    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken) => AnimationTrackCommandHelper.Revert(document.Animation.HitboxTrack, frameId, undoToken);
}

public sealed class SetHurtboxesKeyframeCommand(FrameId frameId, BoxFrameValue value) : ICommand
{
    public string Name => "Set Hurtboxes Keyframe";
    public CommandApplication Apply(PixelDocument document) => AnimationTrackCommandHelper.Set(document, frameId, document.Animation.HurtboxTrack, value);
    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken) => AnimationTrackCommandHelper.Revert(document.Animation.HurtboxTrack, frameId, undoToken);
}

public sealed class ClearHurtboxesKeyframeCommand(FrameId frameId) : ICommand
{
    public string Name => "Clear Hurtboxes Keyframe";
    public CommandApplication Apply(PixelDocument document) => AnimationTrackCommandHelper.Clear(document, frameId, document.Animation.HurtboxTrack);
    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken) => AnimationTrackCommandHelper.Revert(document.Animation.HurtboxTrack, frameId, undoToken);
}

public sealed class SetSocketsKeyframeCommand(FrameId frameId, SocketFrameValue value) : ICommand
{
    public string Name => "Set Sockets Keyframe";
    public CommandApplication Apply(PixelDocument document) => AnimationTrackCommandHelper.Set(document, frameId, document.Animation.SocketTrack, value);
    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken) => AnimationTrackCommandHelper.Revert(document.Animation.SocketTrack, frameId, undoToken);
}

public sealed class ClearSocketsKeyframeCommand(FrameId frameId) : ICommand
{
    public string Name => "Clear Sockets Keyframe";
    public CommandApplication Apply(PixelDocument document) => AnimationTrackCommandHelper.Clear(document, frameId, document.Animation.SocketTrack);
    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken) => AnimationTrackCommandHelper.Revert(document.Animation.SocketTrack, frameId, undoToken);
}

public sealed class SetAnimationEventsKeyframeCommand(FrameId frameId, EventFrameValue value) : ICommand
{
    public string Name => "Set Animation Events Keyframe";
    public CommandApplication Apply(PixelDocument document) => AnimationTrackCommandHelper.Set(document, frameId, document.Animation.EventTrack, value);
    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken) => AnimationTrackCommandHelper.Revert(document.Animation.EventTrack, frameId, undoToken);
}

public sealed class ClearAnimationEventsKeyframeCommand(FrameId frameId) : ICommand
{
    public string Name => "Clear Animation Events Keyframe";
    public CommandApplication Apply(PixelDocument document) => AnimationTrackCommandHelper.Clear(document, frameId, document.Animation.EventTrack);
    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken) => AnimationTrackCommandHelper.Revert(document.Animation.EventTrack, frameId, undoToken);
}

internal static class AnimationTrackCommandHelper
{
    public static CommandApplication Set<T>(PixelDocument document, FrameId frameId, AnimationTrack<T> track, T value)
    {
        document.GetFrame(frameId);
        ArgumentNullException.ThrowIfNull(value);
        var hadPrevious = track.TryGetValue(frameId, out var previous);
        track.Set(frameId, value);
        return new CommandApplication(new TrackUndo<T>(hadPrevious, previous), DocumentChange.Empty);
    }

    public static CommandApplication Clear<T>(PixelDocument document, FrameId frameId, AnimationTrack<T> track)
    {
        document.GetFrame(frameId);
        if (!track.Remove(frameId, out var previous))
            throw new InvalidOperationException($"Animation track '{track.Name}' has no keyframe for frame '{frameId}'.");
        return new CommandApplication(new TrackUndo<T>(true, previous), DocumentChange.Empty);
    }

    public static DocumentChange Revert<T>(AnimationTrack<T> track, FrameId frameId, IUndoToken undoToken)
    {
        if (undoToken is not TrackUndo<T> undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));
        if (undo.HadPrevious)
            track.Restore(frameId, undo.Previous!);
        else
            track.Remove(frameId, out _);
        return DocumentChange.Empty;
    }

    private sealed record TrackUndo<T>(bool HadPrevious, T? Previous) : IUndoToken;
}
