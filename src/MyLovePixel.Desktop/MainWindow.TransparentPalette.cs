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
    private bool _transparentPaletteInstalled;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Dispatcher.UIThread.Post(InstallTransparentPaletteSwatch, DispatcherPriority.Background);
    }

    private void InstallTransparentPaletteSwatch()
    {
        if (_transparentPaletteInstalled || _studioPaletteSwatches.Children.Count < 128) return;
        _transparentPaletteInstalled = true;

        var button = new Button
        {
            Width = 18,
            Height = 18,
            MinHeight = 18,
            Padding = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            BorderBrush = EditorThemeTokens.Accent,
            BorderThickness = new Thickness(1),
            Content = BuildTransparentSwatchVisual(),
        };
        ToolTip.SetTip(button, "Transparent · erase with Pencil, Line, Fill, Arc or Shape");
        button.Click += (_, _) => ApplyStudioColor(Rgba32.Transparent);
        _studioPaletteSwatches.Children.Insert(0, button);
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
