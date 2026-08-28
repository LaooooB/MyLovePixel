using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Selection;

namespace MyLovePixel.Render;

public abstract record RenderOverlayCommand;

public sealed record OverlayLineCommand(
    ViewPoint Start,
    ViewPoint End,
    Rgba32 Color,
    float Thickness) : RenderOverlayCommand;

public sealed record OverlayFillRectCommand(
    ViewRect Rectangle,
    Rgba32 Color) : RenderOverlayCommand;

public sealed class RenderOverlayScene
{
    internal RenderOverlayScene(IReadOnlyList<RenderOverlayCommand> commands)
    {
        Commands = commands;
    }

    public IReadOnlyList<RenderOverlayCommand> Commands { get; }

    public static RenderOverlayScene Empty { get; } =
        new(Array.Empty<RenderOverlayCommand>());
}

public sealed class RenderOverlayBuilder
{
    private readonly List<RenderOverlayCommand> _commands = [];

    public void AddLine(
        ViewPoint start,
        ViewPoint end,
        Rgba32 color,
        float thickness = 1f)
    {
        if (!float.IsFinite(thickness) || thickness <= 0)
            throw new ArgumentOutOfRangeException(nameof(thickness));
        _commands.Add(new OverlayLineCommand(start, end, color, thickness));
    }

    public void AddFillRect(ViewRect rectangle, Rgba32 color)
    {
        if (rectangle.IsEmpty) return;
        _commands.Add(new OverlayFillRectCommand(rectangle, color));
    }

    internal RenderOverlayScene Build() =>
        _commands.Count == 0
            ? RenderOverlayScene.Empty
            : new RenderOverlayScene(Array.AsReadOnly(_commands.ToArray()));
}

public sealed class RenderOverlayContext
{
    public RenderOverlayContext(
        DocumentSnapshot snapshot,
        FrameId frameId,
        ViewTransform view,
        ViewRect? viewport)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        View = view ?? throw new ArgumentNullException(nameof(view));
        if (!snapshot.Frames.ContainsKey(frameId))
            throw new ArgumentException($"Frame '{frameId}' does not exist in the snapshot.", nameof(frameId));

        FrameId = frameId;
        Viewport = viewport;
    }

    public DocumentSnapshot Snapshot { get; }
    public FrameId FrameId { get; }
    public ViewTransform View { get; }
    public ViewRect? Viewport { get; }

    public IntRect VisibleCanvasRegion =>
        View.GetVisibleCanvasRegion(Snapshot.Canvas.Size, Viewport);
}

public interface IRenderOverlayPass
{
    string Id { get; }
    void Build(RenderOverlayContext context, RenderOverlayBuilder builder);
}

public sealed class PixelGridOverlayPass : IRenderOverlayPass
{
    public PixelGridOverlayPass(
        double minimumScale = 4,
        Rgba32? color = null,
        float thickness = 1f)
    {
        if (!double.IsFinite(minimumScale) || minimumScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumScale));
        if (!float.IsFinite(thickness) || thickness <= 0)
            throw new ArgumentOutOfRangeException(nameof(thickness));

        MinimumScale = minimumScale;
        Color = color ?? new Rgba32(0, 0, 0, 72);
        Thickness = thickness;
    }

    public string Id => "core.overlay.pixel-grid";
    public double MinimumScale { get; }
    public Rgba32 Color { get; }
    public float Thickness { get; }

    public void Build(RenderOverlayContext context, RenderOverlayBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(builder);
        if (context.View.Scale < MinimumScale) return;

        var visible = context.VisibleCanvasRegion;
        if (visible.IsEmpty) return;

        var top = context.View.CanvasToView(visible.X, visible.Y);
        var bottom = context.View.CanvasToView(visible.X, visible.Bottom);
        for (var x = visible.X; x < visible.Right; x++)
        {
            var viewX = context.View.CanvasToView(x, 0).X;
            builder.AddLine(
                new ViewPoint(viewX, top.Y),
                new ViewPoint(viewX, bottom.Y),
                Color,
                Thickness);
        }

        var rightBoundaryX = context.View.CanvasToView(visible.Right, 0).X;
        builder.AddLine(
            new ViewPoint(rightBoundaryX, top.Y),
            new ViewPoint(rightBoundaryX, bottom.Y),
            Color,
            Thickness);

        var left = context.View.CanvasToView(visible.X, visible.Y);
        var right = context.View.CanvasToView(visible.Right, visible.Y);
        for (var y = visible.Y; y < visible.Bottom; y++)
        {
            var viewY = context.View.CanvasToView(0, y).Y;
            builder.AddLine(
                new ViewPoint(left.X, viewY),
                new ViewPoint(right.X, viewY),
                Color,
                Thickness);
        }

        var bottomBoundaryY = context.View.CanvasToView(0, visible.Bottom).Y;
        builder.AddLine(
            new ViewPoint(left.X, bottomBoundaryY),
            new ViewPoint(right.X, bottomBoundaryY),
            Color,
            Thickness);
    }
}

public enum GuideOrientation
{
    Vertical = 1,
    Horizontal = 2,
}

public readonly record struct GuideLine(GuideOrientation Orientation, double CanvasCoordinate)
{
    public bool IsValid => double.IsFinite(CanvasCoordinate);
}

public sealed class GuideOverlayPass : IRenderOverlayPass
{
    private readonly GuideLine[] _guides;

