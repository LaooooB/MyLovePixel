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

        return issues;
    }

    public static void ThrowIfInvalid(PixelDocument document)
    {
        var issues = Validate(document);
        if (issues.Count == 0) return;
        throw new InvalidOperationException(string.Join(Environment.NewLine, issues.Select(x => $"[{x.Code}] {x.Message}")));
    }
}
