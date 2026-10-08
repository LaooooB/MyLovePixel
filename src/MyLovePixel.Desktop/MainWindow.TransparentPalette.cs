using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MyLovePixel.Core.Pixel;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    // The former 512-swatch install hook is intentionally removed. Transparency
    // remains available through the picker opacity slider and explicit action.
    private Button BuildTransparentColorButton()
    {
        var button = LibraryButton("Transparent", () => ApplyStudioColor(Rgba32.Transparent), "color-transparent");
        ToolTip.SetTip(button, "Use transparent pixels with the current drawing tool.");
        return button;
    }

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
            Data = Geometry.Parse("M2 14L14 2"), Width = 16, Height = 16,
            Stretch = Stretch.Uniform, Stroke = EditorThemeTokens.Danger,
            StrokeThickness = 1.6, StrokeLineCap = PenLineCap.Round,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        Grid.SetRowSpan(slash, 2); Grid.SetColumnSpan(slash, 2); grid.Children.Add(slash);
        return grid;
    }

    private static void AddCheckerCell(Grid grid, int row, int column, IBrush brush)
    {
        var cell = new Border { Background = brush };
        Grid.SetRow(cell, row); Grid.SetColumn(cell, column); grid.Children.Add(cell);
    }
}
