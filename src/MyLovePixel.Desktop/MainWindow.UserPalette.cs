using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private readonly UserPaletteStore _userPaletteStore;
    private readonly TextBox _personalHex = new() { PlaceholderText = "#654321", MaxLength = 32, MinWidth = 0 };
    private readonly TextBox _personalName = new() { PlaceholderText = "Color name", MaxLength = UserPaletteStore.MaxNameLength, MinWidth = 0 };
    private readonly TextBox _personalSearch = new() { PlaceholderText = "Search name, HEX or folder", MinWidth = 0 };
    private readonly ColorSwatchView _personalPreview = new() { Width = 30, Height = 30 };
    private readonly TextBlock _personalCount = new() { Foreground = EditorThemeTokens.TextSecondary, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
    private readonly TextBlock _personalValidation = new() { Foreground = EditorThemeTokens.Danger, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly TextBlock _personalEmpty = new() { Text = "No saved colors", Foreground = EditorThemeTokens.TextSecondary, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12) };
    private readonly ListBox _personalList = new() { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0) };
    private readonly Button _personalSave = new() { Content = "Save color", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _personalRename = new() { Content = "Rename" };
    private readonly Button _personalRemove = new() { Content = "Remove" };
    private readonly Button _personalMove = new() { Content = "Move…" };
    private readonly ComboBox _folderFilter = new() { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 0 };
    private readonly ComboBox _personalFolder = new() { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 0 };
    private readonly Button _folderManage = new() { Content = "Folders…", Padding = new Thickness(7, 4) };
    private readonly Border _personalEditorHost = new() { IsVisible = false };
    private Rgba32? _selectedPersonalColor;
    private bool _personalDraftEdited;
    private bool _syncingPersonal;
    private bool _syncingFolder;
    private bool _syncingList;
    private bool _personalBuilt;
    private string? _folderSignature;
    private IReadOnlyList<SavedColor> _visiblePersonalColors = Array.Empty<SavedColor>();
    private Grid? _colorPageBody;
    private ScrollViewer? _colorPageScroll;

    private sealed record FolderChoice(string? Id, string Name)
    {
        public override string ToString() => Name;
    }

    private Control BuildColorsPage()
    {
        _colorPageBody = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        _colorEditor = DocumentControl(BuildColorEditor());
        _colorPageBody.Children.Add(_colorEditor);
        _colorPageBody.Children.Add(PlaceRow(BuildQuickColors(), 1));
        _colorPageBody.Children.Add(PlaceRow(BuildUserPaletteEditor(), 2));
        _colorPageScroll = new ScrollViewer
        {
            Content = _colorPageBody, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _colorPageScroll.SizeChanged += (_, _) => SizeColorPage();
        return _colorPageScroll;
    }

    private void SizeColorPage()
    {
        if (_colorPageBody is null || _colorPageScroll is null) return;
        // Normally only the virtualized results scroll. Small windows also allow
        // the complete form to scroll, so saving never strands its buttons.
        var minimum = _personalEditorHost.IsVisible ? 520 : 330;
        _colorPageBody.Height = Math.Max(minimum, _colorPageScroll.Bounds.Height);
    }

    private Control BuildUserPaletteEditor()
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), RowSpacing = 7, Margin = new Thickness(9, 3, 9, 8) };
        Named(root, "palette.personal", "My palette");
        var filters = new StackPanel { Spacing = 6 };
        var search = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4 };
        search.Children.Add(Named(_personalSearch, "palette.search", "Search saved colors by name, HEX or folder"));
        search.Children.Add(Place(Named(SlimButton("Clear", () => _personalSearch.Text = string.Empty), "palette.search.clear", "Clear color search"), 1));
        filters.Children.Add(search);
        var folders = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4 };
        folders.Children.Add(Named(_folderFilter, "palette.folder.filter", "Filter color folder"));
        folders.Children.Add(Place(Named(_folderManage, "palette.folder.manage", "Create or manage color folders"), 1));
        filters.Children.Add(folders);
        root.Children.Add(filters);

        _personalEditorHost.Child = BuildPersonalForm();
        root.Children.Add(PlaceRow(_personalEditorHost, 1));
        _personalList.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        { Setters = { new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch), new Setter(ListBoxItem.PaddingProperty, new Thickness(0)) } });
        _personalList.ItemTemplate = new FuncDataTemplate<SavedColor>((saved, _) => saved is null ? new Border() : BuildPersonalRow(saved));
        _personalList.SelectionChanged += (_, _) =>
        {
            if (!_syncingList && _personalList.SelectedItem is SavedColor saved) SelectPersonalColor(saved.Color);
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_personalList, ScrollBarVisibility.Disabled);
        var results = new Grid();
        results.Children.Add(Named(_personalList, "palette.results", "Saved colors"));
        results.Children.Add(Named(_personalEmpty, "palette.empty", "Palette results"));
        root.Children.Add(PlaceRow(results, 2));
        var footer = new StackPanel { Spacing = 4 };
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 4 };
        actions.Children.Add(Named(SlimButton("New", OpenPersonalPalette), "palette.new", "Save a new named color"));
        actions.Children.Add(Place(Named(_personalRename, "palette.rename", "Rename selected saved color"), 1));
        actions.Children.Add(Place(Named(_personalMove, "palette.move", "Move selected color to a folder"), 2));
        actions.Children.Add(Place(Named(_personalRemove, "palette.remove", "Remove selected saved color"), 3));
        foreach (var button in actions.Children.OfType<Button>()) { button.Padding = new Thickness(5, 4); button.FontSize = 12; button.HorizontalAlignment = HorizontalAlignment.Stretch; }
        footer.Children.Add(actions);
        footer.Children.Add(_personalCount);
        // Errors stay visible even when the save form is closed.
        footer.Children.Add(Named(_personalValidation, "palette.validation", "Palette validation"));
        root.Children.Add(PlaceRow(footer, 3));

        _personalSearch.TextChanged += (_, _) => FilterPersonalRows();
        _folderFilter.SelectionChanged += (_, _) => { if (!_syncingFolder) FilterPersonalRows(); };
        _folderManage.Click += (_, _) => ShowFolderMenu();
        _personalSave.Click += (_, _) => SavePersonalColor();
        _personalRename.Click += (_, _) =>
        {
            if (_selectedPersonalColor is not { } color) return;
            ShowPersonalEditor(); FillPersonalDraft(color); FocusPersonalName();
        };
        _personalMove.Click += async (_, _) => await MovePersonalColorAsync();
        _personalRemove.Click += (_, _) => RemovePersonalColor();
        ToolTip.SetTip(_personalCount, _userPaletteStore.FilePath);
        _personalHex.TextChanging += (_, _) => PersonalDraftChanged();
        _personalName.TextChanging += (_, _) => PersonalDraftChanged();
        _personalFolder.SelectionChanged += (_, _) => PersonalDraftChanged();
        _personalHex.KeyDown += PersonalInputKey; _personalName.KeyDown += PersonalInputKey;
        _personalBuilt = true;
        RefreshPersonalRows(); FillPersonalDraft(_studioColor);
        Activated += (_, _) => { _userPaletteStore.Reload(); RefreshPersonalRows(); RefreshQuickColors(); };
        return root;
    }

    private Control BuildPersonalForm()
    {
        var form = new StackPanel { Spacing = 6 };
        var hex = new Grid { ColumnDefinitions = new ColumnDefinitions("40,*,Auto"), ColumnSpacing = 5 };
        hex.Children.Add(FieldLabel("HEX"));
        hex.Children.Add(Place(Named(_personalHex, "palette.hex", "Saved color HEX"), 1));
        hex.Children.Add(Place(_personalPreview, 2)); form.Children.Add(hex);
        var name = new Grid { ColumnDefinitions = new ColumnDefinitions("40,*"), ColumnSpacing = 5 };
        name.Children.Add(FieldLabel("Name")); name.Children.Add(Place(Named(_personalName, "palette.name", "Saved color name"), 1)); form.Children.Add(name);
        var folder = new Grid { ColumnDefinitions = new ColumnDefinitions("40,*"), ColumnSpacing = 5 };
        folder.Children.Add(FieldLabel("Folder")); folder.Children.Add(Place(Named(_personalFolder, "palette.folder.target", "Save color in folder"), 1)); form.Children.Add(folder);
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 5 };
        actions.Children.Add(Named(_personalSave, "palette.save", "Save named color"));
        actions.Children.Add(Place(Named(SlimButton("Close", () => { _personalEditorHost.IsVisible = false; SizeColorPage(); }), "palette.editor.close", "Close saved color form"), 1));
        form.Children.Add(actions);
        return new Border { Padding = new Thickness(8), Background = EditorThemeTokens.SurfaceRaised, CornerRadius = EditorThemeTokens.CardRadius, Child = form };
    }

    private Control BuildPersonalRow(SavedColor saved)
    {
        var label = new TextBlock { Text = saved.Name, FontSize = 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        var details = HexColor.Format(saved.Color);
        if (saved.FolderId is { } id && _userPaletteStore.Folders.FirstOrDefault(f => f.Id == id) is { } folder) details += "  ·  " + folder.Name;
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(label);
        text.Children.Add(new TextBlock { Text = details, FontSize = 12, Foreground = EditorThemeTokens.TextSecondary, TextWrapping = TextWrapping.Wrap });
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("32,*"), ColumnSpacing = 8 };
        content.Children.Add(new ColorSwatchView { Color = saved.Color, Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center });
        content.Children.Add(Place(text, 1));
        var button = new Button
        {
            Content = content, Padding = new Thickness(7, 5), MinHeight = 48, Margin = new Thickness(0, 1),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        Named(button, "palette.swatch." + HexColor.Format(saved.Color)[1..], $"{saved.Name}, {HexColor.Format(saved.Color)}");
        button.Click += (_, _) => SelectPersonalColor(saved.Color);
        return button;
    }

    private void OpenPersonalPalette()
    {
        if (!TryParseHex(_studioHex.Text, out var color)) { ApplyStudioHex(); return; }
        _sideTabs.SelectedIndex = 0;
        ShowPersonalEditor(); FillPersonalDraft(color); FocusPersonalName();
    }
    private void ShowPersonalEditor() { _personalEditorHost.IsVisible = true; SizeColorPage(); }
    private void FocusPersonalName() => Avalonia.Threading.Dispatcher.UIThread.Post(() => { _personalName.BringIntoView(); _personalName.Focus(); _personalName.SelectAll(); });
    private void PersonalInputKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { SavePersonalColor(); e.Handled = true; }
        else if (e.Key == Key.Escape) { FillPersonalDraft(_selectedPersonalColor ?? _studioColor); e.Handled = true; }
    }
    private void PersonalDraftChanged()
    {
        if (_syncingPersonal || !_personalBuilt || _syncingFolder) return;
        _personalDraftEdited = true; _personalValidation.IsVisible = false; UpdatePersonalActions();
    }
    private void FillPersonalDraft(Rgba32 color)
    {
        if (!_personalBuilt) return;
        _syncingPersonal = true;
        try
        {
            var saved = _userPaletteStore.Swatches.FirstOrDefault(s => s.Color == color);
            _personalHex.Text = HexColor.Format(color); _personalName.Text = saved?.Name ?? string.Empty;
            var folderId = saved is not null ? saved.FolderId : ((_folderFilter.SelectedItem as FolderChoice)?.Id is { } id && id != "*" ? id : null);
            _personalFolder.SelectedItem = _personalFolder.Items.Cast<FolderChoice>().FirstOrDefault(f => f.Id == folderId);
            _personalValidation.IsVisible = false; _personalDraftEdited = false;
        }
        finally { _syncingPersonal = false; }
        UpdatePersonalActions();
    }
    private void SyncPersonalColor(Rgba32 color)
    { if (_personalBuilt && !_personalDraftEdited) FillPersonalDraft(color); }
    private void UpdatePersonalActions()
    {
        var valid = HexColor.TryParse(_personalHex.Text, out var color);
        var exists = valid && _userPaletteStore.Swatches.Any(s => s.Color == color);
        _personalSave.Content = exists ? "Save changes" : "Save color";
        _personalSave.IsEnabled = _userPaletteStore.LoadError is null && valid && (exists || _userPaletteStore.Swatches.Count < UserPaletteStore.MaxColors);
        _personalPreview.Color = valid ? color : Rgba32.Transparent;
        var selected = _selectedPersonalColor is { } chosen && _userPaletteStore.Swatches.Any(s => s.Color == chosen);
        _personalRename.IsEnabled = _personalMove.IsEnabled = _personalRemove.IsEnabled = selected && _userPaletteStore.LoadError is null;
        if (_userPaletteStore.LoadError is { } error) ShowPersonalError(error);
    }
    private void FilterPersonalRows()
    {
        RefreshPersonalRows();
        if (_visiblePersonalColors.Count > 0) _personalList.ScrollIntoView(_visiblePersonalColors[0]);
    }
    private void RefreshPersonalRows()
    {
        if (!_personalBuilt) return;
        RefreshFolderChoices();
        var folder = _folderFilter.SelectedItem as FolderChoice;
        var wanted = _userPaletteStore.Query(_personalSearch.Text, folder?.Id == "*" ? null : folder?.Id, folder is { Id: null });
        if (!_visiblePersonalColors.SequenceEqual(wanted))
        {
            _visiblePersonalColors = wanted;
            _syncingList = true;
            try { _personalList.ItemsSource = wanted; _personalList.SelectedItem = wanted.FirstOrDefault(s => s.Color == _selectedPersonalColor); }
            finally { _syncingList = false; }
        }
        if (_selectedPersonalColor is { } old && !_userPaletteStore.Swatches.Any(s => s.Color == old)) _selectedPersonalColor = null;
        _personalCount.Text = wanted.Count == _userPaletteStore.Swatches.Count ? $"{wanted.Count} saved colors" : $"{wanted.Count} of {_userPaletteStore.Swatches.Count} colors";
        _personalEmpty.Text = _userPaletteStore.Swatches.Count == 0 ? "Save your first color with New." : "No matching colors";
        _personalEmpty.IsVisible = wanted.Count == 0;
        UpdatePersonalActions();
    }
    private void SelectPersonalColor(Rgba32 color)
    {
        _selectedPersonalColor = color;
        ApplyStudioColor(color); FillPersonalDraft(color);
        _syncingList = true;
        try { _personalList.SelectedItem = _visiblePersonalColors.FirstOrDefault(s => s.Color == color); }
        finally { _syncingList = false; }
        UpdatePersonalActions();
    }
    private void SavePersonalColor()
    {
        if (!HexColor.TryParse(_personalHex.Text, out var color)) { ShowPersonalError("Enter #RRGGBB or #RRGGBBAA."); return; }
        try
        {
            var folder = (_personalFolder.SelectedItem as FolderChoice)?.Id;
            if (_userPaletteStore.Swatches.Any(s => s.Color == color)) _userPaletteStore.Update(color, _personalName.Text, folder);
            else _userPaletteStore.Add(color, _personalName.Text, folder);
            _selectedPersonalColor = color;
            _personalSearch.Text = string.Empty;
            _folderFilter.SelectedItem = _folderFilter.Items.Cast<FolderChoice>().First(f => f.Id == (folder ?? "*"));
            RefreshPersonalRows(); SelectPersonalColor(color);
            _personalEditorHost.IsVisible = false; SizeColorPage();
            _personalList.ScrollIntoView(_personalList.SelectedItem!);
        }
        catch (Exception ex) when (IsPaletteError(ex)) { ShowPersonalError($"Not saved. {ex.Message}"); }
    }
    private void RemovePersonalColor()
    {
        if (_selectedPersonalColor is not { } color) return;
        try { _userPaletteStore.Remove(color); _selectedPersonalColor = null; RefreshPersonalRows(); FillPersonalDraft(_studioColor); }
        catch (Exception ex) when (IsPaletteError(ex)) { ShowPersonalError($"Not removed. {ex.Message}"); }
    }
    private void ShowPersonalError(string message) { _personalValidation.Text = message; _personalValidation.IsVisible = true; }
    private static bool IsPaletteError(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException;
    private static TextBlock FieldLabel(string text) => new() { Text = text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = EditorThemeTokens.TextSecondary };
    private static Button SlimButton(string text, Action action)
    { var button = new Button { Content = text, Padding = new Thickness(7, 4), MinHeight = 30, FontSize = 12 }; button.Click += (_, _) => action(); return button; }
    private static T PlaceRow<T>(T control, int row) where T : Control { Grid.SetRow(control, row); return control; }
}
