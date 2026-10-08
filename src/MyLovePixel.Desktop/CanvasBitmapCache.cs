using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

/// <summary>Retains image storage across navigation; previews update only changed pixels.</summary>
internal sealed partial class CanvasBitmapCache : IDisposable
{
    private WriteableBitmap? _bitmap;
    private CanvasPresentation? _source;
    private ReadOnlyMemory<byte> _sourcePixels;
    private Dictionary<int, int> _preview = [];
    private bool _inverted;
    public long FullUploadCount { get; private set; }
    public long PreviewPixelWriteCount { get; private set; }

    public void Update(CanvasPresentation? presentation, bool invert = false)
    {
        if (presentation is null) { Dispose(); return; }
        if (ReferenceEquals(presentation, _source) && invert == _inverted && _bitmap is not null) return;
        var width = presentation.Size.Width;
        var height = presentation.Size.Height;
        if (width <= 0 || height <= 0 || presentation.Rgba.Length != checked(width * height * 4))
            throw new InvalidDataException("Invalid canvas display buffer.");
        var resized = _bitmap is null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height;
        var fullUpload = resized || invert != _inverted || !presentation.Rgba.Equals(_sourcePixels);
        var nextPreview = new Dictionary<int, int>(presentation.PreviewPixels.Count);
        foreach (var pixel in presentation.PreviewPixels)
        {
            if ((uint)pixel.Point.X >= (uint)width || (uint)pixel.Point.Y >= (uint)height) continue;
            var c = pixel.Color;
            nextPreview[pixel.Point.Y * width + pixel.Point.X] = Pack(c.R, c.G, c.B, c.A, invert);
        }
        if (resized)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        }
        var previewChanged = nextPreview.Count != _preview.Count || nextPreview.Any(p => !_preview.TryGetValue(p.Key, out var value) || value != p.Value);
        if (fullUpload || previewChanged)
        {
            InvalidateResolutions();
            using var framebuffer = _bitmap!.Lock();
            if (fullUpload)
            {
                ReadOnlyMemory<byte> display = invert
                    ? CanvasDisplaySettings.CopyRgbaForDisplay(presentation.Rgba.Span, true)
                    : presentation.Rgba;
                if (!MemoryMarshal.TryGetArray(display, out ArraySegment<byte> segment)) segment = new ArraySegment<byte>(display.ToArray());
                if (framebuffer.RowBytes == width * 4)
                    Marshal.Copy(segment.Array!, segment.Offset, framebuffer.Address, display.Length);
                else
                    for (var y = 0; y < height; y++)
                        Marshal.Copy(segment.Array!, segment.Offset + y * width * 4,
                            IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes), width * 4);
                FullUploadCount++;
            }
            else
            {
                var rgba = presentation.Rgba.Span;
                foreach (var previous in _preview)
                {
                    if (nextPreview.ContainsKey(previous.Key)) continue;
                    var i = previous.Key * 4;
                    WritePixel(framebuffer, width, previous.Key, Pack(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3], invert));
                }
            }
            foreach (var pixel in nextPreview)
                if (fullUpload || !_preview.TryGetValue(pixel.Key, out var old) || old != pixel.Value)
                    WritePixel(framebuffer, width, pixel.Key, pixel.Value);
        }
        _source = presentation;
        _sourcePixels = presentation.Rgba;
        _inverted = invert;
        _preview = nextPreview;
    }

    private void WritePixel(ILockedFramebuffer framebuffer, int width, int index, int value)
    {
        var offset = index / width * framebuffer.RowBytes + index % width * 4;
        Marshal.WriteInt32(framebuffer.Address, offset, value);
        PreviewPixelWriteCount++;
    }

    private static int Pack(byte r, byte g, byte b, byte a, bool invert)
    {
        if (invert) { r = (byte)(255 - r); g = (byte)(255 - g); b = (byte)(255 - b); }
        return BitConverter.IsLittleEndian ? r | g << 8 | b << 16 | a << 24 : a | b << 8 | g << 16 | r << 24;
    }

    public void Draw(DrawingContext context, Rect destination, Rect? visible = null, double renderScaling = 1d)
    {
        if (_bitmap is null || destination.Width <= 0 || destination.Height <= 0) return;
        var clipped = visible is { } viewport ? destination.Intersect(viewport) : destination;
        if (clipped.Width <= 0 || clipped.Height <= 0) return;
        var bitmap = SelectResolution(destination, renderScaling);
        var sx = bitmap.PixelSize.Width / destination.Width;
        var sy = bitmap.PixelSize.Height / destination.Height;
        context.DrawImage(bitmap,
            new Rect((clipped.X - destination.X) * sx, (clipped.Y - destination.Y) * sy, clipped.Width * sx, clipped.Height * sy), clipped);
    }

    public void Dispose()
    {
        InvalidateResolutions();
        _bitmap?.Dispose(); _bitmap = null; _source = null; _sourcePixels = default; _preview.Clear();
    }
}
