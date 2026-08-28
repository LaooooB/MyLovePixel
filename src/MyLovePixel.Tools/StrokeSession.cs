using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Raster;
using MyLovePixel.Raster.Brush;
using MyLovePixel.Raster.Coordinates;
using MyLovePixel.Raster.Ink;
using MyLovePixel.Raster.Strokes;

namespace MyLovePixel.Tools;

public sealed class StrokeSession
{
    private readonly PixelSurfaceSnapshot _surface;
    private readonly BrushMask _brush;
    private readonly Rgba32 _paint;
    private readonly IInkStrategy _ink;
    private readonly int _spacingPixels;
    private readonly IStrokeFilter _strokeFilter;
    private readonly ICoordinatePolicy _coordinatePolicy;
    private readonly List<IntPoint> _samples = [];
    private bool _completed;

    public StrokeSession(
        long pointerId,
        PixelSurfaceSnapshot surface,
        IntPoint start,
        BrushMask brush,
        Rgba32 paint,
        IInkStrategy ink,
        int spacingPixels = 1,
        IStrokeFilter? strokeFilter = null,
        ICoordinatePolicy? coordinatePolicy = null)
    {
        if (spacingPixels <= 0) throw new ArgumentOutOfRangeException(nameof(spacingPixels));
        PointerId = pointerId;
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _brush = brush ?? throw new ArgumentNullException(nameof(brush));
        _paint = paint;
        _ink = ink ?? throw new ArgumentNullException(nameof(ink));
        _spacingPixels = spacingPixels;
        _strokeFilter = strokeFilter ?? IdentityStrokeFilter.Instance;
        _coordinatePolicy = coordinatePolicy ?? ClipCoordinatePolicy.Instance;
        _samples.Add(start);
        Preview = BuildPreview();
    }

    public long PointerId { get; }
    public long StartRevision => _surface.Revision;
    public IReadOnlyList<IntPoint> Samples => _samples.AsReadOnly();
    public RasterPatch Preview { get; private set; }
    public bool IsCompleted => _completed;

    public RasterPatch Update(IntPoint point)
    {
        EnsureActive();
        if (_samples[^1] != point)
            _samples.Add(point);
        Preview = BuildPreview();
        return Preview;
    }

    public RasterPatch Complete(IntPoint point)
    {
        EnsureActive();
        if (_samples[^1] != point)
            _samples.Add(point);
        Preview = BuildPreview();
        _completed = true;
        return Preview;
    }

    public void Cancel()
    {
        if (_completed) return;
        _completed = true;
        Preview = RasterPatch.Empty;
    }

    private RasterPatch BuildPreview()
    {
        var points = BrushStrokeRasterizer.Rasterize(
            _samples,
            _brush,
            _spacingPixels,
            _strokeFilter);
        return RasterPatchBuilder.Build(
            _surface,
            points,
            _paint,
            _ink,
            _coordinatePolicy);
    }

    private void EnsureActive()
    {
        if (_completed) throw new InvalidOperationException("Stroke session is already completed.");
    }
}
