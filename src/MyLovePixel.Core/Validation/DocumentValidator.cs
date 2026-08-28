using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Validation;

public sealed record ValidationIssue(string Code, string Message);

public static class DocumentValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(PixelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = new List<ValidationIssue>();

        var layers = document.LayerOrder.ToHashSet();
        var frames = document.FrameOrder.ToHashSet();
        var frameIndex = document.FrameOrder
            .Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index);
        var occupied = new HashSet<(LayerId Layer, FrameId Frame)>();

        foreach (var frameId in document.FrameOrder)
        {
            if (document.GetFrame(frameId).DurationTicks <= 0)
                issues.Add(new("frame.duration.invalid", $"Frame {frameId} has a non-positive duration."));
        }

        foreach (var cel in document.Cels)
        {
            if (!layers.Contains(cel.LayerId))
                issues.Add(new("cel.layer.missing", $"Cel {cel.Id} references missing layer {cel.LayerId}."));
            if (!frames.Contains(cel.FrameId))
                issues.Add(new("cel.frame.missing", $"Cel {cel.Id} references missing frame {cel.FrameId}."));
            if (!document.Resources.ContainsSurface(cel.SurfaceId))
                issues.Add(new("cel.surface.missing", $"Cel {cel.Id} references missing surface {cel.SurfaceId}."));
            if (!occupied.Add((cel.LayerId, cel.FrameId)))
                issues.Add(new("cel.slot.duplicate", $"More than one Cel occupies layer {cel.LayerId}, frame {cel.FrameId}."));
        }

        ValidateAnimation(document.Animation, frames, frameIndex, issues);
        return issues;
    }

    public static void ThrowIfInvalid(PixelDocument document)
    {
        var issues = Validate(document);
        if (issues.Count == 0) return;
        throw new InvalidOperationException(string.Join(Environment.NewLine, issues.Select(x => $"[{x.Code}] {x.Message}")));
    }

    private static void ValidateAnimation(
        AnimationMetadata animation,
        IReadOnlySet<FrameId> frames,
        IReadOnlyDictionary<FrameId, int> frameIndex,
        List<ValidationIssue> issues)
    {
        var trackIds = new[]
        {
            animation.PivotTrack.Id,
            animation.HitboxTrack.Id,
            animation.HurtboxTrack.Id,
            animation.SocketTrack.Id,
            animation.EventTrack.Id,
        };
        if (trackIds.Distinct().Count() != trackIds.Length)
            issues.Add(new("animation.track.id.duplicate", "Built-in animation tracks must have unique stable IDs."));

        foreach (var clipId in animation.ClipOrder)
        {
            var clip = animation.GetClip(clipId);
            ValidateRange("clip", clip.Id.ToString(), clip.StartFrameId, clip.EndFrameId, frames, frameIndex, issues);
        }

        foreach (var tagId in animation.TagOrder)
        {
            var tag = animation.GetTag(tagId);
            ValidateRange("tag", tag.Id.ToString(), tag.StartFrameId, tag.EndFrameId, frames, frameIndex, issues);
        }

        ValidateTrack("pivot", animation.PivotTrack.Values.Keys, frames, issues);
        ValidateTrack("hitbox", animation.HitboxTrack.Values.Keys, frames, issues);
        ValidateTrack("hurtbox", animation.HurtboxTrack.Values.Keys, frames, issues);
        ValidateTrack("socket", animation.SocketTrack.Values.Keys, frames, issues);
        ValidateTrack("event", animation.EventTrack.Values.Keys, frames, issues);
    }

    private static void ValidateRange(
        string kind,
        string id,
        FrameId start,
        FrameId end,
        IReadOnlySet<FrameId> frames,
        IReadOnlyDictionary<FrameId, int> frameIndex,
        List<ValidationIssue> issues)
    {
        if (!frames.Contains(start))
        {
            issues.Add(new($"animation.{kind}.start.missing", $"Animation {kind} {id} references missing start frame {start}."));
            return;
        }
        if (!frames.Contains(end))
        {
            issues.Add(new($"animation.{kind}.end.missing", $"Animation {kind} {id} references missing end frame {end}."));
            return;
        }
        if (frameIndex[start] > frameIndex[end])
            issues.Add(new($"animation.{kind}.range.invalid", $"Animation {kind} {id} starts after its end frame."));
    }

    private static void ValidateTrack(
        string kind,
        IEnumerable<FrameId> keyFrames,
        IReadOnlySet<FrameId> frames,
        List<ValidationIssue> issues)
    {
        foreach (var frameId in keyFrames)
        {
            if (!frames.Contains(frameId))
                issues.Add(new($"animation.track.{kind}.frame.missing", $"Animation {kind} track references missing frame {frameId}."));
        }
    }
}
