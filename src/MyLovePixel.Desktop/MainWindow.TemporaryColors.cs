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
    private readonly WrapPanel _temporarySwatches = new() { ItemWidth = 82, ItemHeight = 66 };
    private readonly TextBlock _temporaryStatus = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _temporaryEmpty = new() { Text = "Press I to pick a pixel, then + Temp above. Repeat to keep several sampled colors ready.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) };
    private Button? _temporaryQuickSave, _temporaryRemove, _temporaryClear, _temporaryPromote;
    private Rgba32? _selectedTemporaryColor;

    private Control BuildTemporaryColors()
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 5 };
        _temporaryPromote = ColorAction("Save to library", "temporary-promote", () =>
        {
            if (_selectedTemporaryColor is not { } color) return;
            ApplyStudioColor(color); SaveUserPaletteColor();
        });
        _temporaryRemove = ColorAction("Remove", "temporary-remove", () =>
        {
            if (_selectedTemporaryColor is not { } color || _userPaletteStore is null) return;
            RunPaletteAction(() => { _userPaletteStore.RemoveTemporary(color); _selectedTemporaryColor = null; }, "Temporary color removed.");
        });
        _temporaryClear = ColorAsyncAction("Clear all", "temporary-clear", async () =>
        {
            if (_userPaletteStore is null || !await new ColorLibraryConfirmDialog("Clear temporary colors?", "This clears only the temporary rack. Your saved library and artwork will stay unchanged.", "Clear rack").ShowDialog<bool>(this)) return;
            RunPaletteAction(() => { _userPaletteStore.ClearTemporary(); _selectedTemporaryColor = null; }, "Temporary rack cleared.");
        });
        actions.Children.Add(_temporaryPromote); actions.Children.Add(Place(_temporaryRemove, 1)); actions.Children.Add(Place(_temporaryClear, 2));
        root.Children.Add(actions);
        var center = new Grid();
        center.Children.Add(new ScrollViewer { Content = _temporarySwatches, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        center.Children.Add(_temporaryEmpty); Grid.SetRow(center, 1); root.Children.Add(center);
        _temporaryStatus.Classes.Add("muted"); _temporaryEmpty.Classes.Add("muted");
        Grid.SetRow(_temporaryStatus, 2); root.Children.Add(_temporaryStatus);
        AutomationProperties.SetAutomationId(root, "temporary-colors");
        return root;
    }
    private void SaveTemporaryColor()
    {
        var session = Current();
        if (session is null || _userPaletteStore is null) return;
        var colors = session.GetToolColors();
        var current = _studioSecondaryTarget ? colors.Secondary : colors.Primary;
        RunPaletteAction(() => { _userPaletteStore.AddTemporary(current); _selectedTemporaryColor = current; }, $"{HexColor.Format(current)} kept in Temporary.");
    }
    private void RefreshTemporaryColors()
    {
        var colors = _userPaletteStore?.TemporaryColors ?? Array.Empty<Rgba32>();
        _temporarySwatches.Children.Clear();
        foreach (var color in colors)
        {
            var captured = color;
            var button = BuildLibrarySwatch(color, "", color == _selectedTemporaryColor, "temporary-");
            button.Click += (_, _) => { _selectedTemporaryColor = captured; ApplyStudioColor(captured); RefreshTemporaryColors(); };
            _temporarySwatches.Children.Add(button);
        }
        _temporaryEmpty.IsVisible = colors.Count == 0;
        _temporaryStatus.Text = _userPaletteStore?.LoadError ?? $"{colors.Count}/{UserPaletteStore.MaxTemporaryColors} temporary colors · kept after restart until you clear them";
        UpdateTemporaryButtons();
    }
    private void UpdateTemporaryButtons()
    {
        var writable = _userPaletteStore is { LoadError: null };
        var selected = writable && _selectedTemporaryColor is { } color && _userPaletteStore!.TemporaryColors.Contains(color);
        if (_temporaryQuickSave is not null)
        {
            _temporaryQuickSave.IsEnabled = writable;
            _temporaryQuickSave.Content = $"+ Temp ({_userPaletteStore?.TemporaryColors.Count ?? 0})";
        }
        if (_temporaryRemove is not null) _temporaryRemove.IsEnabled = selected;
        if (_temporaryPromote is not null) _temporaryPromote.IsEnabled = selected;
        if (_temporaryClear is not null) _temporaryClear.IsEnabled = writable && _userPaletteStore!.TemporaryColors.Count > 0;
    }
}
