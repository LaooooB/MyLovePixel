using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using HsvColor = MyLovePixel.Application.HsvColor;

namespace MyLovePixel.Desktop;

public class ColorPickerDialog : Window
{
    private HsvColor _hsv;
    private bool _syncing;
    private readonly ColorSpectrumPad _pad = new() { Height = 214, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Slider _hue = new() { Minimum = 0, Maximum = 359.99, SmallChange = 1, LargeChange = 15 };
    private readonly Slider _alpha = new() { Minimum = 0, Maximum = 255, SmallChange = 1, LargeChange = 16 };
    private readonly TextBox _hex = new() { MaxLength = 9 };
    private readonly Border _preview = new() { Height = 32, CornerRadius = new CornerRadius(4) };
    private readonly TextBlock _hint = new() { FontSize = 11 };
    private readonly Button _apply = new() { Content = "Apply", MinWidth = 86 };
    private readonly NumericUpDown[] _rgb = Enumerable.Range(0, 3).Select(_ => new NumericUpDown
    {
        Minimum = 0, Maximum = 255, Increment = 1, FormatString = "0", MinWidth = 76,
    }).ToArray();

    public ColorPickerDialog(Rgba32 initial)
    {
        Title = "Color palette";
        Width = 450;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EditorThemeTokens.AppBackground;
        _hsv = HsvColor.FromRgba(initial);
        var root = new StackPanel { Margin = new Thickness(16), Spacing = 7 };
        root.Children.Add(new TextBlock { Text = "Color palette", FontSize = 16, FontWeight = FontWeight.SemiBold });
        root.Children.Add(DialogChrome.Help("Drag the circle to pick a color. Use the hue strip to change the color family."));
        root.Children.Add(_pad);
        root.Children.Add(new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = Rainbow() });
        root.Children.Add(DialogChrome.Labeled("Hue", _hue, 46));
        root.Children.Add(DialogChrome.Labeled("Alpha", _alpha, 46));
        var entry = new Grid { ColumnDefinitions = new ColumnDefinitions("*,66"), ColumnSpacing = 8 };
        entry.Children.Add(_hex); Grid.SetColumn(_preview, 1); entry.Children.Add(_preview);
        root.Children.Add(entry);
        var rgb = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 7 };
        for (var i = 0; i < 3; i++)
        {
            var index = i;
            var labeled = DialogChrome.Labeled(new[] { "R", "G", "B" }[i], _rgb[i], 12);
            Grid.SetColumn(labeled, i); rgb.Children.Add(labeled);
            _rgb[index].ValueChanged += (_, _) =>
            {
                if (_syncing) return;
                SetColor(new Rgba32((byte)(_rgb[0].Value ?? 0), (byte)(_rgb[1].Value ?? 0), (byte)(_rgb[2].Value ?? 0), _hsv.Alpha));
            };
        }
        root.Children.Add(rgb);
        root.Children.Add(_hint);
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8, Margin = new Thickness(0, 6, 0, 0) };
        var transparent = new Button { Content = "Transparent" };
        transparent.Click += (_, _) => { _hsv = _hsv with { Alpha = 0 }; Sync(); };
        actions.Children.Add(transparent);
        var cancel = new Button { Content = "Cancel", MinWidth = 76 };
        cancel.Click += (_, _) => Close(null);
        Grid.SetColumn(cancel, 1); actions.Children.Add(cancel);
        Grid.SetColumn(_apply, 2); actions.Children.Add(_apply); _apply.Classes.Add("primary");
        _apply.Click += (_, _) => AcceptColor();
        root.Children.Add(actions);
        Content = root;
        _pad.Changed += (saturation, value) => { _hsv = _hsv with { Saturation = saturation, Value = value }; Sync(); };
        _hue.ValueChanged += (_, _) => { if (!_syncing) { _hsv = _hsv with { Hue = _hue.Value }; Sync(); } };
        _alpha.ValueChanged += (_, _) => { if (!_syncing) { _hsv = _hsv with { Alpha = (byte)Math.Round(_alpha.Value) }; Sync(); } };
        _hex.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            var valid = HexColor.TryParse(_hex.Text, out var color);
            _apply.IsEnabled = valid;
            _hint.Text = valid ? "HEX uses RRGGBBAA. Alpha 0 = transparent." : "Enter #RRGGBB or #RRGGBBAA.";
            if (valid) SetColor(color, keepText: true);
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(null); e.Handled = true; }
            else if (e.Key == Key.Enter) { AcceptColor(); e.Handled = true; }
        };
        AutomationProperties.SetAutomationId(_pad, "color-spectrum");
        AutomationProperties.SetName(_pad, "Saturation and brightness. Drag or use arrow keys.");
        AutomationProperties.SetAutomationId(_hue, "color-hue");
        AutomationProperties.SetAutomationId(_alpha, "color-alpha");
        AutomationProperties.SetAutomationId(_hex, "color-picker-hex");
        AutomationProperties.SetAutomationId(_apply, "color-picker-apply");
        Sync();
    }

    private void AcceptColor()
    {
        // Read the entry itself: a final TextChanged may still be queued when Enter/Apply arrives.
        if (HexColor.TryParse(_hex.Text, out var color)) Close(color);
        else { _apply.IsEnabled = false; _hint.Text = "Enter #RRGGBB or #RRGGBBAA."; }
    }

    private void SetColor(Rgba32 color, bool keepText = false)
    {
        var next = HsvColor.FromRgba(color);
        // Retain the chosen hue while moving through grey/black, rather than jumping to red.
        _hsv = next.Saturation == 0 ? next with { Hue = _hsv.Hue } : next;
        Sync(keepText);
    }
    private void Sync(bool keepText = false)
    {
        _syncing = true;
        try
        {
            var color = _hsv.ToRgba();
            _hue.Value = _hsv.Hue;
            _alpha.Value = _hsv.Alpha;
            if (!keepText) _hex.Text = HexColor.Format(color);
            _rgb[0].Value = color.R; _rgb[1].Value = color.G; _rgb[2].Value = color.B;
            _preview.Background = new SolidColorBrush(Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
            _hint.Text = $"Alpha {_hsv.Alpha}/255 · HEX uses RRGGBBAA.";
            _apply.IsEnabled = true;
            _pad.Value = _hsv;
        }
        finally { _syncing = false; }
    }

    private static LinearGradientBrush Rainbow()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        };
        for (var index = 0; index <= 6; index++)
        {
            var color = new HsvColor(index * 60, 1, 1).ToRgba();
            brush.GradientStops.Add(new GradientStop(Avalonia.Media.Color.FromRgb(color.R, color.G, color.B), index / 6d));
        }
        return brush;
    }

}

