using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

internal sealed class PixelBitmapCache : IDisposable
{
    private byte[] _bytes = [];
    private bool _inverted;
    public WriteableBitmap? Image { get; private set; }

    public void Update(CanvasPresentation? presentation, bool inverted = false)
    {
        if (presentation is null) { Dispose(); return; }
        var size = new PixelSize(presentation.Size.Width, presentation.Size.Height);
        var source = presentation.Rgba.Span;
        if (Image?.PixelSize == size && inverted == _inverted && source.SequenceEqual(_bytes)) return;
        _bytes = source.ToArray();
        _inverted = inverted;
        if (Image?.PixelSize != size)
        {
            Image?.Dispose();
            Image = new WriteableBitmap(size, new Vector(96, 96), Avalonia.Platform.PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        }
        var upload = inverted ? _bytes.ToArray() : _bytes;
        if (inverted)
            for (var i = 0; i < upload.Length; i += 4) { upload[i] = (byte)(255 - upload[i]); upload[i + 1] = (byte)(255 - upload[i + 1]); upload[i + 2] = (byte)(255 - upload[i + 2]); }
        using var locked = Image!.Lock();
        for (var y = 0; y < size.Height; y++)
            Marshal.Copy(upload, y * size.Width * 4, IntPtr.Add(locked.Address, y * locked.RowBytes), size.Width * 4);
    }

    public void Dispose() { Image?.Dispose(); Image = null; _bytes = []; }
}

internal static class PixelBackdrop
{
    public static IBrush Checker { get; } = Build();

    private static IBrush Build()
    {
        var bitmap = new WriteableBitmap(new PixelSize(2, 2), new Vector(96, 96), Avalonia.Platform.PixelFormat.Rgba8888, AlphaFormat.Opaque);
        byte[] pixels = [224, 226, 224, 255, 186, 191, 187, 255, 186, 191, 187, 255, 224, 226, 224, 255];
        using (var locked = bitmap.Lock())
            for (var y = 0; y < 2; y++) Marshal.Copy(pixels, y * 8, IntPtr.Add(locked.Address, y * locked.RowBytes), 8);
        return new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill,
            DestinationRect = new RelativeRect(0, 0, 12, 12, RelativeUnit.Absolute),
        };
    }
}

internal sealed class ColorSwatchView : Control
{
    private Rgba32 _color;
    public Rgba32 Color { get => _color; set { if (_color == value) return; _color = value; InvalidateVisual(); } }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var rect = new Rect(Bounds.Size);
        context.FillRectangle(PixelBackdrop.Checker, rect);
        context.FillRectangle(new SolidColorBrush(Avalonia.Media.Color.FromArgb(_color.A, _color.R, _color.G, _color.B)), rect);
    }
}
