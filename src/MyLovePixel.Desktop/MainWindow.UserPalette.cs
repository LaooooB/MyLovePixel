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
    private const int LibraryPageSize = 12;
    private ColorLibraryStore? _userPaletteStore;
    private readonly WrapPanel _userPaletteSwatches = new() { ItemWidth = 76, ItemHeight = 64 };
    private readonly WrapPanel _temporarySwatches = new() { ItemWidth = 76, ItemHeight = 64 };
    private readonly TextBlock _userPaletteCount = new() { FontSize = 11 };
    private readonly TextBlock _userPaletteStatus = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis };
    private Control? _libraryRecoveryActions;
    private readonly TextBlock _userPaletteEmpty = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _studioHexHint = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _temporaryCount = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _libraryPageLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _temporaryPageLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _librarySearch = new() { PlaceholderText = "Search HEX, name or folder", MaxLength = 160 };
    private readonly TextBox _librarySaveHex = new() { PlaceholderText = "#RRGGBB or #RRGGBBAA", MaxLength = 32 };
    private readonly ComboBox _libraryFolder = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TabControl _colorLibraryTabs = new();
    private readonly TabItem _temporaryLibraryTab = new() { Header = "Temporary" };
    private Button? _saveActiveLibraryButton;
    private Button? _pinTemporaryButton;
    private Button? _newLibraryFolderButton;
    private Button? _userPaletteAdd;
    private Button? _userPaletteRemove;
    private Button? _libraryEdit;
    private Button? _libraryPrevious;
    private Button? _libraryNext;
    private Button? _temporaryPrevious;
    private Button? _temporaryNext;
    private Button? _temporarySave;
    private Button? _temporaryRemove;
    private Button? _temporaryClear;
    private Button? _folderRename;
    private Button? _folderDelete;
    private Button? _libraryRestore;
    private Button? _studioApplyHex;
    private Guid? _selectedLibraryColor;
    private string? _selectedTemporaryColor;
    private int _libraryPage;
    private int _temporaryPage;
    private bool _syncingLibrary;

    private sealed record LibraryFolderChoice(string Name, Guid? Id, bool IsAll = false)
    {
        public override string ToString() => Name;
    }

    private static Button LibraryButton(string text, Action action, string? automationId = null)
    {
        var button = new Button { Content = text, MinWidth = 0, Padding = new Thickness(7, 5) };
        button.Classes.Add("text-action");
        if (automationId is not null) AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, text);
        button.Click += (_, _) => action();
        return button;
    }

    private static Control LibraryRow(params Control[] controls)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var control in controls)
        {
            control.Margin = new Thickness(0, 0, 5, 4);
            row.Children.Add(control);
        }
        return row;
    }

    private Control BuildUserPaletteEditor()
    {
        foreach (var text in new[] { _userPaletteCount, _userPaletteStatus, _userPaletteEmpty, _studioHexHint, _temporaryCount })
            text.Classes.Add("muted");
        AutomationProperties.SetAutomationId(_librarySearch, "color-library-search");
        AutomationProperties.SetName(_librarySearch, "Search saved colors by HEX, name or folder");
        AutomationProperties.SetAutomationId(_libraryFolder, "color-library-folder");
        AutomationProperties.SetName(_libraryFolder, "Color folder filter");
        AutomationProperties.SetAutomationId(_librarySaveHex, "saved-color-hex");
        AutomationProperties.SetName(_librarySaveHex, "HEX color to save to the library");
        _librarySearch.TextChanged += (_, _) =>
        {
            if (_syncingLibrary) return;
            _libraryPage = 0;
            _selectedLibraryColor = null;
            RefreshUserPalette();
        };
        _libraryFolder.SelectionChanged += (_, _) =>
        {
            if (_syncingLibrary) return;
            _libraryPage = 0;
            _selectedLibraryColor = null;
            RefreshUserPalette();
        };
        _librarySaveHex.TextChanged += (_, _) => UpdateUserPaletteButtons();
        _librarySaveHex.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { SaveUserPaletteColor(); e.Handled = true; }
        };
        _userPaletteAdd = LibraryButton("Save HEX", SaveUserPaletteColor, "user-palette-save");
        _userPaletteRemove = LibraryButton("Remove", () => _ = RemoveLibraryColorAsync(), "user-palette-remove");
        _libraryEdit = LibraryButton("Edit", () => _ = EditLibraryColorAsync(), "color-library-edit");
        _folderRename = LibraryButton("Rename folder", () => _ = RenameLibraryFolderAsync());
        _folderDelete = LibraryButton("Delete folder", () => _ = DeleteLibraryFolderAsync());
        _libraryPrevious = LibraryButton("Prev", () => { _libraryPage--; _selectedLibraryColor = null; RefreshUserPalette(); });
        _libraryNext = LibraryButton("Next", () => { _libraryPage++; _selectedLibraryColor = null; RefreshUserPalette(); });
        _temporaryPrevious = LibraryButton("Prev", () => { _temporaryPage--; _selectedTemporaryColor = null; RefreshUserPalette(); });
        _temporaryNext = LibraryButton("Next", () => { _temporaryPage++; _selectedTemporaryColor = null; RefreshUserPalette(); });
        _temporarySave = LibraryButton("Save selected", SaveSelectedTemporaryColor);
        _temporaryRemove = LibraryButton("Remove slot", RemoveSelectedTemporaryColor);
        _temporaryClear = LibraryButton("Clear tray", () => _ = ClearTemporaryLibraryAsync());
        _libraryRestore = LibraryButton("Restore backup", () => _ = RestoreColorLibraryAsync());
        var folders = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 5 };
        folders.Children.Add(_libraryFolder);
        _newLibraryFolderButton = LibraryButton("New folder", () => _ = CreateLibraryFolderAsync());
        folders.Children.Add(Place(_newLibraryFolderButton, 1));
        var save = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 5 };
        save.Children.Add(_librarySaveHex);
        save.Children.Add(Place(_userPaletteAdd, 1));
        _temporaryLibraryTab.Content = BuildTemporaryLibraryLayout();
        _colorLibraryTabs.ItemsSource = new object[]
        {
            new TabItem { Header = "Saved colors", Content = BuildSavedLibraryLayout(folders, save) },
            _temporaryLibraryTab,
        };
        var body = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 6 };
        body.Children.Add(_colorLibraryTabs);
        var status = new StackPanel { Spacing = 4 };
        status.Children.Add(_userPaletteStatus);
        _libraryRecoveryActions = LibraryRow(LibraryButton("Reload", ReloadColorLibrary), _libraryRestore);
        status.Children.Add(_libraryRecoveryActions);
        Grid.SetRow(status, 1); body.Children.Add(status);
        AutomationProperties.SetAutomationId(body, "user-palette");
        AutomationProperties.SetAutomationId(_colorLibraryTabs, "color-library-tabs");
        ReloadColorLibrary();
        return body;
    }

    private void ReloadColorLibrary()
    {
        try
        {
            _userPaletteStore = new ColorLibraryStore();
            ToolTip.SetTip(_userPaletteCount, _userPaletteStore.FilePath);
            RefreshUserPalette();
        }
        catch (Exception error) when (LibraryFailure(error))
        {
            _userPaletteStore = null;
            _userPaletteStatus.Text = "Color library unavailable: " + error.Message;
            UpdateUserPaletteButtons();
        }
    }

    private void RefreshStudioHexFeedback()
    {
        if (_syncingStudioColor) return;
        var valid = HexColor.TryParse(_studioHex.Text, out _);
        _studioHexHint.Text = valid ? "Enter to apply · I: eyedropper · Esc: cancel entry"
            : "Use #RRGGBB or #RRGGBBAA, for example #654321.";
        if (_studioApplyHex is not null) _studioApplyHex.IsEnabled = valid;
        UpdateUserPaletteButtons();
    }

    private void UpdateUserPaletteButtons()
    {
        var writable = _userPaletteStore is { LoadError: null };
        if (_saveActiveLibraryButton is not null) _saveActiveLibraryButton.IsEnabled = writable;
        if (_pinTemporaryButton is not null) _pinTemporaryButton.IsEnabled = writable;
        if (_newLibraryFolderButton is not null) _newLibraryFolderButton.IsEnabled = writable;
        var selected = writable && _userPaletteStore!.Colors.Any(color => color.Id == _selectedLibraryColor);
        var tempSelected = writable && _selectedTemporaryColor is { } hex && _userPaletteStore!.TemporaryColors.Contains(hex);
        if (_userPaletteAdd is not null) _userPaletteAdd.IsEnabled = writable && HexColor.TryParse(_librarySaveHex.Text, out _);
        if (_userPaletteRemove is not null) _userPaletteRemove.IsEnabled = selected;
        if (_libraryEdit is not null) _libraryEdit.IsEnabled = selected;
        if (_temporarySave is not null) _temporarySave.IsEnabled = tempSelected;
        if (_temporaryRemove is not null) _temporaryRemove.IsEnabled = tempSelected;
        if (_temporaryClear is not null) _temporaryClear.IsEnabled = writable && _userPaletteStore!.TemporaryColors.Count > 0;
        var folderSelected = writable && (_libraryFolder.SelectedItem as LibraryFolderChoice)?.Id is not null;
        if (_folderRename is not null) _folderRename.IsEnabled = folderSelected;
        if (_folderDelete is not null) _folderDelete.IsEnabled = folderSelected;
        if (_libraryRecoveryActions is not null) _libraryRecoveryActions.IsVisible = _userPaletteStore is null || _userPaletteStore.LoadError is not null;
        ToolTip.SetTip(_userPaletteStatus, _userPaletteStatus.Text);
        if (_libraryRestore is not null)
            _libraryRestore.IsVisible = _userPaletteStore is { LoadError: not null } store && File.Exists(store.BackupPath);
    }

    private void RefreshLibraryFolders()
    {
        var old = _libraryFolder.SelectedItem as LibraryFolderChoice;
        var colors = _userPaletteStore?.Colors ?? Array.Empty<ColorLibraryColor>();
        var options = new List<LibraryFolderChoice>
        {
            new($"All colors ({colors.Count})", null, true),
            new($"Unfiled ({colors.Count(color => color.FolderId is null)})", null),
        };
        foreach (var folder in (_userPaletteStore?.Folders ?? Array.Empty<ColorLibraryFolder>()).OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            options.Add(new LibraryFolderChoice($"{folder.Name} ({colors.Count(c => c.FolderId == folder.Id)})", folder.Id));
        _syncingLibrary = true;
        try
        {
            _libraryFolder.ItemsSource = options;
            _libraryFolder.SelectedItem = options.FirstOrDefault(option => old is not null && option.Id == old.Id && option.IsAll == old.IsAll) ?? options[0];
        }
        finally { _syncingLibrary = false; }
    }

    private void RefreshUserPalette(string? message = null)
    {
        RefreshLibraryFolders();
        var folder = _libraryFolder.SelectedItem as LibraryFolderChoice;
        var results = _userPaletteStore?.Search(_librarySearch.Text, folder?.Id, folder is { IsAll: false, Id: null })
            ?? Array.Empty<ColorLibraryColor>();
        _libraryPage = Math.Clamp(_libraryPage, 0, Math.Max(0, (results.Count - 1) / LibraryPageSize));
        _userPaletteSwatches.Children.Clear();
        foreach (var color in results.Skip(_libraryPage * LibraryPageSize).Take(LibraryPageSize))
        {
            var captured = color;
            _userPaletteSwatches.Children.Add(BuildLibrarySwatch(color.Hex, color.Name, color.Id == _selectedLibraryColor, () =>
            {
                _selectedLibraryColor = captured.Id;
                ApplyStudioColor(ColorLibraryStore.ParseColor(captured.Hex));
                _userPaletteStatus.Text = "Selected " + captured.Hex + ".";
                RefreshLibrarySelection();
            }, "user-palette-" + color.Hex[1..]));
        }
        _userPaletteCount.Text = $"{results.Count} matching · {_userPaletteStore?.Colors.Count ?? 0} saved colors";
        _userPaletteEmpty.IsVisible = results.Count == 0;
        _userPaletteEmpty.Text = (_userPaletteStore?.Colors.Count ?? 0) == 0
            ? "Save current above, or choose More → Save by HEX."
            : "No matching colors. Clear the search or choose All colors.";
        SetLibraryPage(_libraryPage, results.Count, _libraryPageLabel, _libraryPrevious, _libraryNext);
        var temporary = _userPaletteStore?.TemporaryColors ?? Array.Empty<string>();
        _temporaryPage = Math.Clamp(_temporaryPage, 0, Math.Max(0, (temporary.Count - 1) / LibraryPageSize));
        _temporarySwatches.Children.Clear();
        for (var index = _temporaryPage * LibraryPageSize; index < Math.Min(temporary.Count, (_temporaryPage + 1) * LibraryPageSize); index++)
        {
            var hex = temporary[index];
            _temporarySwatches.Children.Add(BuildLibrarySwatch(hex, "Slot " + (index + 1), hex == _selectedTemporaryColor, () =>
            {
                _selectedTemporaryColor = hex;
                ApplyStudioColor(ColorLibraryStore.ParseColor(hex));
                _userPaletteStatus.Text = "Selected temporary color " + hex + ".";
                RefreshLibrarySelection();
            }, "temporary-color-" + hex[1..]));
        }
        _temporaryLibraryTab.Header = $"Temporary ({temporary.Count})";
        _temporaryCount.Text = temporary.Count == 0 ? "No temporary colors yet." : $"{temporary.Count} temporary colors · click a slot to use it";
        SetLibraryPage(_temporaryPage, temporary.Count, _temporaryPageLabel, _temporaryPrevious, _temporaryNext);
        _userPaletteStatus.Text = _userPaletteStore?.LoadError ?? message ?? "Saved on this computer · available after restarting.";
        UpdateUserPaletteButtons();
    }

    private static void SetLibraryPage(int page, int count, TextBlock label, Button? previous, Button? next)
    {
        var pages = Math.Max(1, (count + LibraryPageSize - 1) / LibraryPageSize);
        label.Text = $"{page + 1} / {pages}";
        label.IsVisible = pages > 1;
        if (previous is not null) previous.IsVisible = pages > 1;
        if (next is not null) next.IsVisible = pages > 1;
        if (previous is not null) previous.IsEnabled = page > 0;
        if (next is not null) next.IsEnabled = page + 1 < pages;
    }

    private Control BuildLibrarySwatch(string hex, string name, bool selected, Action action, string automationId)
    {
        var content = new StackPanel { Spacing = 2 };
        content.Children.Add(new Border { Height = 20, Background = CanvasBackdrop.Create(new CanvasDisplaySettings()), Child = new Border { Background = Brush(ColorLibraryStore.ParseColor(hex)) }, BorderBrush = EditorThemeTokens.PanelBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2) });
        content.Children.Add(new TextBlock { Text = hex, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center });
        if (!string.IsNullOrEmpty(name)) content.Children.Add(new TextBlock { Text = name, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center });
        var button = new Button { Width = 72, Height = 60, MinWidth = 0, MinHeight = 0, Padding = new Thickness(3), Content = content, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        if (selected) button.Classes.Add("selected");
        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, $"Use {name} {hex}");
        ToolTip.SetTip(button, string.IsNullOrEmpty(name) ? hex : name + " · " + hex);
        button.Click += (_, _) => action();
        return button;
    }

    private Rgba32 ActiveLibraryColor()
    {
        var session = Current();
        if (session is null) return _studioColor;
        var colors = session.GetToolColors();
        return _studioSecondaryTarget ? colors.Secondary : colors.Primary;
    }

    private void SaveUserPaletteColor()
    {
        if (!HexColor.TryParse(_librarySaveHex.Text, out var color))
        {
            _userPaletteStatus.Text = "Enter a valid #RRGGBB or #RRGGBBAA color to save.";
            return;
        }
        SaveLibraryColor(color);
    }

    private void SaveActiveLibraryColor()
    {
        if (!HexColor.TryParse(_studioHex.Text, out var color))
        {
            _studioHexHint.Text = "Enter a valid HEX before saving this color.";
            _studioHex.Focus(); return;
        }
        ApplyStudioColor(color);
        SaveLibraryColor(color);
    }

    private void SaveLibraryColor(Rgba32 color)
    {
        TryLibraryChange(() =>
        {
            var id = _userPaletteStore!.SaveColor(color, folderId: (_libraryFolder.SelectedItem as LibraryFolderChoice)?.Id);
            var savedColor = _userPaletteStore.Colors.First(c => c.Id == id);
            _syncingLibrary = true;
            try
            {
                _librarySearch.Text = string.Empty;
                var selectedFolder = _libraryFolder.SelectedItem as LibraryFolderChoice;
                if (selectedFolder is { IsAll: false } && selectedFolder.Id != savedColor.FolderId)
                    _libraryFolder.SelectedItem = ((IEnumerable<LibraryFolderChoice>)_libraryFolder.ItemsSource!).First(f => !f.IsAll && f.Id == savedColor.FolderId);
            }
            finally { _syncingLibrary = false; }
            _selectedLibraryColor = id;
            var filter = _libraryFolder.SelectedItem as LibraryFolderChoice;
            var visible = _userPaletteStore.Search(null, filter?.Id, filter is { IsAll: false, Id: null });
            _libraryPage = Math.Max(0, visible.ToList().FindIndex(item => item.Id == id) / LibraryPageSize);
            _librarySaveHex.Text = HexColor.Format(color);
        }, "Saved " + HexColor.Format(color) + ". Existing names and folders are kept.");
    }

    private void PinTemporaryColor()
    {
        if (!HexColor.TryParse(_studioHex.Text, out var current)) { ApplyStudioHex(); _studioHex.Focus(); return; }
        ApplyStudioColor(current);
        TryLibraryChange(() =>
        {
            var color = current;
            _userPaletteStore!.AddTemporary(color);
            _selectedTemporaryColor = HexColor.Format(color);
            _temporaryPage = Math.Max(0, _userPaletteStore.TemporaryColors.ToList().IndexOf(_selectedTemporaryColor) / LibraryPageSize);
        }, "Temporary color kept. Pick another pixel, then Add temp again.");
    }

    private void SaveSelectedTemporaryColor()
    {
        if (_selectedTemporaryColor is { } hex) SaveLibraryColor(ColorLibraryStore.ParseColor(hex));
    }

    private void RemoveSelectedTemporaryColor()
    {
        if (_selectedTemporaryColor is not { } hex) return;
        TryLibraryChange(() => { _userPaletteStore!.RemoveTemporary(hex); _selectedTemporaryColor = null; }, "Temporary slot removed. Saved colors are unchanged.");
    }

    private async Task ClearTemporaryLibraryAsync()
    {
        if (!await ConfirmLibraryAsync("Clear temporary colors", "Remove all temporary slots? Permanently saved colors will be kept.")) return;
        TryLibraryChange(() => { _userPaletteStore!.ClearTemporary(); _selectedTemporaryColor = null; }, "Temporary tray cleared.");
    }

    private async Task RemoveLibraryColorAsync()
    {
        if (_selectedLibraryColor is not { } id || _userPaletteStore is null) return;
        var selected = _userPaletteStore.Colors.FirstOrDefault(color => color.Id == id);
        if (selected is null || !await ConfirmLibraryAsync("Remove saved color", $"Remove {selected.Hex} from the library? Your artwork and current drawing color stay unchanged.")) return;
        TryLibraryChange(() => { _userPaletteStore!.RemoveColor(id); _selectedLibraryColor = null; }, "Saved color removed.");
    }

    private async Task CreateLibraryFolderAsync()
    {
        var name = await AskLibraryTextAsync("New color folder", "Folder name", string.Empty);
        if (name is null) return;
        TryLibraryChange(() =>
        {
            var id = _userPaletteStore!.CreateFolder(name);
            RefreshLibraryFolders();
            _libraryFolder.SelectedItem = ((IEnumerable<LibraryFolderChoice>)_libraryFolder.ItemsSource!).First(option => option.Id == id);
        }, "Folder created. New saves go into the selected folder.");
    }

    private async Task RenameLibraryFolderAsync()
    {
        if ((_libraryFolder.SelectedItem as LibraryFolderChoice)?.Id is not { } id || _userPaletteStore is null) return;
        var folder = _userPaletteStore.Folders.FirstOrDefault(item => item.Id == id);
        if (folder is null) return;
        var name = await AskLibraryTextAsync("Rename folder", "Folder name", folder.Name);
        if (name is not null) TryLibraryChange(() => _userPaletteStore!.RenameFolder(id, name), "Folder renamed.");
    }

    private async Task DeleteLibraryFolderAsync()
    {
        if ((_libraryFolder.SelectedItem as LibraryFolderChoice)?.Id is not { } id) return;
        if (!await ConfirmLibraryAsync("Delete folder", "Delete this folder? Every color inside will move to Unfiled. No colors will be deleted.")) return;
        TryLibraryChange(() => _userPaletteStore!.DeleteFolder(id), "Folder deleted. Its colors are in Unfiled.");
    }

    private async Task EditLibraryColorAsync()
    {
        var saved = _userPaletteStore?.Colors.FirstOrDefault(color => color.Id == _selectedLibraryColor);
        if (saved is null || _userPaletteStore is null) return;
        var name = new TextBox { Text = saved.Name, MaxLength = 120, PlaceholderText = "Optional color name" };
        var options = new List<LibraryFolderChoice> { new("Unfiled", null) };
        options.AddRange(_userPaletteStore.Folders.Select(folder => new LibraryFolderChoice(folder.Name, folder.Id)));
        var folder = new ComboBox { ItemsSource = options, SelectedItem = options.First(item => item.Id == saved.FolderId), HorizontalAlignment = HorizontalAlignment.Stretch };
        var body = new StackPanel { Spacing = 9 };
        var window = LibraryDialog("Edit " + saved.Hex, body);
        var errorText = new TextBlock { TextWrapping = TextWrapping.Wrap };
        body.Children.Add(new TextBlock { Text = "Color name" });
        body.Children.Add(name);
        body.Children.Add(new TextBlock { Text = "Folder" });
        body.Children.Add(folder);
        body.Children.Add(errorText);
        body.Children.Add(LibraryRow(LibraryButton("Save", () =>
        {
            if (TryLibraryChange(() => _userPaletteStore!.UpdateColor(saved.Id, name.Text ?? string.Empty, (folder.SelectedItem as LibraryFolderChoice)?.Id), "Color details saved.")) window.Close();
            else errorText.Text = _userPaletteStatus.Text;
        }), LibraryButton("Cancel", () => window.Close())));
        window.KeyDown += (_, e) => { if (e.Key == Key.Escape) { window.Close(); e.Handled = true; } };
        window.Opened += (_, _) => { name.Focus(); name.SelectAll(); };
        await window.ShowDialog(this);
    }

    private async Task RestoreColorLibraryAsync()
    {
        if (_userPaletteStore is null || !await ConfirmLibraryAsync("Restore color-library backup", "Restore the previous saved library? It may not include the most recent change. The damaged file will be kept separately for recovery.")) return;
        try
        {
            _userPaletteStore.RestoreBackup();
            RefreshUserPalette("Backup restored. The damaged file, if present, was kept beside the library.");
        }
        catch (Exception error) when (LibraryFailure(error)) { _userPaletteStatus.Text = "Backup was not restored: " + error.Message; }
    }

    private bool TryLibraryChange(Action action, string message)
    {
        if (_userPaletteStore is null) { _userPaletteStatus.Text = "The library is unavailable. Choose Reload."; return false; }
        try { action(); RefreshUserPalette(message); return true; }
        catch (Exception error) when (LibraryFailure(error))
        {
            _userPaletteStatus.Text = "Change not saved: " + error.Message;
            SetError(_userPaletteStatus.Text);
            UpdateUserPaletteButtons();
            return false;
        }
    }

    private static bool LibraryFailure(Exception error) => error is IOException or InvalidDataException
        or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException or NotSupportedException;

    private static Window LibraryDialog(string title, Control body) => new()
    {
        Title = title, Width = 400, CanResize = false, SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Background = EditorThemeTokens.AppBackground,
        Content = new Border { Padding = new Thickness(16), Child = body },
    };

    private async Task<string?> AskLibraryTextAsync(string title, string label, string value)
    {
        var input = new TextBox { Text = value, MaxLength = 80 };
        var hint = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var body = new StackPanel { Spacing = 9 };
        var window = LibraryDialog(title, body);
        void Submit()
        {
            var text = (input.Text ?? string.Empty).Trim();
            if (text.Length == 0) { hint.Text = "Enter a folder name."; return; }
            window.Close(text);
        }
        body.Children.Add(new TextBlock { Text = label });
        body.Children.Add(input);
        body.Children.Add(hint);
        body.Children.Add(LibraryRow(LibraryButton("Save", Submit), LibraryButton("Cancel", () => window.Close(null))));
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Submit(); e.Handled = true; }
            else if (e.Key == Key.Escape) { window.Close(null); e.Handled = true; }
        };
        window.Opened += (_, _) => input.Focus();
        return await window.ShowDialog<string?>(this);
    }

    private async Task<bool> ConfirmLibraryAsync(string title, string message)
    {
        var body = new StackPanel { Spacing = 12 };
        var window = LibraryDialog(title, body);
        body.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        var cancel = LibraryButton("Cancel", () => window.Close(false));
        var confirm = LibraryButton(title.StartsWith("Delete", StringComparison.Ordinal) ? "Delete folder" :
            title.StartsWith("Clear", StringComparison.Ordinal) ? "Clear temporary" :
            title.StartsWith("Restore", StringComparison.Ordinal) ? "Restore backup" : "Remove color", () => window.Close(true));
        confirm.Classes.Add("danger");
        body.Children.Add(LibraryRow(cancel, confirm));
        window.Opened += (_, _) => cancel.Focus();
        window.KeyDown += (_, e) => { if (e.Key == Key.Escape) { window.Close(false); e.Handled = true; } };
        return await window.ShowDialog<bool?>(this) == true;
    }
}
