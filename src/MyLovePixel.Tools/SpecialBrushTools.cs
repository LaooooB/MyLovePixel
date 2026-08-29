using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Raster;
using MyLovePixel.Raster.Brush;
using MyLovePixel.Raster.Ink;
using MyLovePixel.Raster.Strokes;

namespace MyLovePixel.Tools;

public static class SpecialToolOptionIds
{
    public const string Strength = "strength";
    public const string Radius = "radius";
    public const string Bend = "bend";
}

public static class SpecialToolDescriptors
{
    private static ToolOptionSchema ModifierOptions(int defaultStrength = 30) => new(
    [
        ToolOptionDefinition.Integer(ToolOptionIds.BrushSize, "Brush Size", 3, 1, 64),
        ToolOptionDefinition.Integer(ToolOptionIds.Spacing, "Spacing", 1, 1, 64),
        ToolOptionDefinition.Integer(SpecialToolOptionIds.Strength, "Strength", defaultStrength, 1, 100),
    ]);

    public static ToolDescriptor Arc { get; } = new(
        "core.arc",
        "Arc",
        new ToolOptionSchema(
        [
            ToolOptionDefinition.Integer(ToolOptionIds.BrushSize, "Brush Size", 1, 1, 64),
            ToolOptionDefinition.Integer(SpecialToolOptionIds.Bend, "Bend", 50, -150, 150),
        ]));

    public static ToolDescriptor Blur { get; } = new(
        "core.blur",
        "Blur Brush",
        new ToolOptionSchema(
        [
            ToolOptionDefinition.Integer(ToolOptionIds.BrushSize, "Brush Size", 3, 1, 64),
            ToolOptionDefinition.Integer(ToolOptionIds.Spacing, "Spacing", 1, 1, 64),
            ToolOptionDefinition.Integer(SpecialToolOptionIds.Radius, "Radius", 1, 1, 4),
            ToolOptionDefinition.Integer(SpecialToolOptionIds.Strength, "Strength", 40, 1, 100),
        ]));

    public static ToolDescriptor Fade { get; } = new("core.fade", "Fade Brush", ModifierOptions(25));
    public static ToolDescriptor Shadow { get; } = new("core.shadow", "Shadow Brush", ModifierOptions(30));
    public static ToolDescriptor Highlight { get; } = new("core.highlight", "Highlight Brush", ModifierOptions(25));
}

public sealed class ArcTool : ITool
{
    private ArcSession? _session;

    public ToolDescriptor Descriptor => SpecialToolDescriptors.Arc;
    public bool IsInteracting => _session is not null;

    public ToolDispatchResult HandlePointer(ToolContext context, ToolOptions options, PointerEvent pointerEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        switch (pointerEvent.Kind)
        {
            case PointerEventKind.Pressed:
                if (_session is not null || (pointerEvent.Buttons & PointerButtons.Primary) == 0)
                    return ToolDispatchResult.Ignored;
                _session = new ArcSession(
                    pointerEvent.PointerId,
                    context.CaptureTargetSurface(),
                    context.Target.CanvasToSurface(pointerEvent.CanvasPixel),
                    BrushMask.Square(options.GetInteger(ToolOptionIds.BrushSize)),
                    context.PrimaryColor,
                    options.GetInteger(SpecialToolOptionIds.Bend));
                return BuildResult(context, _session, pointerEvent);

            case PointerEventKind.Moved:
                if (_session is null || _session.PointerId != pointerEvent.PointerId)
                    return ToolDispatchResult.Ignored;
                return BuildResult(context, _session, pointerEvent);

            case PointerEventKind.Released:
                if (_session is null || _session.PointerId != pointerEvent.PointerId)
                    return ToolDispatchResult.Ignored;
                var session = _session;
                _session = null;
                var patch = session.Build(context.Target.CanvasToSurface(pointerEvent.CanvasPixel));
                var committed = context.CommitPatch(patch, session.StartRevision, "Arc");
                return new ToolDispatchResult(true, null, committed);

            default:
                return ToolDispatchResult.Ignored;
        }
    }

    public ToolDispatchResult Cancel(ToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _session = null;
        return ToolDispatchResult.Cleared;
    }

    private static ToolDispatchResult BuildResult(ToolContext context, ArcSession session, PointerEvent pointerEvent) =>
        new(true, new ToolPreview(context.Target.SurfaceId, session.Build(context.Target.CanvasToSurface(pointerEvent.CanvasPixel))), false);

    private sealed class ArcSession
    {
        public ArcSession(long pointerId, PixelSurfaceSnapshot surface, IntPoint start, BrushMask brush, Rgba32 paint, int bend)
        {
            PointerId = pointerId;
            Surface = surface;
            Start = start;
            Brush = brush;
            Paint = paint;
            Bend = bend;
        }

        public long PointerId { get; }
        public PixelSurfaceSnapshot Surface { get; }
        public long StartRevision => Surface.Revision;
        public IntPoint Start { get; }
        public BrushMask Brush { get; }
        public Rgba32 Paint { get; }
        public int Bend { get; }

