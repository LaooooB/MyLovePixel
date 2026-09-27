using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private readonly UserPaletteStore _userPaletteStore;
    private readonly TextBox _personalHex = new() { PlaceholderText = "#654321", MaxLength = 32, MinWidth = 0 };
    private readonly TextBox _personalName = new() { PlaceholderText = "Color name", MaxLength = UserPaletteStore.MaxNameLength, MinWidth = 0 };
    private readonly ColorSwatchView _personalPreview = new() { Width = 32, Height = 32 };
    private readonly TextBlock _personalCount = new() { Foreground = EditorThemeTokens.TextSecondary, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _personalValidation = new() { Foreground = EditorThemeTokens.Danger, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly TextBlock _personalEmpty = new() { Text = "No saved colors", Foreground = EditorThemeTokens.TextSecondary, Margin = new Thickness(0, 4) };
    private readonly StackPanel _personalRows = new() { Spacing = 5 };
    private readonly Button _personalSave = new() { Content = "Save color", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _personalRename = new() { Content = "Rename", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _personalRemove = new() { Content = "Remove", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Dictionary<Rgba32, (Button Button, TextBlock Name)> _personalButtons = [];
    private Rgba32? _selectedPersonalColor;
    private bool _personalDraftEdited;
    private bool _syncingPersonal;
    private bool _personalBuilt;

    private Control BuildUserPaletteEditor()
    {
        var body = new StackPanel { Spacing = 7 };
        Named(body, "palette.personal", "My palette");
        var title = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 };
        title.Children.Add(new TextBlock { Text = "My palette", FontSize = 14, FontWeight = FontWeight.SemiBold });
        title.Children.Add(Place(_personalCount, 1));
        body.Children.Add(title);
        var hex = new Grid { ColumnDefinitions = new ColumnDefinitions("42,*,Auto"), ColumnSpacing = 6 };
        hex.Children.Add(new TextBlock { Text = "HEX", VerticalAlignment = VerticalAlignment.Center });
        hex.Children.Add(Place(Named(_personalHex, "palette.hex", "Saved color HEX"), 1));
        hex.Children.Add(Place(_personalPreview, 2));
        body.Children.Add(hex);
        var name = new Grid { ColumnDefinitions = new ColumnDefinitions("42,*"), ColumnSpacing = 6 };
        name.Children.Add(new TextBlock { Text = "Name", VerticalAlignment = VerticalAlignment.Center });
        name.Children.Add(Place(Named(_personalName, "palette.name", "Saved color name"), 1));
        body.Children.Add(name);
        body.Children.Add(Named(_personalValidation, "palette.validation", "Palette validation"));
        body.Children.Add(Named(_personalSave, "palette.save", "Save named color"));
        body.Children.Add(_personalEmpty);
        body.Children.Add(new ScrollViewer
        {
            Content = _personalRows, MaxHeight = 228,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 6 };
        actions.Children.Add(Named(_personalRename, "palette.rename", "Rename selected saved color"));
        actions.Children.Add(Place(Named(_personalRemove, "palette.remove", "Remove selected saved color"), 1));
        body.Children.Add(actions);
        ToolTip.SetTip(_personalSave, "Save on this computer · Enter");
        ToolTip.SetTip(_personalRename, "Edit the selected color's visible name");
        ToolTip.SetTip(_personalRemove, "Remove from My palette; artwork stays unchanged");
        ToolTip.SetTip(_personalCount, _userPaletteStore.FilePath);
        _personalHex.TextChanging += (_, _) => PersonalDraftChanged();
        _personalName.TextChanging += (_, _) => PersonalDraftChanged();
        _personalHex.KeyDown += PersonalInputKey;
        _personalName.KeyDown += PersonalInputKey;
        _personalSave.Click += (_, _) => SavePersonalColor();
        _personalRename.Click += (_, _) =>
        {
            if (_selectedPersonalColor is not { } color) return;
            FillPersonalDraft(color);
            _personalName.BringIntoView();
            _personalName.Focus(); _personalName.SelectAll();
        };
        _personalRemove.Click += (_, _) => RemovePersonalColor();
        _personalBuilt = true;
        RefreshPersonalRows();
        FillPersonalDraft(_studioColor);
        Activated += (_, _) => { _userPaletteStore.Reload(); RefreshPersonalRows(); UpdatePersonalActions(); };
        return new Border
        {
            Padding = new Thickness(10), CornerRadius = EditorThemeTokens.CardRadius,
            Background = EditorThemeTokens.SurfaceRaised,
            BorderBrush = EditorThemeTokens.PanelBorder, BorderThickness = new Thickness(1), Child = body,
        };
    }

    private void OpenPersonalPalette()
    {
        if (!TryParseHex(_studioHex.Text, out var color)) { ApplyStudioHex(); return; }
        _sideTabs.SelectedIndex = 0;
        FillPersonalDraft(color);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _personalName.BringIntoView();
            _personalName.Focus(); _personalName.SelectAll();
        });
    }

    private void PersonalInputKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { SavePersonalColor(); e.Handled = true; }
        else if (e.Key == Key.Escape) { FillPersonalDraft(_selectedPersonalColor ?? _studioColor); e.Handled = true; }
    }

    private void PersonalDraftChanged()
    {
        if (_syncingPersonal || !_personalBuilt) return;
        _personalDraftEdited = true;
        _personalValidation.IsVisible = false;
        UpdatePersonalActions();
    }

    private void FillPersonalDraft(Rgba32 color)
    {
        if (!_personalBuilt) return;
        _syncingPersonal = true;
        try
        {
            _personalHex.Text = HexColor.Format(color);
            _personalName.Text = _userPaletteStore.Swatches.FirstOrDefault(s => s.Color == color)?.Name ?? string.Empty;
            _personalValidation.IsVisible = false;
            _personalDraftEdited = false;
        }
        finally { _syncingPersonal = false; }
        UpdatePersonalActions();
    }

    private void SyncPersonalColor(Rgba32 color)
    {
        if (_personalBuilt && !_personalDraftEdited) FillPersonalDraft(color);
    }

    private void UpdatePersonalActions()
    {
        var valid = HexColor.TryParse(_personalHex.Text, out var color);
        var existing = valid ? _userPaletteStore.Swatches.FirstOrDefault(s => s.Color == color) : null;
        _personalSave.Content = existing is null ? "Save color" : "Save name";
        _personalSave.IsEnabled = _userPaletteStore.LoadError is null && valid &&
            (existing is not null || _userPaletteStore.Swatches.Count < UserPaletteStore.MaxColors);
        _personalPreview.Color = valid ? color : Rgba32.Transparent;
        var selected = _selectedPersonalColor is { } selectedColor && _personalButtons.ContainsKey(selectedColor);
        _personalRename.IsEnabled = selected;
        _personalRemove.IsEnabled = selected && _userPaletteStore.LoadError is null;
        if (_userPaletteStore.LoadError is { } error) ShowPersonalError(error);
    }

    private void RefreshPersonalRows()
    {
        var colors = _userPaletteStore.Swatches;
        var wanted = colors.Select(s => s.Color).ToHashSet();
        foreach (var color in _personalButtons.Keys.Where(c => !wanted.Contains(c)).ToArray())
        {
            _personalRows.Children.Remove(_personalButtons[color].Button);
            _personalButtons.Remove(color);
        }
        if (_selectedPersonalColor is { } old && !wanted.Contains(old)) _selectedPersonalColor = null;
        foreach (var saved in colors)
        {
            if (_personalButtons.TryGetValue(saved.Color, out var row))
            {
                row.Name.Text = saved.Name;
                AutomationProperties.SetName(row.Button, $"{saved.Name}, {HexColor.Format(saved.Color)}");
                continue;
            }
            var label = new TextBlock { Text = saved.Name, FontSize = 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
            var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(label);
            text.Children.Add(new TextBlock { Text = HexColor.Format(saved.Color), FontSize = 12, Foreground = EditorThemeTokens.TextSecondary });
            var content = new Grid { ColumnDefinitions = new ColumnDefinitions("36,*"), ColumnSpacing = 9 };
            content.Children.Add(new ColorSwatchView { Color = saved.Color, Width = 36, Height = 36, VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(Place(text, 1));
            var button = new Button
            {
                Content = content, Padding = new Thickness(8), MinHeight = 52,
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            Named(button, "palette.swatch." + HexColor.Format(saved.Color)[1..], $"{saved.Name}, {HexColor.Format(saved.Color)}");
            var captured = saved.Color;
            button.Click += (_, _) => SelectPersonalColor(captured);
            _personalButtons.Add(saved.Color, (button, label));
            _personalRows.Children.Add(button);
        }
        _personalCount.Text = $"{colors.Count} / {UserPaletteStore.MaxColors}";
        _personalEmpty.IsVisible = colors.Count == 0;
        foreach (var pair in _personalButtons) SetSelected(pair.Value.Button, pair.Key == _selectedPersonalColor);
        UpdatePersonalActions();
    }

    private void SelectPersonalColor(Rgba32 color)
    {
        _selectedPersonalColor = color;
        ApplyStudioColor(color);
        FillPersonalDraft(color);
        foreach (var pair in _personalButtons) SetSelected(pair.Value.Button, pair.Key == color);
        UpdatePersonalActions();
    }

    private void SavePersonalColor()
    {
        if (!HexColor.TryParse(_personalHex.Text, out var color)) { ShowPersonalError("Enter #RRGGBB or #RRGGBBAA."); return; }
        try
        {
            // Add never overwrites a concurrent insertion. Rename is an explicit
            // save against an existing color shown in the current form.
            var existing = _userPaletteStore.Swatches.FirstOrDefault(s => s.Color == color);
            if (existing is not null) _userPaletteStore.Rename(color, _personalName.Text ?? string.Empty);
            else _userPaletteStore.Add(color, _personalName.Text);
            _selectedPersonalColor = color;
            RefreshPersonalRows();
            SelectPersonalColor(color);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        { ShowPersonalError($"Not saved. {ex.Message}"); }
    }

    private void RemovePersonalColor()
    {
        if (_selectedPersonalColor is not { } color) return;
        try
        {
            _userPaletteStore.Remove(color);
            _selectedPersonalColor = null;
            RefreshPersonalRows();
            FillPersonalDraft(_studioColor);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.Text.Json.JsonException)
        { ShowPersonalError($"Not removed. {ex.Message}"); }
    }

    private void ShowPersonalError(string message) { _personalValidation.Text = message; _personalValidation.IsVisible = true; }
}
