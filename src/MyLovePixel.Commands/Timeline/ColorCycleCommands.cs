using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Commands.Timeline;

public sealed class SetColorCyclesKeyframeCommand(FrameId frameId, ColorCycleFrameValue value) : ICommand
{
    public string Name => "Set Color Cycles Keyframe";

    public CommandApplication Apply(PixelDocument document)
    {
        ArgumentNullException.ThrowIfNull(value);
        Validate(document, value);
        return AnimationTrackCommandHelper.Set(document, frameId, document.Animation.ColorCycleTrack, value);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken) =>
        AnimationTrackCommandHelper.Revert(document.Animation.ColorCycleTrack, frameId, undoToken);

    private static void Validate(PixelDocument document, ColorCycleFrameValue frameValue)
    {
        foreach (var cycle in frameValue.Cycles)
        {
            if (!document.Resources.ContainsPalette(cycle.PaletteId))
                throw new InvalidOperationException($"Color cycle references missing palette '{cycle.PaletteId}'.");
            var palette = document.Resources.GetPalette(cycle.PaletteId);
            if (cycle.EndIndex >= palette.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"Color cycle range {cycle.StartIndex}..{cycle.EndIndex} exceeds palette '{cycle.PaletteId}' with {palette.Count} entries.");
            if (palette.TransparentIndex is { } transparentIndex &&
                transparentIndex >= cycle.StartIndex && transparentIndex <= cycle.EndIndex)
                throw new ArgumentException(
                    $"Color cycle cannot include transparent palette index {transparentIndex}.",
                    nameof(value));
        }
    }
}

public sealed class ClearColorCyclesKeyframeCommand(FrameId frameId) : ICommand
{
    public string Name => "Clear Color Cycles Keyframe";

    public CommandApplication Apply(PixelDocument document) =>
        AnimationTrackCommandHelper.Clear(document, frameId, document.Animation.ColorCycleTrack);

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken) =>
        AnimationTrackCommandHelper.Revert(document.Animation.ColorCycleTrack, frameId, undoToken);
}
