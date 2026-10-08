using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

internal static class CanvasBackdrop
{
    private static readonly Dictionary<int, IBrush> Checkers = [];
    public static IBrush Solid(Rgba32 color) => new SolidColorBrush(Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B));

    public static IBrush Create(CanvasDisplaySettings settings)
    {
        var key = Math.Clamp(settings.BackgroundBrightness, 0, 100);
        lock (Checkers)
        {
            if (Checkers.TryGetValue(key, out var cached)) return cached;
            var cell = (int)CanvasDisplaySettings.CheckerCellSize;
            var side = cell * 2;
            var bytes = new byte[side * side * 4];
            for (var y = 0; y < side; y++)
            for (var x = 0; x < side; x++)
            {
                var c = (x / cell + y / cell) % 2 == 0 ? settings.CheckerLight : settings.CheckerDark;
                var i = (y * side + x) * 4;
                bytes[i] = c.R; bytes[i + 1] = c.G; bytes[i + 2] = c.B; bytes[i + 3] = 255;
            }
            // A tiny pre-rasterized tile, as in 5572f97, avoids creating a
            // vector drawing render target during each canvas paint/pan frame.
            var bitmap = new WriteableBitmap(new PixelSize(side, side), new Vector(96, 96), Avalonia.Platform.PixelFormat.Rgba8888, AlphaFormat.Opaque);
            using (var buffer = bitmap.Lock())
                for (var y = 0; y < side; y++)
                    Marshal.Copy(bytes, y * side * 4, IntPtr.Add(buffer.Address, y * buffer.RowBytes), side * 4);
            var brush = new ImageBrush(bitmap)
            {
                TileMode = TileMode.Tile, Stretch = Stretch.Fill,
                AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top,
                DestinationRect = new RelativeRect(0, 0, side, side, RelativeUnit.Absolute),
            };
            Checkers.Add(key, brush);
            return brush;
        }
    }
}
