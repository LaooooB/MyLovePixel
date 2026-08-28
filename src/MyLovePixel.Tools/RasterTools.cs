using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Raster;
using MyLovePixel.Raster.Brush;
using MyLovePixel.Raster.Color;
using MyLovePixel.Raster.Fill;
using MyLovePixel.Raster.Geometry;
using MyLovePixel.Raster.Ink;
using MyLovePixel.Raster.Strokes;

namespace MyLovePixel.Tools;

public abstract class StrokeToolBase : ITool
{
    private StrokeSession? _session;

    protected StrokeToolBase(ToolDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public ToolDescriptor Descriptor { get; }
    public bool IsInteracting => _session is not null;

    protected abstract Rgba32 ResolvePaint(ToolContext context);
    protected abstract string CommandName { get; }

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
                var localStart = context.Target.CanvasToSurface(pointerEvent.CanvasPixel);
                _session = new StrokeSession(
                    pointerEvent.PointerId,
                    surface,
                    localStart,
                    BrushMask.Square(options.GetInteger(ToolOptionIds.BrushSize)),
                    ResolvePaint(context),
                    SimpleInkStrategy.Instance,
                    options.GetInteger(ToolOptionIds.Spacing),
                    options.GetBoolean(ToolOptionIds.PixelPerfect)
                        ? PixelPerfectStrokeFilter.Instance
                        : IdentityStrokeFilter.Instance);
                return PreviewResult(context, _session.Preview);

            case PointerEventKind.Moved:
                if (_session is null || _session.PointerId != pointerEvent.PointerId)
                    return ToolDispatchResult.Ignored;
                return PreviewResult(
                    context,
                    _session.Update(context.Target.CanvasToSurface(pointerEvent.CanvasPixel)));

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
        if (_session is null) return ToolDispatchResult.Cleared;
        _session.Cancel();
        _session = null;
        return ToolDispatchResult.Cleared;
    }

    private static ToolDispatchResult PreviewResult(ToolContext context, RasterPatch patch) =>
        new(true, new ToolPreview(context.Target.SurfaceId, patch), false);
}

public sealed class PencilTool : StrokeToolBase
{
    public PencilTool()
        : base(ToolDescriptors.Pencil)
    {
    }

    protected override Rgba32 ResolvePaint(ToolContext context) => context.PrimaryColor;
    protected override string CommandName => "Pencil Stroke";
}

public sealed class EraserTool : StrokeToolBase
{
    public EraserTool()
        : base(ToolDescriptors.Eraser)
    {
    }

    protected override Rgba32 ResolvePaint(ToolContext context) => Rgba32.Transparent;
    protected override string CommandName => "Eraser Stroke";
}

public sealed class LineTool : ITool
{
    private LineSession? _session;

    public ToolDescriptor Descriptor => ToolDescriptors.Line;
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
                _session = new LineSession(
                    pointerEvent.PointerId,
                    context.CaptureTargetSurface(),
                    context.Target.CanvasToSurface(pointerEvent.CanvasPixel),
                    BrushMask.Square(options.GetInteger(ToolOptionIds.BrushSize)),
                    context.PrimaryColor,
                    options.GetBoolean(ToolOptionIds.PixelPerfect));
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
                var finalPatch = session.Build(
                    context.Target.CanvasToSurface(pointerEvent.CanvasPixel),
                    pointerEvent.Modifiers);
                var committed = context.CommitPatch(finalPatch, session.StartRevision, "Line");
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

    private static ToolDispatchResult BuildResult(ToolContext context, LineSession session, PointerEvent pointerEvent)
    {
        var patch = session.Build(
            context.Target.CanvasToSurface(pointerEvent.CanvasPixel),
            pointerEvent.Modifiers);
        return new ToolDispatchResult(true, new ToolPreview(context.Target.SurfaceId, patch), false);
    }

    private sealed class LineSession
    {
        public LineSession(
            long pointerId,
            PixelSurfaceSnapshot surface,
            IntPoint start,
            BrushMask brush,
            Rgba32 paint,
            bool pixelPerfect)
        {
            PointerId = pointerId;
            Surface = surface;
            Start = start;
            Brush = brush;
            Paint = paint;
            PixelPerfect = pixelPerfect;
        }

        public long PointerId { get; }
        public PixelSurfaceSnapshot Surface { get; }
        public long StartRevision => Surface.Revision;
        public IntPoint Start { get; }
        public BrushMask Brush { get; }
        public Rgba32 Paint { get; }
        public bool PixelPerfect { get; }

