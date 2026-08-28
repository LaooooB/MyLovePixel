using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Render;

public enum TextureUploadMode
{
    None = 0,
    Full = 1,
    Partial = 2,
}

public sealed class TextureUploadPlan
{
    private TextureUploadPlan(
        TextureUploadMode mode,
        IReadOnlyList<IntRect> regions,
        int pixelCount)
    {
        Mode = mode;
        Regions = regions;
        PixelCount = pixelCount;
    }

    public TextureUploadMode Mode { get; }
    public IReadOnlyList<IntRect> Regions { get; }
    public int PixelCount { get; }

    public static TextureUploadPlan None { get; } =
        new(TextureUploadMode.None, Array.Empty<IntRect>(), 0);

    public static TextureUploadPlan Full(IntSize size)
    {
        var region = RenderMath.Bounds(size);
        return new TextureUploadPlan(
            TextureUploadMode.Full,
            Array.AsReadOnly(new[] { region }),
            checked(size.Width * size.Height));
    }

    public static TextureUploadPlan Partial(IReadOnlyList<IntRect> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);
        if (regions.Count == 0) return None;

        var copy = regions.ToArray();
        var pixels = 0;
        foreach (var region in copy)
        {
            if (region.IsEmpty)
                throw new ArgumentException("Upload regions cannot contain empty rectangles.", nameof(regions));
            pixels = checked(pixels + checked(region.Width * region.Height));
        }

        return new TextureUploadPlan(
            TextureUploadMode.Partial,
            Array.AsReadOnly(copy),
            pixels);
    }
}

public static class TextureUploadPlanner
{
    public static TextureUploadPlan Plan(
        RenderCacheOutcome outcome,
        IntSize size,
        IReadOnlyList<IntRect> dirtyRegions) =>
        outcome switch
        {
            RenderCacheOutcome.FullRecompose => TextureUploadPlan.Full(size),
            RenderCacheOutcome.PartialRecompose => TextureUploadPlan.Partial(dirtyRegions),
            RenderCacheOutcome.CacheHit => TextureUploadPlan.None,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
}
