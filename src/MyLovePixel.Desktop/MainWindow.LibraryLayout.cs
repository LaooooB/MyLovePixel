using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private Control BuildSavedLibraryLayout(Control folders, Control save)
    {
        var search = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 5 };
        search.Children.Add(_librarySearch);
        var clear = LibraryButton("Clear", () => { _librarySearch.Text = string.Empty; _librarySearch.Focus(); }, "color-library-search-clear");
        clear.IsEnabled = false;
        search.Children.Add(Place(clear, 1));
        _librarySearch.TextChanged += (_, _) => clear.IsEnabled = !string.IsNullOrEmpty(_librarySearch.Text);
        _librarySearch.KeyDown += (_, e) => { if (e.Key == Key.Escape) { _librarySearch.Text = string.Empty; e.Handled = true; } };
        var header = new StackPanel { Spacing = 5 };
        header.Children.Add(search); header.Children.Add(folders); header.Children.Add(_userPaletteCount);
        var folderActions = LibraryButton("More", () => { }, "color-library-more");
        ToolTip.SetTip(folderActions, "Save a HEX color, manage the selected folder or reload the library.");
        var extra = new StackPanel { Spacing = 8, Width = 290 };
        extra.Children.Add(new TextBlock { Text = "Save by HEX" });
        extra.Children.Add(save);
        extra.Children.Add(LibraryRow(_folderRename!, _folderDelete!));
        extra.Children.Add(LibraryButton("Reload library", ReloadColorLibrary));
        var menu = new Flyout { Placement = PlacementMode.Bottom, Content = extra };
        folderActions.Click += (_, _) => menu.ShowAt(folderActions);
        _folderRename!.Click += (_, _) => menu.Hide();
        _folderDelete!.Click += (_, _) => menu.Hide();
        var footer = new StackPanel { Spacing = 4 };
        footer.Children.Add(LibraryRow(_libraryPrevious!, _libraryPageLabel, _libraryNext!, _libraryEdit!, _userPaletteRemove!, folderActions));
        var swatches = new StackPanel { Spacing = 4 };
        swatches.Children.Add(_userPaletteEmpty); swatches.Children.Add(_userPaletteSwatches);
        return LibraryPageLayout(header, swatches, footer);
    }

    private Control BuildTemporaryLibraryLayout()
    {
        var header = new StackPanel { Spacing = 5 };
        header.Children.Add(new TextBlock { Text = "Pick a pixel, then Add temp. Kept until removed.", TextWrapping = TextWrapping.Wrap });
        header.Children.Add(_temporaryCount);
        var footer = new StackPanel { Spacing = 4 };
        footer.Children.Add(LibraryRow(_temporaryPrevious!, _temporaryPageLabel, _temporaryNext!));
        footer.Children.Add(LibraryRow(_temporarySave!, _temporaryRemove!, _temporaryClear!));
        return LibraryPageLayout(header, _temporarySwatches, footer);
    }

    private static Control LibraryPageLayout(Control header, Control swatches, Control footer)
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 5 };
        root.Children.Add(header);
        var scroll = new ScrollViewer { Content = swatches,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1); root.Children.Add(scroll);
        Grid.SetRow(footer, 2); root.Children.Add(footer);
        return root;
    }
}