        public RasterPatch Build(IntPoint end, KeyModifiers modifiers)
        {
            var constrainedEnd = ToolGeometry.ConstrainLine(Start, end, modifiers);
            var points = BrushStrokeRasterizer.Rasterize(
                new[] { Start, constrainedEnd },
                Brush,
                1,
                PixelPerfect ? PixelPerfectStrokeFilter.Instance : IdentityStrokeFilter.Instance);
            return RasterPatchBuilder.Build(Surface, points, Paint, SimpleInkStrategy.Instance);
        }
    }
}

public sealed class ShapeTool : ITool
{
    private ShapeSession? _session;

    public ToolDescriptor Descriptor => ToolDescriptors.Shape;
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
                _session = new ShapeSession(
                    pointerEvent.PointerId,
                    context.CaptureTargetSurface(),
                    context.Target.CanvasToSurface(pointerEvent.CanvasPixel),
                    options.GetEnum(ToolOptionIds.ShapeKind),
                    options.GetBoolean(ToolOptionIds.Filled),
                    BrushMask.Square(options.GetInteger(ToolOptionIds.BrushSize)),
                    context.PrimaryColor);
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
                var patch = session.Build(
                    context.Target.CanvasToSurface(pointerEvent.CanvasPixel),
                    pointerEvent.Modifiers);
                var committed = context.CommitPatch(patch, session.StartRevision, "Shape");
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

    private static ToolDispatchResult BuildResult(ToolContext context, ShapeSession session, PointerEvent pointerEvent)
    {
        var patch = session.Build(
            context.Target.CanvasToSurface(pointerEvent.CanvasPixel),
            pointerEvent.Modifiers);
        return new ToolDispatchResult(true, new ToolPreview(context.Target.SurfaceId, patch), false);
    }

    private sealed class ShapeSession
    {
        public ShapeSession(
            long pointerId,
            PixelSurfaceSnapshot surface,
            IntPoint start,
            string shapeKind,
            bool filled,
            BrushMask brush,
            Rgba32 paint)
        {
            PointerId = pointerId;
            Surface = surface;
            Start = start;
            ShapeKind = shapeKind;
            Filled = filled;
            Brush = brush;
            Paint = paint;
        }

        public long PointerId { get; }
        public PixelSurfaceSnapshot Surface { get; }
        public long StartRevision => Surface.Revision;
        public IntPoint Start { get; }
        public string ShapeKind { get; }
        public bool Filled { get; }
        public BrushMask Brush { get; }
        public Rgba32 Paint { get; }

        public RasterPatch Build(IntPoint end, KeyModifiers modifiers)
        {
            var constrainedEnd = ToolGeometry.ConstrainShapeEnd(Start, end, modifiers);
            var bounds = ToolGeometry.InclusiveBounds(Start, constrainedEnd);
            IReadOnlyList<IntPoint> basePoints = ShapeKind switch
            {
                ToolOptionValues.Rectangle => RectangleRasterizer.Rasterize(bounds, Filled),
                ToolOptionValues.Ellipse => EllipseRasterizer.Rasterize(bounds, Filled),
                _ => throw new InvalidOperationException($"Unsupported shape kind '{ShapeKind}'."),
            };

            IEnumerable<IntPoint> points = basePoints;
            if (!Filled && Brush.Size != new IntSize(1, 1))
                points = basePoints.SelectMany(Brush.Stamp).Distinct();

            return RasterPatchBuilder.Build(Surface, points, Paint, SimpleInkStrategy.Instance);
        }
    }
}

public sealed class FillTool : ITool
{
    public ToolDescriptor Descriptor => ToolDescriptors.Fill;
    public bool IsInteracting => false;

    public ToolDispatchResult HandlePointer(ToolContext context, ToolOptions options, PointerEvent pointerEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        if (pointerEvent.Kind != PointerEventKind.Pressed ||
            (pointerEvent.Buttons & PointerButtons.Primary) == 0)
            return ToolDispatchResult.Ignored;

        var surface = context.CaptureTargetSurface();
        var seed = context.Target.CanvasToSurface(pointerEvent.CanvasPixel);
        var toleranceValue = options.GetInteger(ToolOptionIds.Tolerance);
        IColorToleranceStrategy tolerance = toleranceValue == 0
            ? ExactColorTolerance.Instance
            : new MaxChannelColorTolerance((byte)toleranceValue);
        var patch = FloodFillRasterizer.BuildPatch(
            surface,
            seed,
            context.PrimaryColor,
            SimpleInkStrategy.Instance,
            tolerance,
            context.WorkBudget);
        var committed = context.CommitPatch(patch, surface.Revision, "Fill");
        return new ToolDispatchResult(true, null, committed);
    }

