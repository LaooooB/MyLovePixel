using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private readonly WrapPanel _quickSwatches = new() { ItemWidth = 30, ItemHeight = 30 };
    private readonly TextBlock _quickLabel = new() { Text = "Quick colors", FontSize = 12, Foreground = EditorThemeTokens.TextSecondary, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _quickSave = new() { Content = "Save…", Padding = new Thickness(6, 3), FontSize = 12, MinHeight = 26 };
    private readonly Button _quickRemove = new() { Content = "Remove", Padding = new Thickness(6, 3), FontSize = 12, MinHeight = 26 };
    private readonly TextBlock _quickValue = new() { FontSize = 12, Foreground = EditorThemeTokens.TextSecondary };
    private readonly ScrollViewer _quickScroll = new() { MaxHeight = 64, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly Dictionary<Rgba32, Button> _quickButtons = [];
    private Rgba32? _selectedQuickColor;

    private void RefreshFolderChoices()
    {
        var signature = string.Join("|", _userPaletteStore.Folders.Select(f => f.Id + ":" + f.Name));
        if (signature == _folderSignature) return;
        _folderSignature = signature;
        _visiblePersonalColors = Array.Empty<SavedColor>();
        var previousFilter = _folderFilter.SelectedItem as FolderChoice;
        var filterId = previousFilter is null ? "*" : previousFilter.Id;
        var targetId = (_personalFolder.SelectedItem as FolderChoice)?.Id;
        var folders = _userPaletteStore.Folders.Select(f => new FolderChoice(f.Id, f.Name)).ToArray();
        _syncingFolder = true;
        try
        {
            var filters = new[] { new FolderChoice("*", "All colors"), new FolderChoice(null, "Unfiled") }.Concat(folders).ToArray();
            _folderFilter.ItemsSource = filters; _folderFilter.SelectedItem = filters.FirstOrDefault(f => f.Id == filterId) ?? filters[0];
            var targets = new[] { new FolderChoice(null, "Unfiled") }.Concat(folders).ToArray();
            _personalFolder.ItemsSource = targets; _personalFolder.SelectedItem = targets.FirstOrDefault(f => f.Id == targetId) ?? targets[0];
        }
        finally { _syncingFolder = false; }
    }
    private void ShowFolderMenu()
    {
        var id = (_folderFilter.SelectedItem as FolderChoice)?.Id;
        var current = _userPaletteStore.Folders.FirstOrDefault(f => f.Id == id);
        var create = Named(new MenuItem { Header = "New folder…" }, "folder.action.new", "New folder");
        create.Click += async (_, _) => await EditColorFolderAsync(null);
        var rename = Named(new MenuItem { Header = "Rename folder…", IsEnabled = current is not null }, "folder.action.rename", "Rename folder");
        rename.Click += async (_, _) => await EditColorFolderAsync(current);
        var remove = Named(new MenuItem { Header = "Remove folder · keep colors", IsEnabled = current is not null }, "folder.action.remove", "Remove folder and keep its colors");
        remove.Click += (_, _) =>
        {
            if (current is null) return;
            try { _userPaletteStore.DeleteFolder(current.Id); RefreshPersonalRows(); }
            catch (Exception ex) when (IsPaletteError(ex)) { ShowPersonalError(ex.Message); }
        };
        new MenuFlyout { ItemsSource = new[] { create, rename, remove }, Placement = PlacementMode.Bottom }.ShowAt(_folderManage);
    }
    private async Task EditColorFolderAsync(ColorFolder? folder)
    {
        var selectedId = folder?.Id;
        var dialog = new PaletteNameDialog(folder is null ? "New folder" : "Rename folder", folder?.Name ?? string.Empty, name =>
        {
            if (folder is null) selectedId = _userPaletteStore.CreateFolder(name);
            else _userPaletteStore.RenameFolder(folder.Id, name);
        });
        var saved = await dialog.ShowDialog<bool>(this);
        if (!saved) return;
        RefreshPersonalRows();
        _folderFilter.SelectedItem = _folderFilter.Items.Cast<FolderChoice>().FirstOrDefault(f => f.Id == selectedId);
    }
    private async Task MovePersonalColorAsync()
    {
        if (_selectedPersonalColor is not { } color) return;
        var saved = _userPaletteStore.Swatches.FirstOrDefault(s => s.Color == color);
        if (saved is null) return;
        var dialog = new ColorFolderDialog(_userPaletteStore.Folders, saved.FolderId, id => _userPaletteStore.Move(color, id));
        if (await dialog.ShowDialog<bool>(this)) RefreshPersonalRows();
    }

    private Control BuildQuickColors()
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 4 };
        header.Children.Add(_quickLabel);
        header.Children.Add(Place(Named(_quickSave, "quick.save", "Name and save selected quick color"), 1));
        header.Children.Add(Place(Named(_quickRemove, "quick.remove", "Remove selected quick color slot"), 2));
        var body = new StackPanel { Spacing = 3, Margin = new Thickness(9, 2, 9, 5) };
        body.Children.Add(header);
        _quickScroll.Content = _quickSwatches;
        body.Children.Add(_quickScroll);
        body.Children.Add(_quickValue);
        _quickSave.Click += (_, _) =>
        {
            if (_selectedQuickColor is not { } color) return;
            ShowPersonalEditor(); FillPersonalDraft(color); FocusPersonalName();
        };
        _quickRemove.Click += (_, _) =>
        {
            if (_selectedQuickColor is not { } color) return;
            try { _userPaletteStore.RemoveQuick(color); _selectedQuickColor = null; RefreshQuickColors(); }
            catch (Exception ex) when (IsPaletteError(ex)) { ShowPersonalError(ex.Message); }
        };
        RefreshQuickColors();
        return Named(body, "quick.colors", "Quick color slots");
    }
    private void KeepCurrentColor()
    {
        try
        {
            _userPaletteStore.Keep(_studioColor);
            _selectedQuickColor = _studioColor;
            RefreshQuickColors();
            _quickButtons[_studioColor].BringIntoView();
        }
        catch (Exception ex) when (IsPaletteError(ex)) { ShowPersonalError(ex.Message); }
    }
    private void RefreshQuickColors()
    {
        var wanted = _userPaletteStore.QuickColors.ToHashSet();
        foreach (var color in _quickButtons.Keys.Where(c => !wanted.Contains(c)).ToArray())
        { _quickSwatches.Children.Remove(_quickButtons[color]); _quickButtons.Remove(color); }
        foreach (var color in _userPaletteStore.QuickColors)
        {
            if (_quickButtons.ContainsKey(color)) continue;
            var button = new Button { Width = 28, Height = 28, MinHeight = 28, Padding = new Thickness(3), Content = new ColorSwatchView { Color = color } };
            Named(button, "quick.swatch." + HexColor.Format(color)[1..], "Use quick color " + HexColor.Format(color));
            ToolTip.SetTip(button, HexColor.Format(color));
            var captured = color;
            button.Click += (_, _) => { _selectedQuickColor = captured; ApplyStudioColor(captured); RefreshQuickColors(); };
            _quickButtons.Add(color, button); _quickSwatches.Children.Add(button);
        }
        if (_selectedQuickColor is { } selected && !wanted.Contains(selected)) _selectedQuickColor = null;
        foreach (var pair in _quickButtons) SetSelected(pair.Value, pair.Key == _selectedQuickColor);
        _quickLabel.Text = $"Quick colors · {wanted.Count}";
        _quickValue.Text = _selectedQuickColor is { } current ? HexColor.Format(current) : string.Empty;
        _quickValue.IsVisible = _selectedQuickColor.HasValue;
        _quickSave.IsEnabled = _quickRemove.IsEnabled = _selectedQuickColor.HasValue;
        _quickScroll.IsVisible = wanted.Count > 0;
        SizeColorPage();
    }
}
