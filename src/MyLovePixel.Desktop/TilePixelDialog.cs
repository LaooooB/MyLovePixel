using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Export;

namespace MyLovePixel.Desktop;

public sealed class TilePixelDialog : Window
{
    private readonly DocumentSession _session;
    private readonly TilesetId _tilesetId;
    private readonly TileId _tileId;
    private readonly TilePixelEditorView _view = new();
    private readonly WrapPanel _paletteBar = new() { ItemWidth = 30, ItemHeight = 30, Margin = new Thickness(8, 0, 8, 8) };
    private byte _index;

    public TilePixelDialog(DocumentSession session, TilesetId tilesetId, TileId tileId)
    {
        _session = session;
        _tilesetId = tilesetId;
        _tileId = tileId;
        Title = "Edit Tile Pixels";
        Width = 580;
        Height = 650;
        MinWidth = 380;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;

        _view.PixelPressed = Paint;
        var root = new DockPanel();

        var header = new StackPanel { Spacing = 4, Margin = new Thickness(12, 10, 12, 8) };
        header.Children.Add(new TextBlock { Text = "Tile pixel editor", FontSize = 15, FontWeight = FontWeight.SemiBold });
        header.Children.Add(DialogChrome.Help("Click a pixel to paint. Indexed tiles use the palette below; RGBA tiles use the current primary color."));
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        DockPanel.SetDock(_paletteBar, Dock.Top);
        root.Children.Add(_paletteBar);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12, 8),
        };
        footer.Children.Add(DialogChrome.TextButton("Done", Close, primary: true));
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        root.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border
            {
                Background = EditorThemeTokens.CanvasFrame,
                Margin = new Thickness(12),
                Padding = new Thickness(12),
                Child = _view,
            },
        });
        Content = root;
        RefreshView();
    }

    private void Paint(int x, int y)
    {
        try
        {
            var surface = _session.GetTileSurface(_tilesetId, _tileId);
            if (surface.Format == PixelFormat.Rgba32)
                _session.SetTilePixel(_tilesetId, _tileId, x, y, _session.GetToolColors().Primary);
            else
                _session.SetIndexedTilePixel(_tilesetId, _tileId, x, y, _index);
            RefreshView();
        }
        catch
        {
            // The parent editor owns persistent error reporting for tile mutations.
        }
    }

    private void RefreshView()
    {
        var surface = _session.GetTileSurface(_tilesetId, _tileId);
        IReadOnlyList<Rgba32>? palette = null;
        if (surface.PaletteId is { } paletteId)
        {
            var editor = _session.GetPaletteEditors().FirstOrDefault(value => value.Id == paletteId);
            if (editor is not null)
            {
                palette = editor.Colors.Select(value => value.Color).ToArray();
                if (_index >= palette.Count) _index = 0;
            }
        }

        _paletteBar.Children.Clear();
        _paletteBar.IsVisible = surface.Format == PixelFormat.Indexed8 && palette is not null;
        if (_paletteBar.IsVisible && palette is not null)
        {
            for (var i = 0; i < palette.Count; i++)
            {
                var index = checked((byte)i);
                var color = palette[i];
                var swatch = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B)),
                    CornerRadius = new CornerRadius(3),
                };
                var button = new Button { Width = 28, Height = 28, Padding = new Thickness(2), Content = swatch };
                ToolTip.SetTip(button, $"Palette index {index} · click to select");
                if (_index == index) button.Classes.Add("selected");
                button.Click += (_, _) => { _index = index; RefreshView(); };
                _paletteBar.Children.Add(button);
            }
        }
        _view.SetSurface(surface, palette);
    }
}

internal sealed class TilePixelEditorView : Control
{
    private TileSurfacePresentation? _surface;
    private IReadOnlyList<Rgba32>? _palette;
    private double _zoom = 20;

    public Action<int, int>? PixelPressed { get; set; }

    public void SetSurface(TileSurfacePresentation surface, IReadOnlyList<Rgba32>? palette)
    {
        _surface = surface;
        _palette = palette;
        _zoom = Math.Clamp(360d / Math.Max(surface.Size.Width, surface.Size.Height), 4d, 28d);
        Width = surface.Size.Width * _zoom;
        Height = surface.Size.Height * _zoom;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var surface = _surface;
        if (surface is null) return;
        var bytes = surface.Bytes.Span;
        for (var y = 0; y < surface.Size.Height; y++)
        for (var x = 0; x < surface.Size.Width; x++)
        {
            Rgba32 color;
            if (surface.Format == PixelFormat.Rgba32)
            {
                var offset = ((y * surface.Size.Width) + x) * 4;
                color = new Rgba32(bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]);
            }
            else
            {
                var index = bytes[(y * surface.Size.Width) + x];
                color = _palette is not null && index < _palette.Count ? _palette[index] : Rgba32.Transparent;
            }

            var rect = new Rect(x * _zoom, y * _zoom, _zoom, _zoom);
            if (color.A == 0)
                context.FillRectangle(((x + y) & 1) == 0 ? EditorThemeTokens.CheckerLight : EditorThemeTokens.CheckerDark, rect);
            else
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B)), rect);
        }

        if (_zoom >= 8)
        {
            var pen = new Pen(EditorThemeTokens.GridLine, 1);
            for (var x = 1; x < surface.Size.Width; x++) context.DrawLine(pen, new Point(x * _zoom, 0), new Point(x * _zoom, Height));
            for (var y = 1; y < surface.Size.Height; y++) context.DrawLine(pen, new Point(0, y * _zoom), new Point(Width, y * _zoom));
        }
        context.DrawRectangle(null, new Pen(EditorThemeTokens.StrongBorder, 1), new Rect(0, 0, Width, Height));
    }

    protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_surface is null) return;
        var point = e.GetPosition(this);
        var x = (int)Math.Floor(point.X / _zoom);
        var y = (int)Math.Floor(point.Y / _zoom);
        if ((uint)x < (uint)_surface.Size.Width && (uint)y < (uint)_surface.Size.Height)
            PixelPressed?.Invoke(x, y);
        e.Handled = true;
    }
}