    public ToolDispatchResult Cancel(ToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ToolDispatchResult.Cleared;
    }
}

public static class ToolOptionIds
{
    public const string BrushSize = "brush-size";
    public const string Spacing = "spacing";
    public const string PixelPerfect = "pixel-perfect";
    public const string ShapeKind = "shape-kind";
    public const string Filled = "filled";
    public const string Tolerance = "tolerance";
}

public static class ToolOptionValues
{
    public const string Rectangle = "rectangle";
    public const string Ellipse = "ellipse";
}

public static class ToolDescriptors
{
    private static ToolOptionSchema StrokeOptions() => new(
    [
        ToolOptionDefinition.Integer(ToolOptionIds.BrushSize, "Brush Size", 1, 1, 64),
        ToolOptionDefinition.Integer(ToolOptionIds.Spacing, "Spacing", 1, 1, 64),
        ToolOptionDefinition.Boolean(ToolOptionIds.PixelPerfect, "Pixel Perfect", false),
    ]);

    public static ToolDescriptor Pencil { get; } = new("core.pencil", "Pencil", StrokeOptions());
    public static ToolDescriptor Eraser { get; } = new("core.eraser", "Eraser", StrokeOptions());
    public static ToolDescriptor Line { get; } = new(
        "core.line",
        "Line",
        new ToolOptionSchema(
        [
            ToolOptionDefinition.Integer(ToolOptionIds.BrushSize, "Brush Size", 1, 1, 64),
            ToolOptionDefinition.Boolean(ToolOptionIds.PixelPerfect, "Pixel Perfect", false),
        ]));
    public static ToolDescriptor Shape { get; } = new(
        "core.shape",
        "Shape",
        new ToolOptionSchema(
        [
            ToolOptionDefinition.Enum(
                ToolOptionIds.ShapeKind,
                "Shape",
                ToolOptionValues.Rectangle,
                ToolOptionValues.Rectangle,
                ToolOptionValues.Ellipse),
            ToolOptionDefinition.Boolean(ToolOptionIds.Filled, "Filled", false),
            ToolOptionDefinition.Integer(ToolOptionIds.BrushSize, "Brush Size", 1, 1, 64),
        ]));
    public static ToolDescriptor Fill { get; } = new(
        "core.fill",
        "Fill",
        new ToolOptionSchema(
        [
            ToolOptionDefinition.Integer(ToolOptionIds.Tolerance, "Tolerance", 0, 0, 255),
        ]));
}

internal static class ToolGeometry
{
    public static IntPoint ConstrainLine(IntPoint start, IntPoint end, KeyModifiers modifiers)
    {
        if ((modifiers & KeyModifiers.Shift) == 0) return end;

        var dx = checked(end.X - start.X);
        var dy = checked(end.Y - start.Y);
        var absX = Math.Abs((long)dx);
        var absY = Math.Abs((long)dy);
        if (absX == 0 && absY == 0) return end;

        if (absX >= absY * 2)
            return new IntPoint(end.X, start.Y);
        if (absY >= absX * 2)
            return new IntPoint(start.X, end.Y);

        var length = checked((int)Math.Max(absX, absY));
        var xSign = dx < 0 ? -1 : 1;
        var ySign = dy < 0 ? -1 : 1;
        return new IntPoint(
            checked(start.X + (xSign * length)),
            checked(start.Y + (ySign * length)));
    }

    public static IntPoint ConstrainShapeEnd(IntPoint start, IntPoint end, KeyModifiers modifiers)
    {
        if ((modifiers & KeyModifiers.Shift) == 0) return end;

        var dx = checked(end.X - start.X);
        var dy = checked(end.Y - start.Y);
        var side = checked((int)Math.Max(Math.Abs((long)dx), Math.Abs((long)dy)));
        var xSign = dx < 0 ? -1 : 1;
        var ySign = dy < 0 ? -1 : 1;
        return new IntPoint(
            checked(start.X + (xSign * side)),
            checked(start.Y + (ySign * side)));
    }

    public static IntRect InclusiveBounds(IntPoint a, IntPoint b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.X, b.X);
        var bottom = Math.Max(a.Y, b.Y);
        return new IntRect(
            x,
            y,
            checked(right - x + 1),
            checked(bottom - y + 1));
    }
}
