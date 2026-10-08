using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using MyLovePixel.Core.Pixel;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private static Control BuildTransparentSwatchVisual()
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,*"),
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ClipToBounds = true,
        };

        AddCheckerCell(grid, 0, 0, EditorThemeTokens.CheckerLight);
        AddCheckerCell(grid, 0, 1, EditorThemeTokens.CheckerDark);
        AddCheckerCell(grid, 1, 0, EditorThemeTokens.CheckerDark);
        AddCheckerCell(grid, 1, 1, EditorThemeTokens.CheckerLight);

        var slash = new ShapePath
        {
            Data = Geometry.Parse("M2 14L14 2"),
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            Stroke = EditorThemeTokens.Danger,
            StrokeThickness = 1.6,
            StrokeLineCap = PenLineCap.Round,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        Grid.SetRowSpan(slash, 2);
        Grid.SetColumnSpan(slash, 2);
        grid.Children.Add(slash);
        return grid;
    }

    private static void AddCheckerCell(Grid grid, int row, int column, IBrush brush)
    {
        var cell = new Border { Background = brush };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }
}
