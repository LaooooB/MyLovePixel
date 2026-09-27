using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private bool? _compactHeight;
    private bool _restoreTimeline = true;

    private T DocumentControl<T>(T control) where T : Control
    {
        _documentControls.Add(control);
        return control;
    }

    private void UpdateAdaptiveLayout()
    {
        if (_workspaceGrid is null) return;
        var width = Bounds.Width > 0 ? Bounds.Width : Width;
        var height = Bounds.Height > 0 ? Bounds.Height : Height;
        var compactWidth = width < 1050;
        _workspaceGrid.ColumnDefinitions[0].Width = new GridLength(compactWidth ? 144 : EditorThemeTokens.ToolRailWidth);
        _workspaceGrid.ColumnDefinitions[2].Width = new GridLength(width < 760 ? 280 : width < 1050 ? 308 : EditorThemeTokens.RightPanelWidth);
        if (_fullViewOptions is not null) _fullViewOptions.IsVisible = !compactWidth;
        if (_compactViewOptions is not null) _compactViewOptions.IsVisible = compactWidth;

        var compactHeight = height < 650;
        // Change defaults only on crossing a breakpoint; subsequent edits must
        // not overwrite the user's explicit expansion choices.
        if (_compactHeight != compactHeight)
        {
            if (compactHeight)
            {
                _restoreTimeline = _timelineExpander.IsExpanded;
                _timelineExpander.IsExpanded = false;
            }
            else if (_compactHeight.HasValue)
            {
                _timelineExpander.IsExpanded = _restoreTimeline;
            }
            _compactHeight = compactHeight;
        }
        // An explicitly reopened timeline remains scrollable on small screens.
        if (_timelineContent is not null) _timelineContent.MaxHeight = Math.Clamp(height * .26, 96, 230);
        _quickPreview.Height = height < 500 ? 24 : compactHeight ? 64 : 128;
        if (_previewZoomControls is not null) _previewZoomControls.IsVisible = height >= 500;
    }

    private void ShowViewMenu()
    {
        if (_compactViewOptions is not Control anchor) return;
        var grid = new MenuItem { Header = "Pixel grid", ToggleType = MenuItemToggleType.CheckBox, IsChecked = _gridVisible };
        grid.Click += (_, _) =>
        {
            _gridVisible = grid.IsChecked;
            _canvas.SetGrid(_gridVisible);
            SyncGridShortcutButton();
        };
        var invert = new MenuItem { Header = "Invert view", ToggleType = MenuItemToggleType.CheckBox, IsChecked = _invertView };
        invert.Click += (_, _) => { _invertView = invert.IsChecked; _canvas.SetInvert(_invertView); };
        new MenuFlyout { ItemsSource = new[] { grid, invert }, Placement = PlacementMode.Bottom }.ShowAt(anchor);
    }
}
