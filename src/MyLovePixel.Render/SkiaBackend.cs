using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using SkiaSharp;

namespace MyLovePixel.Render;

public sealed class SkiaFrameCache : IDisposable
{
    private readonly Dictionary<SkiaFrameCacheKey, SKBitmap> _bitmaps = [];
    private bool _disposed;

    public int Count => _bitmaps.Count;

    /// <summary>
    /// Returns a cache-owned bitmap. Callers may draw/read it, but must not dispose it.
    /// The cache owns the native lifetime and releases bitmaps through ClearCaches/Dispose.
    /// </summary>
    public SKBitmap Update(
        DocumentId documentId,
        FrameId frameId,
        FrameRenderResult result)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(result);

        var key = new SkiaFrameCacheKey(documentId, frameId);
        var requiresFullUpload = false;

        if (!_bitmaps.TryGetValue(key, out var bitmap) ||
            bitmap.Width != result.Surface.Size.Width ||
            bitmap.Height != result.Surface.Size.Height)
        {
            bitmap?.Dispose();
            bitmap = CreateBitmap(result.Surface.Size);
            _bitmaps[key] = bitmap;
            requiresFullUpload = true;
        }

        if (requiresFullUpload ||
            result.UploadPlan.Mode == TextureUploadMode.Full)
        {
            UploadFull(bitmap, result.Surface);
        }
        else if (result.UploadPlan.Mode == TextureUploadMode.Partial)
        {
            foreach (var region in result.UploadPlan.Regions)
                UploadRegion(bitmap, result.Surface, region);
        }

        return bitmap;
    }

    public void ClearCaches()
    {
        foreach (var bitmap in _bitmaps.Values)
            bitmap.Dispose();
        _bitmaps.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        ClearCaches();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static SKBitmap CreateBitmap(IntSize size)
    {
        var info = new SKImageInfo(
            size.Width,
            size.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);
        if (!bitmap.ReadyToDraw)
        {
            bitmap.Dispose();
            throw new InvalidOperationException("SkiaSharp failed to allocate the frame bitmap.");
        }
        return bitmap;
    }

    private static void UploadFull(
        SKBitmap bitmap,
        CpuRenderSurface source) =>
        UploadRegion(
            bitmap,
            source,
            new IntRect(0, 0, source.Size.Width, source.Size.Height));

    private static void UploadRegion(
        SKBitmap bitmap,
        CpuRenderSurface source,
        IntRect region)
    {
        var clipped = RenderMath.Intersect(
            region,
            RenderMath.Bounds(source.Size));
        if (clipped.IsEmpty) return;

        if (bitmap.Width != source.Size.Width ||
            bitmap.Height != source.Size.Height)
            throw new ArgumentException("Bitmap and CPU surface sizes must match.", nameof(bitmap));

        var sourceBytes = source.Bytes.Span;
        var bytesPerRow = checked(source.Size.Width * 4);
        var copyLength = checked(clipped.Width * 4);

        for (var y = clipped.Y; y < clipped.Bottom; y++)
        {
            var sourceOffset = checked((y * bytesPerRow) + (clipped.X * 4));
            var sourceRow = sourceBytes.Slice(sourceOffset, copyLength);
            var destinationRow = bitmap.GetPixelSpan(clipped.X, y);
            sourceRow.CopyTo(destinationRow[..copyLength]);
        }

        bitmap.NotifyPixelsChanged();
    }

    private readonly record struct SkiaFrameCacheKey(
        DocumentId DocumentId,
        FrameId FrameId);
}

public sealed class SkiaCanvasPresenter
{
    public static SKSamplingOptions NearestSampling { get; } =
        new(SKFilterMode.Nearest, SKMipmapMode.None);

    public void Draw(
        SKCanvas canvas,
        SKBitmap bitmap,
        ViewTransform view,
        RenderOverlayScene overlays)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(overlays);

        var topLeft = view.CanvasToView(0, 0);
        var bottomRight = view.CanvasToView(bitmap.Width, bitmap.Height);
        var destination = new SKRect(
            ToFloat(topLeft.X),
            ToFloat(topLeft.Y),
            ToFloat(bottomRight.X),
            ToFloat(bottomRight.Y));

        canvas.DrawBitmap(bitmap, destination, NearestSampling);
        DrawOverlays(canvas, overlays);
    }

    private static void DrawOverlays(
        SKCanvas canvas,
        RenderOverlayScene overlays)
    {
        using var paint = new SKPaint
        {
            IsAntialias = false,
        };

        foreach (var command in overlays.Commands)
        {
            switch (command)
            {
                case OverlayLineCommand line:
                    paint.Style = SKPaintStyle.Stroke;
                    paint.Color = ToSkColor(line.Color);
                    paint.StrokeWidth = line.Thickness;
                    canvas.DrawLine(
                        ToFloat(line.Start.X),
                        ToFloat(line.Start.Y),
                        ToFloat(line.End.X),
                        ToFloat(line.End.Y),
                        paint);
                    break;

                case OverlayFillRectCommand fill:
                    paint.Style = SKPaintStyle.Fill;
                    paint.Color = ToSkColor(fill.Color);
                    var rectangle = fill.Rectangle;
                    canvas.DrawRect(
                        new SKRect(
                            ToFloat(rectangle.X),
                            ToFloat(rectangle.Y),
                            ToFloat(rectangle.Right),
                            ToFloat(rectangle.Bottom)),
                        paint);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Overlay command '{command.GetType().Name}' is not supported by the Skia presenter.");
            }
        }
    }

    private static SKColor ToSkColor(Rgba32 color) =>
        new(color.R, color.G, color.B, color.A);

    private static float ToFloat(double value)
    {
        if (!double.IsFinite(value) ||
            value < -float.MaxValue ||
            value > float.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value));
        return (float)value;
    }
}
