using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private readonly PixelPreviewView _quickPreview = new();
    private readonly WrapPanel _quickPaletteColors = new() { ItemWidth = 28, ItemHeight = 28 };
    private readonly Border _quickPrimary = Swatch();
    private readonly Border _quickSecondary = Swatch();
    private readonly TextBlock _quickZoom = new();
    private readonly TextBlock _quickPaletteHint = new() { TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer _convenienceTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private bool _convenienceInstalled;
    private string? _quickPaletteSignature;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_convenienceInstalled) return;
        _convenienceInstalled = true;

        if (Content is Control shell)
        {
            var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,224") };
            layout.Children.Add(shell);
            var utility = BuildConveniencePanel();
            Grid.SetColumn(utility, 1);
            layout.Children.Add(utility);
            Content = layout;
        }

        _canvas.PointerWheelChanged += OnConvenienceCanvasWheel;
        KeyDown += OnConvenienceKeyDown;
        _convenienceTimer.Tick += OnConvenienceTick;
        _convenienceTimer.Start();
        RefreshConvenienceUi(forcePalette: true);
        Dispatcher.UIThread.Post(FitCanvas, DispatcherPriority.Background);
    }

    protected override void OnClosed(EventArgs e)
    {
        _convenienceTimer.Stop();
        _canvas.PointerWheelChanged -= OnConvenienceCanvasWheel;
        KeyDown -= OnConvenienceKeyDown;
        base.OnClosed(e);
    }

    private Control BuildConveniencePanel()
    {
        _quickPreview.Source = _canvas;
        _quickPreview.Height = 158;
        _quickPreview.HorizontalAlignment = HorizontalAlignment.Stretch;

        _quickPrimary.Width = 32;
        _quickPrimary.Height = 32;
        _quickSecondary.Width = 32;
        _quickSecondary.Height = 32;

        var previewBody = new StackPanel { Spacing = 7 };
        previewBody.Children.Add(_quickPreview);
        _quickZoom.Classes.Add("muted");
        previewBody.Children.Add(_quickZoom);
        previewBody.Children.Add(Icons(
            IconButton("−", "Zoom out", () => ChangeZoom(0.8d)),
            TextIconButton("", "Fit", "Fit canvas in the editor", FitCanvas),
            TextIconButton("", "100%", "Reset zoom to 100%", () => SetZoom(1d)),
            IconButton("＋", "Zoom in", () => ChangeZoom(1.25d))));

        var colorsBody = new StackPanel { Spacing = 8 };
        var activeColors = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,Auto"), ColumnSpacing = 6 };
        activeColors.Children.Add(QuickColorButton(_quickPrimary, "Primary color", true));
        activeColors.Children.Add(Place(QuickColorButton(_quickSecondary, "Secondary color", false), 1));
        activeColors.Children.Add(Place(IconButton("⇄", "Swap primary and secondary colors", SwapColors), 2));
        colorsBody.Children.Add(activeColors);
        colorsBody.Children.Add(_quickPaletteColors);
        _quickPaletteHint.Classes.Add("subtle");
        colorsBody.Children.Add(_quickPaletteHint);

        var toolsBody = new WrapPanel { ItemWidth = 94, ItemHeight = 36 };
        toolsBody.Children.Add(TextIconButton("✎", "Pencil", "Pencil · B", () => SelectQuickTool("core.pencil")));
        toolsBody.Children.Add(TextIconButton("⌫", "Eraser", "Eraser · E", () => SelectQuickTool("core.eraser")));
        toolsBody.Children.Add(TextIconButton("▣", "Fill", "Fill · G", () => SelectQuickTool("core.fill")));
        toolsBody.Children.Add(TextIconButton("▧", "Select", "Selection", SelectQuickSelection));

        var help = new TextBlock
        {
            Text = "Wheel: zoom  ·  Right-click canvas: pick color\nB/E/G: pencil/eraser/fill  ·  F: fit  ·  X: swap colors",
            TextWrapping = TextWrapping.Wrap,
        };
        help.Classes.Add("subtle");

        var stack = new StackPanel { Spacing = 10, Margin = new Thickness(9) };
        stack.Children.Add(SectionCard("Live Preview", "Always shows the whole canvas without the editor grid.", previewBody));
        stack.Children.Add(SectionCard("Quick Colors", "Left-click a palette color for primary. Right-click sets secondary.", colorsBody));
        stack.Children.Add(SectionCard("Quick Tools", null, toolsBody));
        stack.Children.Add(help);

        return new Border
        {
            Background = EditorThemeTokens.Surface,
            BorderBrush = EditorThemeTokens.PanelBorder,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = stack,
            },
        };
    }

    private Button QuickColorButton(Border swatch, string tip, bool primary)
    {
        var button = new Button
        {
            Content = swatch,
            Padding = new Thickness(5),
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        ToolTip.SetTip(button, tip);
        button.Click += async (_, _) => await EditColorAsync(primary);
        return button;
    }

    private void OnConvenienceTick(object? sender, EventArgs e) => RefreshConvenienceUi();

    private void RefreshConvenienceUi(bool forcePalette = false)
    {
        _quickPreview.InvalidateVisual();
        var session = Current();
        if (session is null)
        {
            _quickZoom.Text = "No canvas";
            _quickPaletteColors.Children.Clear();
            _quickPaletteHint.Text = "Create or open a document to use colors.";
            _quickPaletteSignature = null;
            return;
        }

        _quickZoom.Text = $"Zoom {session.Zoom * 100:0}% · F to fit";
        var toolColors = session.GetToolColors();
        _quickPrimary.Background = Brush(toolColors.Primary);
        _quickSecondary.Background = Brush(toolColors.Secondary);

        var editors = session.GetPaletteEditors();
        var signature = string.Join("|", editors.SelectMany((palette, paletteIndex) =>
            palette.Colors.Select(entry => $"{paletteIndex}:{entry.Index}:{entry.Color.R:X2}{entry.Color.G:X2}{entry.Color.B:X2}{entry.Color.A:X2}")));
        if (!forcePalette && signature == _quickPaletteSignature) return;
        _quickPaletteSignature = signature;
        _quickPaletteColors.Children.Clear();

        if (editors.Count == 0)
        {
            _quickPaletteHint.Text = "No palette yet.";
            _quickPaletteColors.Children.Add(TextIconButton("＋", "Create Palette", "Create a 16-color grayscale palette", () =>
            {
                session.AddDefaultPalette();
                RefreshConvenienceUi(forcePalette: true);
            }));
            return;
        }

        var shown = 0;
        foreach (var palette in editors)
        {
            foreach (var entry in palette.Colors)
            {
                if (shown >= 48) break;
                var color = entry.Color;
                var button = new Button
                {
                    Width = 26,
                    Height = 26,
                    Padding = new Thickness(2),
                    Content = new Border
                    {
                        Background = Brush(color),
                        CornerRadius = new CornerRadius(3),
                        BorderBrush = EditorThemeTokens.StrongBorder,
                        BorderThickness = new Thickness(1),
                    },
                };
                ToolTip.SetTip(button, $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2} · left primary · right secondary");
                button.Click += (_, _) =>
                {
                    var current = session.GetToolColors();
                    session.SetToolColors(color, current.Secondary);
                    RefreshConvenienceUi();
                };
                button.PointerPressed += (_, e) =>
                {
                    var point = e.GetCurrentPoint(button);
                    if (!point.Properties.IsRightButtonPressed) return;
                    var current = session.GetToolColors();
                    session.SetToolColors(current.Primary, color);
                    e.Handled = true;
                    RefreshConvenienceUi();
                };
                _quickPaletteColors.Children.Add(button);
                shown++;
            }
            if (shown >= 48) break;
        }

        var total = editors.Sum(value => value.Colors.Count);
        _quickPaletteHint.Text = total > shown
            ? $"Showing {shown} of {total} colors. Full palette editing remains in Inspector → Edit."
            : $"{total} palette color{(total == 1 ? string.Empty : "s")}.";
    }

    private void OnConvenienceCanvasWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.Handled || e.Delta.Y == 0d) return;
        ChangeZoom(e.Delta.Y > 0d ? 1.25d : 0.8d);
        e.Handled = true;
    }

    private void OnConvenienceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || IsEditingText(e.Source)) return;
        switch (e.Key)
        {
            case Key.B:
                SelectQuickTool("core.pencil");
                e.Handled = true;
                break;
            case Key.E:
                SelectQuickTool("core.eraser");
                e.Handled = true;
                break;
            case Key.G:
                SelectQuickTool("core.fill");
                e.Handled = true;
                break;
            case Key.F:
                FitCanvas();
                e.Handled = true;
                break;
            case Key.X:
                SwapColors();
                e.Handled = true;
                break;
            case Key.D1:
                SetZoom(1d);
                e.Handled = true;
                break;
        }
    }

    private static bool IsEditingText(object? source) => source is TextBox or NumericUpDown or ComboBox;

    private void SelectQuickTool(string id)
    {
        var session = Current();
        if (session is null) return;
        Safe(() =>
        {
            _selectionMode = false;
            session.EnsureEditableCel();
            _plugins.SelectTool(session, id);
        });
        RefreshTools();
        RefreshToolOptions();
    }

    private void SelectQuickSelection()
    {
        var session = Current();
        if (session is null) return;
        _selectionMode = true;
        _plugins.CancelTool(session);
        RefreshTools();
        RefreshToolOptions();
    }

    private void FitCanvas()
    {
        var session = Current();
        if (session is null) return;
        var canvas = session.CaptureSnapshot().Canvas.Size;
        if (canvas.Width <= 0 || canvas.Height <= 0) return;

        var availableWidth = Math.Max(240d, Bounds.Width - EditorThemeTokens.ToolRailWidth - EditorThemeTokens.RightPanelWidth - 224d - 120d);
        var availableHeight = Math.Max(220d, Bounds.Height - EditorThemeTokens.TimelineHeight - 170d);
        var zoom = Math.Min(availableWidth / canvas.Width, availableHeight / canvas.Height) * 0.9d;
        SetZoom(Math.Clamp(zoom, 0.125d, 32d));
    }
}

