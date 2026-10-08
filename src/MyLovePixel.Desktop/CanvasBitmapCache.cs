using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

/// <summary>Owns only a disposable display copy; pointer hover never uploads a new bitmap.</summary>
internal sealed class CanvasBitmapCache : IDisposable
{
    private WriteableBitmap? _bitmap;
    private CanvasPresentation? _source;
    private bool _inverted;

    public void Update(CanvasPresentation? presentation, bool invert = false)
    {
        if (presentation is null) { Dispose(); return; }
        if (ReferenceEquals(presentation, _source) && invert == _inverted && _bitmap is not null) return;
        var width = presentation.Size.Width;
        var height = presentation.Size.Height;
        var expected = checked(width * height * 4);
        if (width <= 0 || height <= 0 || presentation.Rgba.Length != expected)
            throw new InvalidDataException("Invalid canvas display buffer.");
        var bytes = CanvasDisplaySettings.CopyRgbaForDisplay(presentation.Rgba.Span, invert);
        foreach (var pixel in presentation.PreviewPixels)
        {
            if ((uint)pixel.Point.X >= (uint)width || (uint)pixel.Point.Y >= (uint)height) continue;
            var i = (pixel.Point.Y * width + pixel.Point.X) * 4;
            var color = pixel.Color;
            bytes[i] = invert ? (byte)(255 - color.R) : color.R;
            bytes[i + 1] = invert ? (byte)(255 - color.G) : color.G;
            bytes[i + 2] = invert ? (byte)(255 - color.B) : color.B;
            bytes[i + 3] = color.A;
        }
        if (_bitmap is null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        }
        using (var framebuffer = _bitmap.Lock())
            for (var y = 0; y < height; y++)
                Marshal.Copy(bytes, y * width * 4, IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes), width * 4);
        _source = presentation;
        _inverted = invert;
    }

    public void Draw(DrawingContext context, Rect destination)
    {
        if (_bitmap is not null)
            context.DrawImage(_bitmap, new Rect(0, 0, _bitmap.PixelSize.Width, _bitmap.PixelSize.Height), destination);
    }

    public void Dispose()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        _source = null;
    }
}
