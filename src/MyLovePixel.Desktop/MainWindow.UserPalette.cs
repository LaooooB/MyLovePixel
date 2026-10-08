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
    private const int ColorPageSize = 48;
    private UserPaletteStore? _userPaletteStore;
    private readonly WrapPanel _userPaletteSwatches = new() { ItemWidth = 82, ItemHeight = 66 };
    private readonly TextBox _colorSearch = new() { PlaceholderText = "Search name, HEX or folder…", MaxLength = 160 };
    private readonly ComboBox _colorFolder = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _userPaletteCount = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _userPaletteStatus = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _userPaletteEmpty = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) };
    private readonly TextBlock _studioHexHint = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _colorPageLabel = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private Button? _userPaletteAdd, _userPaletteRemove, _userPaletteEdit, _studioApplyHex;
    private Button? _folderRename, _folderDelete, _previousColorPage, _nextColorPage;
    private Rgba32? _selectedUserPaletteColor;
    private int _colorPage;
    private bool _syncingFolders;
    private ScrollViewer? _colorSwatchScroll;

    private Control BuildColorWorkspace()
    {
        _userPaletteStore ??= new UserPaletteStore();
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8, Margin = new Thickness(10) };
        root.Children.Add(BuildStudioPaletteEditor());
        var tabs = new TabControl
        {
            ItemsSource = new object[] { TextTab("Saved colors", BuildUserPaletteEditor()), TextTab("Temporary", BuildTemporaryColors()) },
        };
        AutomationProperties.SetAutomationId(tabs, "color-library-tabs");
        Grid.SetRow(tabs, 1); root.Children.Add(tabs);
        RefreshUserPalette(); RefreshTemporaryColors();
        Activated += (_, _) => RunPaletteAction(() => { _userPaletteStore.Reload(); }, "Colors refreshed from disk.", quiet: true);
        return root;
    }

    private Control BuildUserPaletteEditor()
    {
        foreach (var label in new[] { _userPaletteCount, _userPaletteStatus, _userPaletteEmpty, _studioHexHint, _colorPageLabel }) label.Classes.Add("muted");
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        var header = new StackPanel { Spacing = 6 };
        var search = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 5 };
        AutomationProperties.SetAutomationId(_colorSearch, "color-search");
        AutomationProperties.SetName(_colorSearch, "Search saved colors by name, HEX or folder");
        search.Children.Add(_colorSearch);
        var clear = ColorAction("Clear", "color-search-clear", () => _colorSearch.Text = "");
        Grid.SetColumn(clear, 1); search.Children.Add(clear); header.Children.Add(search);
        _colorSearch.TextChanged += (_, _) => { _colorPage = 0; _selectedUserPaletteColor = null; RefreshUserPalette(); ResetColorScroll(); };
        var folders = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), ColumnSpacing = 4 };
        AutomationProperties.SetAutomationId(_colorFolder, "color-folder");
        AutomationProperties.SetName(_colorFolder, "Color folder");
        folders.Children.Add(_colorFolder);
        var newFolder = ColorAsyncAction("New", "color-folder-new", CreateColorFolderAsync);
        _folderRename = ColorAsyncAction("Rename", "color-folder-rename", RenameColorFolderAsync);
        _folderDelete = ColorAsyncAction("Delete", "color-folder-delete", DeleteColorFolderAsync);
        folders.Children.Add(Place(newFolder, 1)); folders.Children.Add(Place(_folderRename, 2)); folders.Children.Add(Place(_folderDelete, 3));
        header.Children.Add(folders);
        _colorFolder.SelectionChanged += (_, _) =>
        {
            if (_syncingFolders) return;
            _colorPage = 0; _selectedUserPaletteColor = null; RefreshUserPalette(); ResetColorScroll();
        };
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 6 };
        _userPaletteAdd = ColorAction("Save current", "user-palette-save", SaveUserPaletteColor);
        _userPaletteAdd.Classes.Add("primary");
        _userPaletteEdit = ColorAsyncAction("Edit / Move", "user-palette-edit", EditUserPaletteColorAsync);
        _userPaletteRemove = ColorAsyncAction("Remove", "user-palette-remove", RemoveUserPaletteColorAsync);
        actions.Children.Add(_userPaletteAdd); actions.Children.Add(Place(_userPaletteEdit, 1)); actions.Children.Add(Place(_userPaletteRemove, 2));
        header.Children.Add(actions); root.Children.Add(header);
        var center = new Grid();
        _colorSwatchScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _userPaletteSwatches };
        center.Children.Add(_colorSwatchScroll); center.Children.Add(_userPaletteEmpty);
        Grid.SetRow(center, 1); root.Children.Add(center);
        var footer = new StackPanel { Spacing = 4 };
        var paging = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), ColumnSpacing = 5 };
        paging.Children.Add(_userPaletteCount);
        _previousColorPage = ColorAction("Prev", "color-page-previous", () => { _colorPage--; RefreshUserPalette(); ResetColorScroll(); });
        _nextColorPage = ColorAction("Next", "color-page-next", () => { _colorPage++; RefreshUserPalette(); ResetColorScroll(); });
        paging.Children.Add(Place(_previousColorPage, 1)); paging.Children.Add(Place(_colorPageLabel, 2)); paging.Children.Add(Place(_nextColorPage, 3));
        footer.Children.Add(paging); footer.Children.Add(_userPaletteStatus);
        Grid.SetRow(footer, 2); root.Children.Add(footer);
        AutomationProperties.SetAutomationId(root, "user-palette");
        ToolTip.SetTip(_userPaletteStatus, _userPaletteStore?.FilePath);
        return root;
    }

    private static Button ColorAction(string text, string id, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(6, 4), MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetAutomationId(button, id); AutomationProperties.SetName(button, text);
        button.Click += (_, _) => action(); return button;
    }
    private static Button ColorAsyncAction(string text, string id, Func<Task> action)
    {
        var button = new Button { Content = text, Padding = new Thickness(6, 4), MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetAutomationId(button, id); AutomationProperties.SetName(button, text);
        button.Click += async (_, _) => await action(); return button;
    }

    private void RefreshStudioHexFeedback()
    {
        if (_syncingStudioColor) return;
        var valid = HexColor.TryParse(_studioHex.Text, out _);
        _studioHexHint.Text = valid ? "Enter to apply · I = eyedropper · AA = alpha" : "Use #RRGGBB or #RRGGBBAA, e.g. #654321.";
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
            _userPaletteAdd.IsEnabled = writable && valid && (saved || _userPaletteStore!.Colors.Count < UserPaletteStore.MaxColors);
            _userPaletteAdd.Content = saved ? "Already saved" : "Save current";
        }
        var selected = writable && _selectedUserPaletteColor is { } choice && _userPaletteStore!.Colors.Contains(choice);
        if (_userPaletteEdit is not null) _userPaletteEdit.IsEnabled = selected;
        if (_userPaletteRemove is not null) _userPaletteRemove.IsEnabled = selected;
        var folder = (_colorFolder.SelectedItem as ColorFolderChoice)?.Id;
        if (_folderRename is not null) _folderRename.IsEnabled = writable && folder is not null;
        if (_folderDelete is not null) _folderDelete.IsEnabled = writable && folder is not null;
        UpdateTemporaryButtons();
    }

    private void RefreshUserPalette(string? message = null)
    {
        if (_userPaletteStore is null) return;
        SyncColorFolders();
        var folder = _colorFolder.SelectedItem as ColorFolderChoice;
        var matches = _userPaletteStore.Query(_colorSearch.Text, folder?.Id, folder?.UnfiledOnly ?? false);
        var pages = Math.Max(1, (matches.Count + ColorPageSize - 1) / ColorPageSize);
        _colorPage = Math.Clamp(_colorPage, 0, pages - 1);
        _userPaletteSwatches.Children.Clear();
        foreach (var entry in matches.Skip(_colorPage * ColorPageSize).Take(ColorPageSize))
        {
            var captured = entry.Color;
            var button = BuildLibrarySwatch(captured, entry.Name, _selectedUserPaletteColor == captured, "user-palette-");
            var folderName = _userPaletteStore.Folders.FirstOrDefault(item => item.Id == entry.FolderId)?.Name ?? "Unfiled";
            ToolTip.SetTip(button, $"{entry.Name} {HexColor.Format(captured)} · {folderName}");
            button.Click += (_, _) => { _selectedUserPaletteColor = captured; ApplyStudioColor(captured); RefreshUserPalette($"Selected {HexColor.Format(captured)}."); };
            _userPaletteSwatches.Children.Add(button);
        }
        _userPaletteCount.Text = $"{matches.Count:N0} / {_userPaletteStore.Colors.Count:N0} colors";
        _colorPageLabel.Text = $"{_colorPage + 1}/{pages}";
        if (_previousColorPage is not null) _previousColorPage.IsEnabled = _colorPage > 0;
        if (_nextColorPage is not null) _nextColorPage.IsEnabled = _colorPage + 1 < pages;
        _userPaletteEmpty.IsVisible = matches.Count == 0;
        _userPaletteEmpty.Text = _userPaletteStore.Colors.Count == 0 ? "Your saved colors appear here. Pick a color above, then Save current." : "No matching colors. Clear the search or choose All colors.";
        _userPaletteStatus.Text = _userPaletteStore.LoadError ?? message ?? "Auto-saved locally · previous version backed up";
        UpdateUserPaletteButtons();
    }
    private void SyncColorFolders()
    {
        var selected = _colorFolder.SelectedItem as ColorFolderChoice;
        var choices = new[] { new ColorFolderChoice(null, "All colors"), new ColorFolderChoice(null, "Unfiled", true) }
            .Concat((_userPaletteStore?.Folders ?? Array.Empty<UserPaletteFolder>()).Select(folder => new ColorFolderChoice(folder.Id, folder.Name))).ToArray();
        _syncingFolders = true;
        try
        {
            _colorFolder.ItemsSource = choices;
            _colorFolder.SelectedItem = choices.FirstOrDefault(choice => choice.Id == selected?.Id && choice.UnfiledOnly == (selected?.UnfiledOnly ?? false)) ?? choices[0];
        }
        finally { _syncingFolders = false; }
    }
    private void ResetColorScroll() { if (_colorSwatchScroll is not null) _colorSwatchScroll.Offset = default; }

    private static Button BuildLibrarySwatch(Rgba32 color, string name, bool selected, string idPrefix)
    {
        var content = new StackPanel { Spacing = 2 };
        var checker = new Grid { Height = 25, ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), RowDefinitions = new RowDefinitions("*,*"), ClipToBounds = true };
        for (var row = 0; row < 2; row++) for (var column = 0; column < 4; column++)
            AddCheckerCell(checker, row, column, (row + column) % 2 == 0 ? EditorThemeTokens.CheckerLight : EditorThemeTokens.CheckerDark);
        var tint = new Border { Background = Brush(color) }; Grid.SetColumnSpan(tint, 4); Grid.SetRowSpan(tint, 2); checker.Children.Add(tint);
        content.Children.Add(checker);
        if (!string.IsNullOrEmpty(name)) content.Children.Add(new TextBlock { Text = name, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis });
        content.Children.Add(new TextBlock { Text = HexColor.Format(color), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center });
        var button = new Button { Width = 78, Height = 62, MinWidth = 0, MinHeight = 0, Padding = new Thickness(4, 3), Content = content, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        if (selected) button.Classes.Add("selected");
        AutomationProperties.SetAutomationId(button, idPrefix + HexColor.Format(color)[1..]);
        AutomationProperties.SetName(button, $"{name} {HexColor.Format(color)}");
        return button;
    }

    private void RunPaletteAction(Action action, string message, bool quiet = false)
    {
        try { action(); RefreshUserPalette(quiet ? null : message); RefreshTemporaryColors(); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
        {
            _userPaletteStatus.Text = $"Not saved: {error.Message}";
            _temporaryStatus.Text = _userPaletteStatus.Text;
            SetError(_userPaletteStatus.Text);
        }
    }
    private void SaveUserPaletteColor()
    {
        if (_userPaletteStore is null || !HexColor.TryParse(_studioHex.Text, out var color)) { ApplyStudioHex(); return; }
        var folderId = (_colorFolder.SelectedItem as ColorFolderChoice)?.Id;
        RunPaletteAction(() =>
        {
            var added = _userPaletteStore.Add(color, folderId);
            _selectedUserPaletteColor = color; ApplyStudioColor(color);
            // Reveal both new and duplicate entries, rather than hiding saved results behind a filter.
            _colorSearch.Text = "";
            var entry = _userPaletteStore.Entries.First(item => item.Color == color);
            SyncColorFolders();
            _colorFolder.SelectedItem = ((IEnumerable<ColorFolderChoice>)_colorFolder.ItemsSource!).First(item => item.Id == entry.FolderId && (entry.FolderId is not null || item.UnfiledOnly));
            _selectedUserPaletteColor = color;
            _colorPage = Math.Max(0, _userPaletteStore.Query("", entry.FolderId, entry.FolderId is null).ToList().FindIndex(item => item.Color == color) / ColorPageSize);
            _userPaletteStatus.Text = added ? "Color saved." : "Already saved; no duplicate added.";
        }, $"{HexColor.Format(color)} is saved on this computer.");
        ResetColorScroll();
    }
    private async Task CreateColorFolderAsync()
    {
        var name = await new ColorLibraryNameDialog("New color folder", "").ShowDialog<string?>(this);
        if (name is null || _userPaletteStore is null) return;
        RunPaletteAction(() =>
        {
            var id = _userPaletteStore.CreateFolder(name); SyncColorFolders();
            _colorFolder.SelectedItem = ((IEnumerable<ColorFolderChoice>)_colorFolder.ItemsSource!).First(choice => choice.Id == id);
            _colorSearch.Text = "";
        }, $"Folder '{name}' created.");
    }
    private async Task RenameColorFolderAsync()
    {
        if (_colorFolder.SelectedItem is not ColorFolderChoice { Id: { } id } choice || _userPaletteStore is null) return;
        var name = await new ColorLibraryNameDialog("Rename folder", choice.Name).ShowDialog<string?>(this);
        if (name is null) return;
        RunPaletteAction(() => _userPaletteStore.RenameFolder(id, name), "Folder renamed.");
    }
    private async Task DeleteColorFolderAsync()
    {
        if (_colorFolder.SelectedItem is not ColorFolderChoice { Id: { } id } choice || _userPaletteStore is null) return;
        if (!await new ColorLibraryConfirmDialog("Delete folder?", $"Delete '{choice.Name}'? All its colors will move to Unfiled. No colors will be deleted.", "Delete folder").ShowDialog<bool>(this)) return;
        RunPaletteAction(() => _userPaletteStore.DeleteFolder(id), "Folder deleted. Its colors are in Unfiled.");
    }
    private async Task EditUserPaletteColorAsync()
    {
        var entry = _userPaletteStore?.Entries.FirstOrDefault(item => item.Color == _selectedUserPaletteColor);
        if (entry is null || _userPaletteStore is null) return;
        var change = await new ColorLibraryEditDialog(entry, _userPaletteStore.Folders).ShowDialog<ColorLibraryEdit?>(this);
        if (change is null) return;
        RunPaletteAction(() => _userPaletteStore.UpdateColor(entry.Color, change.Name, change.FolderId), "Saved color updated.");
    }
    private async Task RemoveUserPaletteColorAsync()
    {
        if (_userPaletteStore is null || _selectedUserPaletteColor is not { } color) return;
        if (!await new ColorLibraryConfirmDialog("Remove saved color?", $"Remove {HexColor.Format(color)} from your library? Your artwork and active drawing color will stay unchanged.", "Remove").ShowDialog<bool>(this)) return;
        RunPaletteAction(() => { _userPaletteStore.Remove(color); _selectedUserPaletteColor = null; }, "Color removed. Artwork unchanged.");
    }
}
