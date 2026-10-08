using Avalonia;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

internal static class CanvasBackdrop
{
    public static IBrush Solid(Rgba32 color) => new SolidColorBrush(Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B));

    public static IBrush Create(CanvasDisplaySettings settings)
    {
        var cell = CanvasDisplaySettings.CheckerCellSize;
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing
        {
            Brush = Solid(settings.CheckerLight), Geometry = new RectangleGeometry(new Rect(0, 0, cell * 2, cell * 2)),
        });
        foreach (var rect in new[] { new Rect(cell, 0, cell, cell), new Rect(0, cell, cell, cell) })
            drawing.Children.Add(new GeometryDrawing { Brush = Solid(settings.CheckerDark), Geometry = new RectangleGeometry(rect) });
        return new DrawingBrush
        {
            Drawing = drawing, TileMode = TileMode.Tile, Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top,
            DestinationRect = new RelativeRect(0, 0, cell * 2, cell * 2, RelativeUnit.Absolute),
        };
    }
}
