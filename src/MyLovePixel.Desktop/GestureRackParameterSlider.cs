using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace MyLovePixel.Desktop;

internal sealed class GestureRackParameterSlider : Border
{
    private readonly Slider _slider;
    private readonly TextBlock _valueText;
    private readonly Action<int> _changed;
    private readonly int _minimum;
    private readonly int _maximum;
    private int _lastValue;
    private bool _hovered;
    private bool _focused;

    public GestureRackParameterSlider(
        string label,
        int value,
        int minimum,
        int maximum,
        Action<int> changed)
    {
        if (minimum > maximum) throw new ArgumentOutOfRangeException(nameof(minimum));
        ArgumentNullException.ThrowIfNull(changed);

        _minimum = minimum;
        _maximum = maximum;
        _lastValue = Math.Clamp(value, minimum, maximum);
        _changed = changed;

        Height = 34;
        MinWidth = 180;
        Padding = new Thickness(8, 2, 7, 2);
        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        Background = EditorThemeTokens.Surface;
        BorderBrush = EditorThemeTokens.PanelBorder;

        _slider = new Slider
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = _lastValue,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 120,
        };
        _slider.Classes.Add("gesture-rack-slider");

        _valueText = new TextBlock
        {
            Text = _lastValue.ToString(),
            MinWidth = 36,
            TextAlignment = Avalonia.Media.TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _valueText.Classes.Add("muted");

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
        };
        grid.Children.Add(_slider);
        Grid.SetColumn(_valueText, 1);
        grid.Children.Add(_valueText);
        Child = grid;

        ToolTip.SetTip(this, $"{label} · drag to adjust");

        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        _slider.GotFocus += OnSliderGotFocus;
        _slider.LostFocus += OnSliderLostFocus;
        _slider.ValueChanged += OnSliderValueChanged;
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _hovered = true;
        SyncBorder();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _hovered = false;
        SyncBorder();
    }

    private void OnSliderGotFocus(object? sender, GotFocusEventArgs e)
    {
        _focused = true;
        SyncBorder();
    }

    private void OnSliderLostFocus(object? sender, RoutedEventArgs e)
    {
        _focused = false;
        SyncBorder();
    }

    private void OnSliderValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        var next = Math.Clamp((int)Math.Round(_slider.Value), _minimum, _maximum);
        _valueText.Text = next.ToString();
        if (next == _lastValue) return;
        _lastValue = next;
        _changed(next);
    }

    private void SyncBorder()
    {
        BorderBrush = _hovered || _focused
            ? EditorThemeTokens.Accent
            : EditorThemeTokens.PanelBorder;
    }
}