internal sealed class PixelPreviewView : Control
{
    private readonly Dictionary<uint, IBrush> _brushes = [];

    public PixelCanvasView? Source { get; set; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        if (bounds.Width <= 1d || bounds.Height <= 1d) return;

        DrawChecker(context, bounds);
        var presentation = Source?.Presentation;
        if (presentation is null || presentation.Size.Width <= 0 || presentation.Size.Height <= 0)
        {
            context.DrawRectangle(null, new Pen(EditorThemeTokens.StrongBorder, 1d), new Rect(0.5d, 0.5d, Math.Max(0d, bounds.Width - 1d), Math.Max(0d, bounds.Height - 1d)));
            return;
        }

        var padding = 8d;
        var usableWidth = Math.Max(1d, bounds.Width - padding * 2d);
        var usableHeight = Math.Max(1d, bounds.Height - padding * 2d);
        var scale = Math.Min(usableWidth / presentation.Size.Width, usableHeight / presentation.Size.Height);
        var drawWidth = presentation.Size.Width * scale;
        var drawHeight = presentation.Size.Height * scale;
        var originX = (bounds.Width - drawWidth) / 2d;
        var originY = (bounds.Height - drawHeight) / 2d;

        var bytes = presentation.Rgba.Span;
        for (var y = 0; y < presentation.Size.Height; y++)
        for (var x = 0; x < presentation.Size.Width; x++)
        {
            var offset = ((y * presentation.Size.Width) + x) * 4;
            var alpha = bytes[offset + 3];
            if (alpha == 0) continue;
            context.FillRectangle(
                GetBrush(bytes[offset], bytes[offset + 1], bytes[offset + 2], alpha),
                new Rect(originX + x * scale, originY + y * scale, scale, scale));
        }

        foreach (var preview in presentation.PreviewPixels)
        {
            if (preview.Color.A == 0) continue;
            context.FillRectangle(
                GetBrush(preview.Color.R, preview.Color.G, preview.Color.B, preview.Color.A),
                new Rect(originX + preview.Point.X * scale, originY + preview.Point.Y * scale, scale, scale));
        }

        context.DrawRectangle(
            null,
            new Pen(EditorThemeTokens.StrongBorder, 1d),
            new Rect(originX, originY, drawWidth, drawHeight));
    }

    private static void DrawChecker(DrawingContext context, Rect bounds)
    {
        const double cell = 8d;
        var rows = (int)Math.Ceiling(bounds.Height / cell);
        var columns = (int)Math.Ceiling(bounds.Width / cell);
        for (var y = 0; y < rows; y++)
        for (var x = 0; x < columns; x++)
        {
            context.FillRectangle(
                ((x + y) & 1) == 0 ? EditorThemeTokens.CheckerLight : EditorThemeTokens.CheckerDark,
                new Rect(x * cell, y * cell, cell, cell));
        }
    }

    private IBrush GetBrush(byte r, byte g, byte b, byte a)
    {
        var key = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
        if (_brushes.TryGetValue(key, out var brush)) return brush;
        brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        _brushes[key] = brush;
        return brush;
    }
}