        public RasterPatch Build(IntPoint end)
        {
            if (end == Start)
                return RasterPatchBuilder.Build(Surface, Brush.Stamp(Start), Paint, SimpleInkStrategy.Instance);

            var dx = end.X - Start.X;
            var dy = end.Y - Start.Y;
            var distance = Math.Sqrt((double)dx * dx + (double)dy * dy);
            var midpointX = (Start.X + end.X) / 2d;
            var midpointY = (Start.Y + end.Y) / 2d;
            var normalX = -dy / distance;
            var normalY = dx / distance;
            var offset = distance * Bend / 100d;
            var controlX = midpointX + normalX * offset;
            var controlY = midpointY + normalY * offset;
            var steps = Math.Clamp((int)Math.Ceiling(distance * 2d), 8, 4096);
            var samples = new List<IntPoint>(steps + 1);
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (double)steps;
                var oneMinusT = 1d - t;
                var x = oneMinusT * oneMinusT * Start.X + 2d * oneMinusT * t * controlX + t * t * end.X;
                var y = oneMinusT * oneMinusT * Start.Y + 2d * oneMinusT * t * controlY + t * t * end.Y;
                samples.Add(new IntPoint((int)Math.Round(x), (int)Math.Round(y)));
            }

            var points = BrushStrokeRasterizer.Rasterize(samples, Brush, 1, IdentityStrokeFilter.Instance);
            return RasterPatchBuilder.Build(Surface, points, Paint, SimpleInkStrategy.Instance);
        }
    }
}

public abstract class ModifierStrokeToolBase : ITool
{
    private ModifierStrokeSession? _session;

    protected ModifierStrokeToolBase(ToolDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public ToolDescriptor Descriptor { get; }
    public bool IsInteracting => _session is not null;

    protected abstract string CommandName { get; }
    protected abstract Rgba32 Transform(PixelSurfaceSnapshot surface, IntPoint point, ToolOptions options);

    public ToolDispatchResult HandlePointer(ToolContext context, ToolOptions options, PointerEvent pointerEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        switch (pointerEvent.Kind)
        {
            case PointerEventKind.Pressed:
                if (_session is not null || (pointerEvent.Buttons & PointerButtons.Primary) == 0)
                    return ToolDispatchResult.Ignored;
                var surface = context.CaptureTargetSurface();
                _session = new ModifierStrokeSession(
                    pointerEvent.PointerId,
                    surface,
                    context.Target.CanvasToSurface(pointerEvent.CanvasPixel),
                    BrushMask.Square(options.GetInteger(ToolOptionIds.BrushSize)),
                    options.GetInteger(ToolOptionIds.Spacing),
                    point => Transform(surface, point, options));
                return PreviewResult(context, _session.Preview);

            case PointerEventKind.Moved:
                if (_session is null || _session.PointerId != pointerEvent.PointerId)
                    return ToolDispatchResult.Ignored;
                return PreviewResult(context, _session.Update(context.Target.CanvasToSurface(pointerEvent.CanvasPixel)));

            case PointerEventKind.Released:
                if (_session is null || _session.PointerId != pointerEvent.PointerId)
                    return ToolDispatchResult.Ignored;
                var session = _session;
                _session = null;
                var patch = session.Complete(context.Target.CanvasToSurface(pointerEvent.CanvasPixel));
                var committed = context.CommitPatch(patch, session.StartRevision, CommandName);
                return new ToolDispatchResult(true, null, committed);

            default:
                return ToolDispatchResult.Ignored;
        }
    }

    public ToolDispatchResult Cancel(ToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _session = null;
        return ToolDispatchResult.Cleared;
    }

    private static ToolDispatchResult PreviewResult(ToolContext context, RasterPatch patch) =>
        new(true, new ToolPreview(context.Target.SurfaceId, patch), false);
}

internal sealed class ModifierStrokeSession
{
    private readonly PixelSurfaceSnapshot _surface;
    private readonly BrushMask _brush;
    private readonly int _spacing;
    private readonly Func<IntPoint, Rgba32> _transform;
    private readonly List<IntPoint> _samples = [];

    public ModifierStrokeSession(
        long pointerId,
        PixelSurfaceSnapshot surface,
        IntPoint start,
        BrushMask brush,
        int spacing,
        Func<IntPoint, Rgba32> transform)
    {
        PointerId = pointerId;
        _surface = surface;
        _brush = brush;
        _spacing = spacing;
        _transform = transform;
        _samples.Add(start);
        Preview = Build();
    }

    public long PointerId { get; }
    public long StartRevision => _surface.Revision;
    public RasterPatch Preview { get; private set; }

    public RasterPatch Update(IntPoint point)
    {
        if (_samples[^1] != point) _samples.Add(point);
        Preview = Build();
        return Preview;
    }

    public RasterPatch Complete(IntPoint point)
    {
        if (_samples[^1] != point) _samples.Add(point);
        Preview = Build();
        return Preview;
    }

