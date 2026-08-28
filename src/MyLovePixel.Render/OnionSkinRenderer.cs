using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Render;

public sealed record OnionSkinSettings
{
    public OnionSkinSettings(
        int previousFrames = 1,
        int nextFrames = 1,
        byte opacity = 96,
        double depthFalloff = 0.65,
        Rgba32? previousTint = null,
        Rgba32? nextTint = null)
    {
        if (previousFrames < 0) throw new ArgumentOutOfRangeException(nameof(previousFrames));
        if (nextFrames < 0) throw new ArgumentOutOfRangeException(nameof(nextFrames));
        if (!double.IsFinite(depthFalloff) || depthFalloff < 0d || depthFalloff > 1d)
            throw new ArgumentOutOfRangeException(nameof(depthFalloff));
        PreviousFrames = previousFrames;
        NextFrames = nextFrames;
        Opacity = opacity;
        DepthFalloff = depthFalloff;
        PreviousTint = previousTint ?? new Rgba32(255, 96, 96, 255);
        NextTint = nextTint ?? new Rgba32(96, 255, 128, 255);
    }

    public int PreviousFrames { get; }
    public int NextFrames { get; }
    public byte Opacity { get; }
    public double DepthFalloff { get; }
    public Rgba32 PreviousTint { get; }
    public Rgba32 NextTint { get; }
}

public sealed record OnionSkinRenderResult(
    CpuRenderSurface Surface,
    FrameRenderResult CurrentFrame,
    IReadOnlyList<FrameId> PreviousFrameIds,
    IReadOnlyList<FrameId> NextFrameIds);

public sealed class OnionSkinRenderer
{
    private readonly FrameRenderer _frameRenderer;

    public OnionSkinRenderer(FrameRenderer frameRenderer)
    {
        _frameRenderer = frameRenderer ?? throw new ArgumentNullException(nameof(frameRenderer));
    }

    public OnionSkinRenderResult Render(
        DocumentSnapshot snapshot,
        FrameRenderRequest currentRequest,
        OnionSkinSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(currentRequest);
        var effectiveSettings = settings ?? new OnionSkinSettings();
        var currentIndex = IndexOf(snapshot.FrameOrder, currentRequest.FrameId);
        var previousIds = CollectPrevious(snapshot.FrameOrder, currentIndex, effectiveSettings.PreviousFrames);
        var nextIds = CollectNext(snapshot.FrameOrder, currentIndex, effectiveSettings.NextFrames);
        var target = new CpuRenderTarget(snapshot.Canvas.Size);

        for (var depth = previousIds.Count; depth >= 1; depth--)
        {
            var frameId = previousIds[depth - 1];
            var rendered = _frameRenderer.Render(snapshot, new FrameRenderRequest(frameId));
            CompositeTinted(target, rendered.Surface, effectiveSettings.PreviousTint, CalculateOpacity(effectiveSettings, depth));
        }

        for (var depth = nextIds.Count; depth >= 1; depth--)
        {
            var frameId = nextIds[depth - 1];
            var rendered = _frameRenderer.Render(snapshot, new FrameRenderRequest(frameId));
            CompositeTinted(target, rendered.Surface, effectiveSettings.NextTint, CalculateOpacity(effectiveSettings, depth));
        }

        var current = _frameRenderer.Render(snapshot, currentRequest);
        CompositeUntinted(target, current.Surface);
        return new OnionSkinRenderResult(
            target.Snapshot(),
            current,
            previousIds,
            nextIds);
    }

    private static byte CalculateOpacity(OnionSkinSettings settings, int depth)
    {
        var factor = Math.Pow(settings.DepthFalloff, Math.Max(0, depth - 1));
        var scaled = Math.Clamp((int)Math.Round(settings.Opacity * factor, MidpointRounding.AwayFromZero), 0, 255);
        return (byte)scaled;
    }

    private static void CompositeTinted(
        CpuRenderTarget target,
        CpuRenderSurface source,
        Rgba32 tint,
        byte opacity)
    {
        if (opacity == 0) return;
        for (var y = 0; y < source.Size.Height; y++)
        for (var x = 0; x < source.Size.Width; x++)
        {
            var sourcePixel = source.GetPixel(x, y);
            if (sourcePixel.A == 0) continue;
            var alpha = RenderMath.ScaleByte(sourcePixel.A, opacity);
            if (alpha == 0) continue;
            var tinted = new Rgba32(tint.R, tint.G, tint.B, alpha);
            target.SetPixel(x, y, RenderMath.SourceOver(target.GetPixel(x, y), tinted));
        }
    }

    private static void CompositeUntinted(CpuRenderTarget target, CpuRenderSurface source)
    {
        for (var y = 0; y < source.Size.Height; y++)
        for (var x = 0; x < source.Size.Width; x++)
        {
            var sourcePixel = source.GetPixel(x, y);
            if (sourcePixel.A == 0) continue;
            target.SetPixel(x, y, RenderMath.SourceOver(target.GetPixel(x, y), sourcePixel));
        }
    }

    private static IReadOnlyList<FrameId> CollectPrevious(IReadOnlyList<FrameId> frameOrder, int currentIndex, int count)
    {
        var result = new List<FrameId>(Math.Min(count, currentIndex));
        for (var offset = 1; offset <= count && currentIndex - offset >= 0; offset++)
            result.Add(frameOrder[currentIndex - offset]);
        return result.AsReadOnly();
    }

    private static IReadOnlyList<FrameId> CollectNext(IReadOnlyList<FrameId> frameOrder, int currentIndex, int count)
    {
        var result = new List<FrameId>(Math.Min(count, frameOrder.Count - currentIndex - 1));
        for (var offset = 1; offset <= count && currentIndex + offset < frameOrder.Count; offset++)
            result.Add(frameOrder[currentIndex + offset]);
        return result.AsReadOnly();
    }

    private static int IndexOf(IReadOnlyList<FrameId> values, FrameId value)
    {
        for (var index = 0; index < values.Count; index++)
            if (values[index] == value) return index;
        throw new ArgumentException($"Frame '{value}' does not exist in the snapshot.", nameof(value));
    }
}
