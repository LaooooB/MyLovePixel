using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private bool _eyedropperMode;
    private Button? _primaryTargetButton;
    private Button? _secondaryTargetButton;

    private void SelectEyedropper()
    {
        CancelCanvasInteraction();
        _selectionMode = false;
        _eyedropperMode = true;
        RefreshTools(); RefreshToolOptions(); RefreshCanvas(false);
    }

    private void PickCanvasColor(int x, int y)
    {
        if (_canvas.Presentation is not { } p || (uint)x >= (uint)p.Size.Width || (uint)y >= (uint)p.Size.Height) return;
        var bytes = p.Rgba.Span;
        var i = (y * p.Size.Width + x) * 4;
        var color = new Rgba32(bytes[i], bytes[i + 1], bytes[i + 2], bytes[i + 3]);
        ApplyStudioColor(color);
        _userPaletteStatus.Text = "Picked " + HexColor.Format(color) + ". Add temp keeps it for reuse.";
    }

    private static Button NamedToolButton(string glyph, string label, Action action)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 5 };
        if (UiIconSemantics.TryCreate(label, glyph, 16, out var icon)) row.Children.Add(icon);
        else if (UiIcons.TryResolve(label, glyph, out var kind)) row.Children.Add(UiIcons.Create(kind, 16));
        var text = new TextBlock { Text = label, FontSize = 11, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 1); row.Children.Add(text);
        var button = new Button { Content = row, HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, MinHeight = 38, Padding = new Thickness(6, 4) };
        button.Classes.Add("ghost");
        AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, label);
        button.Click += (_, _) => action();
        return button;
    }

    private void RefreshComfortPalette()
    {
        if (Current() is not { } session) return;
        if (_primaryTargetButton is null)
        {
            Button Target(string label, Border swatch, bool secondary)
            {
                swatch.Width = 22; swatch.Height = 22;
                var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                body.Children.Add(swatch);
                body.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
                var button = new Button { Content = body, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(5), MinWidth = 0 };
                button.Click += (_, _) => SetStudioColorTarget(secondary);
                AutomationProperties.SetAutomationId(button, secondary ? "active-secondary-color" : "active-primary-color");
                return button;
            }
            _primaryTargetButton = Target("Primary", _primarySwatch, false);
            _secondaryTargetButton = Target("Secondary", _secondarySwatch, true);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,Auto"), ColumnSpacing = 5 };
            row.Children.Add(_primaryTargetButton);
            row.Children.Add(Place(_secondaryTargetButton, 1));
            row.Children.Add(Place(LibraryButton("Swap", SwapColors, "color-swap"), 2));
            _palettePanel.Children.Add(row);
        }
        var colors = session.GetToolColors();
        _primarySwatch.Background = Brush(colors.Primary);
        _secondarySwatch.Background = Brush(colors.Secondary);
        SetSelectedClass(_primaryTargetButton, !_studioSecondaryTarget);
        SetSelectedClass(_secondaryTargetButton!, _studioSecondaryTarget);
        AutomationProperties.SetName(_primaryTargetButton, "Primary color " + HexColor.Format(colors.Primary));
        AutomationProperties.SetName(_secondaryTargetButton!, "Secondary color " + HexColor.Format(colors.Secondary));
    }

    private static void SetSelectedClass(Control control, bool selected)
    {
        if (selected) { if (!control.Classes.Contains("selected")) control.Classes.Add("selected"); }
        else control.Classes.Remove("selected");
    }

    private void RefreshLibrarySelection()
    {
        var hex = _userPaletteStore?.Colors.FirstOrDefault(c => c.Id == _selectedLibraryColor)?.Hex;
        foreach (var button in _userPaletteSwatches.Children.OfType<Button>())
            SetSelectedClass(button, hex is not null && AutomationProperties.GetAutomationId(button) == "user-palette-" + hex[1..]);
        foreach (var button in _temporarySwatches.Children.OfType<Button>())
            SetSelectedClass(button, _selectedTemporaryColor is not null && AutomationProperties.GetAutomationId(button) == "temporary-color-" + _selectedTemporaryColor[1..]);
        UpdateUserPaletteButtons();
    }
}