internal sealed class ColorSpectrumPad : Control
{
    private HsvColor _value;
    private bool _dragging;
    public ColorSpectrumPad() { Focusable = true; Cursor = new Cursor(StandardCursorType.Cross); }
    public HsvColor Value { get => _value; set { _value = value; InvalidateVisual(); } }
    public event Action<double, double>? Changed;
    private Rect Area => new(8, 8, Math.Max(1, Bounds.Width - 16), Math.Max(1, Bounds.Height - 16));

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var hue = new HsvColor(Value.Hue, 1, 1).ToRgba();
        var saturation = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = new GradientStops { new(Colors.White, 0), new(Avalonia.Media.Color.FromRgb(hue.R, hue.G, hue.B), 1) },
        };
        var brightness = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops { new(Colors.Transparent, 0), new(Colors.Black, 1) },
        };
        context.DrawRectangle(saturation, null, Area, 4, 4);
        context.DrawRectangle(brightness, new Pen(EditorThemeTokens.StrongBorder, 1), Area, 4, 4);
        var center = new Point(Area.X + Value.Saturation * Area.Width, Area.Y + (1 - Value.Value) * Area.Height);
        context.DrawEllipse(null, new Pen(Brushes.Black, 4), center, 5, 5);
        context.DrawEllipse(null, new Pen(Brushes.White, 2), center, 5, 5);
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(); _dragging = true; e.Pointer.Capture(this); Pick(e.GetPosition(this)); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        Pick(e.GetPosition(this)); e.Handled = true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        Pick(e.GetPosition(this)); _dragging = false; e.Pointer.Capture(null); e.Handled = true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { _dragging = false; base.OnPointerCaptureLost(e); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? .1 : .01;
        var (s, v) = (Value.Saturation, Value.Value);
        switch (e.Key)
        {
            case Key.Left: s -= step; break; case Key.Right: s += step; break;
            case Key.Up: v += step; break; case Key.Down: v -= step; break;
            default: return;
        }
        Changed?.Invoke(Math.Clamp(s, 0, 1), Math.Clamp(v, 0, 1)); e.Handled = true;
    }
    private void Pick(Point point) => Changed?.Invoke(Math.Clamp((point.X - Area.X) / Area.Width, 0, 1), Math.Clamp(1 - (point.Y - Area.Y) / Area.Height, 0, 1));
}