    private RasterPatch Build()
    {
        var points = BrushStrokeRasterizer.Rasterize(_samples, _brush, _spacing, IdentityStrokeFilter.Instance);
        var writes = new List<PixelWrite>();
        var seen = new HashSet<IntPoint>();
        var dirty = default(IntRect);
        foreach (var point in points)
        {
            if ((uint)point.X >= (uint)_surface.Size.Width || (uint)point.Y >= (uint)_surface.Size.Height || !seen.Add(point))
                continue;
            var destination = _surface.GetPixel(point.X, point.Y);
            var next = _transform(point);
            if (next == destination) continue;
            writes.Add(new PixelWrite(point.X, point.Y, next));
            dirty = IntRect.Union(dirty, IntRect.FromPoint(point.X, point.Y));
        }
        return writes.Count == 0 ? RasterPatch.Empty : new RasterPatch(writes.ToArray(), dirty);
    }
}

public sealed class BlurBrushTool : ModifierStrokeToolBase
{
    public BlurBrushTool() : base(SpecialToolDescriptors.Blur) { }
    protected override string CommandName => "Blur Brush";

    protected override Rgba32 Transform(PixelSurfaceSnapshot surface, IntPoint point, ToolOptions options)
    {
        var original = surface.GetPixel(point.X, point.Y);
        var radius = options.GetInteger(SpecialToolOptionIds.Radius);
        var strength = options.GetInteger(SpecialToolOptionIds.Strength);
        long premulR = 0, premulG = 0, premulB = 0, alphaSum = 0, count = 0;
        for (var y = Math.Max(0, point.Y - radius); y <= Math.Min(surface.Size.Height - 1, point.Y + radius); y++)
        for (var x = Math.Max(0, point.X - radius); x <= Math.Min(surface.Size.Width - 1, point.X + radius); x++)
        {
            var color = surface.GetPixel(x, y);
            premulR += color.R * color.A;
            premulG += color.G * color.A;
            premulB += color.B * color.A;
            alphaSum += color.A;
            count++;
        }
        if (count == 0 || alphaSum == 0) return original;
        var averageAlpha = (byte)(alphaSum / count);
        var average = new Rgba32(
            (byte)(premulR / alphaSum),
            (byte)(premulG / alphaSum),
            (byte)(premulB / alphaSum),
            averageAlpha);
        return Blend(original, average, strength);
    }

    private static Rgba32 Blend(Rgba32 from, Rgba32 to, int strength)
    {
        var inverse = 100 - strength;
        return new Rgba32(
            (byte)((from.R * inverse + to.R * strength + 50) / 100),
            (byte)((from.G * inverse + to.G * strength + 50) / 100),
            (byte)((from.B * inverse + to.B * strength + 50) / 100),
            (byte)((from.A * inverse + to.A * strength + 50) / 100));
    }
}

public sealed class FadeBrushTool : ModifierStrokeToolBase
{
    public FadeBrushTool() : base(SpecialToolDescriptors.Fade) { }
    protected override string CommandName => "Fade Brush";

    protected override Rgba32 Transform(PixelSurfaceSnapshot surface, IntPoint point, ToolOptions options)
    {
        var original = surface.GetPixel(point.X, point.Y);
        if (original.A == 0) return original;
        var strength = options.GetInteger(SpecialToolOptionIds.Strength);
        return new Rgba32(original.R, original.G, original.B, (byte)((original.A * (100 - strength) + 50) / 100));
    }
}

public sealed class ShadowBrushTool : ModifierStrokeToolBase
{
    public ShadowBrushTool() : base(SpecialToolDescriptors.Shadow) { }
    protected override string CommandName => "Shadow Brush";

    protected override Rgba32 Transform(PixelSurfaceSnapshot surface, IntPoint point, ToolOptions options)
    {
        var original = surface.GetPixel(point.X, point.Y);
        if (original.A == 0) return original;
        var factor = 100 - options.GetInteger(SpecialToolOptionIds.Strength);
        return new Rgba32(
            (byte)((original.R * factor + 50) / 100),
            (byte)((original.G * factor + 50) / 100),
            (byte)((original.B * factor + 50) / 100),
            original.A);
    }
}

public sealed class HighlightBrushTool : ModifierStrokeToolBase
{
    public HighlightBrushTool() : base(SpecialToolDescriptors.Highlight) { }
    protected override string CommandName => "Highlight Brush";

    protected override Rgba32 Transform(PixelSurfaceSnapshot surface, IntPoint point, ToolOptions options)
    {
        var original = surface.GetPixel(point.X, point.Y);
        if (original.A == 0) return original;
        var strength = options.GetInteger(SpecialToolOptionIds.Strength);
        return new Rgba32(
            (byte)(original.R + ((255 - original.R) * strength + 50) / 100),
            (byte)(original.G + ((255 - original.G) * strength + 50) / 100),
            (byte)(original.B + ((255 - original.B) * strength + 50) / 100),
            original.A);
    }
}
