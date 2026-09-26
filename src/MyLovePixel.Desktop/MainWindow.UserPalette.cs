using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private UserPaletteStore? _userPaletteStore;
    private readonly WrapPanel _userPaletteSwatches = new() { ItemWidth = 74, ItemHeight = 54 };
    private readonly TextBlock _userPaletteCount = new() { FontSize = 11 };
    private readonly TextBlock _userPaletteStatus = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _userPaletteEmpty = new()
    {
        Text = "Enter a HEX color above, then choose Save color.",
        TextWrapping = TextWrapping.Wrap,
    };
    private readonly TextBlock _studioHexHint = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private Button? _userPaletteAdd;
    private Button? _userPaletteRemove;
    private Button? _studioApplyHex;
    private Rgba32? _selectedUserPaletteColor;

    private Control BuildUserPaletteEditor()
    {
        _userPaletteCount.Classes.Add("muted");
        _userPaletteStatus.Classes.Add("muted");
        _userPaletteEmpty.Classes.Add("muted");
        _studioHexHint.Classes.Add("muted");

        _userPaletteAdd = new Button
        {
            Content = "Save color",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 6),
        };
        _userPaletteAdd.Classes.Add("text-action");
        AutomationProperties.SetAutomationId(_userPaletteAdd, "user-palette-save");
        ToolTip.SetTip(_userPaletteAdd, "Save the HEX color to My palette on this computer.");
        _userPaletteAdd.Click += (_, _) => SaveUserPaletteColor();

        _userPaletteRemove = new Button
        {
            Content = "Remove selected",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 6),
        };
        _userPaletteRemove.Classes.Add("text-action");
        AutomationProperties.SetAutomationId(_userPaletteRemove, "user-palette-remove");
        ToolTip.SetTip(_userPaletteRemove, "Remove the selected saved swatch. Your artwork is not changed.");
        _userPaletteRemove.Click += (_, _) => RemoveUserPaletteColor();

        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 6 };
        actions.Children.Add(_userPaletteAdd);
        actions.Children.Add(Place(_userPaletteRemove, 1));
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(actions);
        body.Children.Add(_userPaletteCount);
        body.Children.Add(_userPaletteEmpty);
        body.Children.Add(new ScrollViewer
        {
            MaxHeight = 170,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _userPaletteSwatches,
        });
        body.Children.Add(_userPaletteStatus);
        AutomationProperties.SetAutomationId(body, "user-palette");

        try
        {
            _userPaletteStore = new UserPaletteStore();
            ToolTip.SetTip(_userPaletteCount, _userPaletteStore.FilePath);
            RefreshUserPalette();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            _userPaletteStatus.Text = $"My palette is unavailable: {error.Message}";
            UpdateUserPaletteButtons();
        }
        return SectionCard("My palette", "Saved on this computer. Available after restarting and in every project.", body);
    }

    private void RefreshStudioHexFeedback()
    {
        if (_syncingStudioColor) return;
        var valid = HexColor.TryParse(_studioHex.Text, out _);
        _studioHexHint.Text = valid
            ? "Enter or Apply to use · Escape to cancel · AA is transparency."
            : "Use #RRGGBB or #RRGGBBAA, for example #654321.";
        if (_studioApplyHex is not null) _studioApplyHex.IsEnabled = valid;
        UpdateUserPaletteButtons();
    }

    private void UpdateUserPaletteButtons()
    {
        var writable = _userPaletteStore is { LoadError: null };
        var valid = HexColor.TryParse(_studioHex.Text, out var color);
        var saved = valid && _userPaletteStore?.Colors.Contains(color) == true;
        if (_userPaletteAdd is not null)
        {
            _userPaletteAdd.IsEnabled = writable && valid &&
                (saved || _userPaletteStore!.Colors.Count < UserPaletteStore.MaxColors);
            _userPaletteAdd.Content = saved ? "Color saved" : "Save color";
        }
        if (_userPaletteRemove is not null)
            _userPaletteRemove.IsEnabled = writable && _selectedUserPaletteColor is { } selected &&
                _userPaletteStore!.Colors.Contains(selected);
    }

    private void RefreshUserPalette(string? message = null)
    {
        _userPaletteSwatches.Children.Clear();
        var colors = _userPaletteStore?.Colors ?? Array.Empty<Rgba32>();
        _userPaletteCount.Text = $"{colors.Count} / {UserPaletteStore.MaxColors} saved colors";
        _userPaletteEmpty.IsVisible = colors.Count == 0;
        foreach (var color in colors)
        {
            var captured = color;
            var hex = HexColor.Format(color);
            var content = new StackPanel { Spacing = 3 };
            content.Children.Add(new Border
            {
                Height = 21,
                Background = Brush(color),
                BorderBrush = EditorThemeTokens.PanelBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
            });
            content.Children.Add(new TextBlock
            {
                Text = hex,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            var button = new Button
            {
                Width = 70,
                Height = 50,
                MinWidth = 0,
                MinHeight = 0,
                Padding = new Thickness(4, 3),
                Content = content,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                BorderBrush = EditorThemeTokens.PanelBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
            };
            if (_selectedUserPaletteColor == captured) button.Classes.Add("selected");
            AutomationProperties.SetAutomationId(button, "user-palette-" + hex[1..]);
            AutomationProperties.SetName(button, $"Use saved color {hex}");
            ToolTip.SetTip(button, $"Use {hex} as the active Primary or Secondary color.");
            button.Click += (_, _) =>
            {
                _selectedUserPaletteColor = captured;
                ApplyStudioColor(captured);
                RefreshUserPalette($"Selected {hex}.");
            };
            _userPaletteSwatches.Children.Add(button);
        }
        _userPaletteStatus.Text = _userPaletteStore?.LoadError ?? message ??
            "Click a swatch to use it. Changes are saved automatically.";
        UpdateUserPaletteButtons();
    }

    private void SaveUserPaletteColor()
    {
        if (_userPaletteStore is null) return;
        if (!HexColor.TryParse(_studioHex.Text, out var color))
        {
            ApplyStudioHex();
            return;
        }
        try
        {
            var added = _userPaletteStore.Add(color);
            _selectedUserPaletteColor = color;
            ApplyStudioColor(color);
            RefreshUserPalette(added
                ? $"Saved {HexColor.Format(color)} on this computer."
                : $"{HexColor.Format(color)} is already saved; no duplicate was added.");
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.Text.Json.JsonException)
        {
            _userPaletteStatus.Text = $"Color was not saved: {error.Message}";
            SetError(_userPaletteStatus.Text);
        }
    }

    private void RemoveUserPaletteColor()
    {
        if (_userPaletteStore is null || _selectedUserPaletteColor is not { } color) return;
        try
        {
            _userPaletteStore.Remove(color);
            _selectedUserPaletteColor = null;
            RefreshUserPalette($"Removed {HexColor.Format(color)}. The active drawing color is unchanged.");
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.Text.Json.JsonException)
        {
            _userPaletteStatus.Text = $"Color was not removed: {error.Message}";
            SetError(_userPaletteStatus.Text);
        }
    }
}