    public GuideOverlayPass(
        IEnumerable<GuideLine> guides,
        Rgba32? color = null,
        float thickness = 1f)
    {
        ArgumentNullException.ThrowIfNull(guides);
        _guides = guides.ToArray();
        if (_guides.Any(guide => !guide.IsValid))
            throw new ArgumentException("Guide coordinates must be finite.", nameof(guides));
        if (_guides.Any(guide =>
                guide.Orientation is not GuideOrientation.Vertical and
                not GuideOrientation.Horizontal))
            throw new ArgumentException("Guide orientation is invalid.", nameof(guides));
        if (!float.IsFinite(thickness) || thickness <= 0)
            throw new ArgumentOutOfRangeException(nameof(thickness));

        Color = color ?? new Rgba32(64, 192, 255, 220);
        Thickness = thickness;
    }

    public string Id => "core.overlay.guides";
    public IReadOnlyList<GuideLine> Guides => Array.AsReadOnly(_guides);
    public Rgba32 Color { get; }
    public float Thickness { get; }

    public void Build(RenderOverlayContext context, RenderOverlayBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(builder);

        var visible = context.VisibleCanvasRegion;
        if (visible.IsEmpty) return;

        foreach (var guide in _guides)
        {
            if (guide.Orientation == GuideOrientation.Vertical)
            {
                if (guide.CanvasCoordinate < visible.X || guide.CanvasCoordinate > visible.Right) continue;
                var start = context.View.CanvasToView(guide.CanvasCoordinate, visible.Y);
                var end = context.View.CanvasToView(guide.CanvasCoordinate, visible.Bottom);
                builder.AddLine(start, end, Color, Thickness);
            }
            else
            {
                if (guide.CanvasCoordinate < visible.Y || guide.CanvasCoordinate > visible.Bottom) continue;
                var start = context.View.CanvasToView(visible.X, guide.CanvasCoordinate);
                var end = context.View.CanvasToView(visible.Right, guide.CanvasCoordinate);
                builder.AddLine(start, end, Color, Thickness);
            }
        }
    }
}

public sealed class SelectionOutlineOverlayPass : IRenderOverlayPass
{
    public SelectionOutlineOverlayPass(
        SelectionMask selection,
        Rgba32? color = null,
        float thickness = 1f)
    {
        Selection = selection ?? throw new ArgumentNullException(nameof(selection));
        if (!float.IsFinite(thickness) || thickness <= 0)
            throw new ArgumentOutOfRangeException(nameof(thickness));

        Color = color ?? new Rgba32(255, 255, 255, 255);
        Thickness = thickness;
    }

    public string Id => "core.overlay.selection-outline";
    public SelectionMask Selection { get; }
    public Rgba32 Color { get; }
    public float Thickness { get; }

    public void Build(RenderOverlayContext context, RenderOverlayBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(builder);

        if (Selection.Size != context.Snapshot.Canvas.Size)
            throw new InvalidOperationException("Selection mask size must match the document canvas.");

        var visible = RenderMath.Intersect(
            Selection.Bounds,
            context.VisibleCanvasRegion);
        if (visible.IsEmpty) return;

        for (var y = visible.Y; y < visible.Bottom; y++)
        for (var x = visible.X; x < visible.Right; x++)
        {
            if (!Selection.IsSelected(x, y)) continue;

            if (!IsSelected(x, y - 1))
                AddEdge(context.View, builder, x, y, x + 1, y);
            if (!IsSelected(x + 1, y))
                AddEdge(context.View, builder, x + 1, y, x + 1, y + 1);
            if (!IsSelected(x, y + 1))
                AddEdge(context.View, builder, x + 1, y + 1, x, y + 1);
            if (!IsSelected(x - 1, y))
                AddEdge(context.View, builder, x, y + 1, x, y);
        }
    }

    private bool IsSelected(int x, int y) =>
        (uint)x < (uint)Selection.Size.Width &&
        (uint)y < (uint)Selection.Size.Height &&
        Selection.IsSelected(x, y);

    private void AddEdge(
        ViewTransform view,
        RenderOverlayBuilder builder,
        int x1,
        int y1,
        int x2,
        int y2) =>
        builder.AddLine(
            view.CanvasToView(x1, y1),
            view.CanvasToView(x2, y2),
            Color,
            Thickness);
}

public readonly record struct ToolPreviewPixel(IntPoint Position, Rgba32 Color);

public sealed class ToolPreviewOverlayPass : IRenderOverlayPass
{
    private readonly ToolPreviewPixel[] _pixels;

    public ToolPreviewOverlayPass(IEnumerable<ToolPreviewPixel> pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        _pixels = pixels.ToArray();
    }

    public string Id => "core.overlay.tool-preview";
    public IReadOnlyList<ToolPreviewPixel> Pixels => Array.AsReadOnly(_pixels);

    public void Build(RenderOverlayContext context, RenderOverlayBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(builder);

        var visible = context.VisibleCanvasRegion;
        if (visible.IsEmpty) return;

        foreach (var pixel in _pixels)
        {
            if (pixel.Position.X < visible.X ||
                pixel.Position.Y < visible.Y ||
                pixel.Position.X >= visible.Right ||
                pixel.Position.Y >= visible.Bottom)
                continue;

            builder.AddFillRect(
                context.View.CanvasRectToView(
                    new IntRect(pixel.Position.X, pixel.Position.Y, 1, 1)),
                pixel.Color);
        }
    }
}
