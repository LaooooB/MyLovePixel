using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private bool _gestureRackUxInstalled;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_gestureRackUxInstalled) return;
        _gestureRackUxInstalled = true;

        // Pixel rectangles must stay hard-edged at fractional zoom values. The grid
        // itself remains a separate overlay and is still controlled by _gridVisible.
        RenderOptions.SetEdgeMode(_canvas, EdgeMode.Aliased);
        _canvas.SetGrid(_gridVisible);

        InstallClearCanvasButton();
        SyncGridShortcutButton();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None ||
            e.Source is TextBox or NumericUpDown or ComboBox or Slider)
            return;

        var toolIndex = e.Key switch
        {
            Key.D1 => 0,
            Key.D2 => 1,
            Key.D3 => 2,
            Key.D4 => 3,
            Key.D5 => 4,
            Key.D6 => 5,
            Key.D7 => 6,
            Key.D8 => 7,
            Key.D9 => 8,
            Key.D0 => 9,
            _ => -1,
        };

        if (toolIndex >= 0)
        {
            SelectToolShortcut(toolIndex);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.G)
        {
            _gridVisible = !_gridVisible;
            _canvas.SetGrid(_gridVisible);
            SyncGridShortcutButton();
            e.Handled = true;
        }
    }

    private void SelectToolShortcut(int index)
    {
        var session = Current();
        if (session is null) return;
        var tools = _plugins.GetTools(session);
        if ((uint)index >= (uint)tools.Count) return;
        SelectQuickTool(tools[index].Id);
    }

    private void InstallClearCanvasButton()
    {
        var row = FindToolbarRow("History");
        if (row is null) return;

        var clear = IconButton("×", "Clear canvas · Ctrl+Z to undo", ClearCanvas);
        row.Children.Add(clear);
    }

    private void ClearCanvas()
    {
        var session = Current();
        if (session is null) return;
        Safe(() => session.ClearCurrentCanvas());
        _selection.Clear(session);
        RefreshAll();
    }

    private void SyncGridShortcutButton()
    {
        var row = FindToolbarRow("View");
        if (row is null) return;

        foreach (var button in row.Children.OfType<Button>())
        {
            if (button.Content is not string text || !text.StartsWith("Grid ", StringComparison.Ordinal))
                continue;

            button.Content = _gridVisible ? "Grid On" : "Grid Off";
            ToolTip.SetTip(button, "Show or hide the pixel grid · G");
            if (_gridVisible)
            {
                if (!button.Classes.Contains("selected")) button.Classes.Add("selected");
            }
            else
            {
                button.Classes.Remove("selected");
            }
            break;
        }
    }

    private StackPanel? FindToolbarRow(string title)
    {
        if (Content is not DockPanel root) return null;

        foreach (var border in root.Children.OfType<Border>())
        {
            if (border.Child is not Grid grid) continue;
            foreach (var group in grid.Children.OfType<StackPanel>())
            {
                var heading = group.Children.OfType<TextBlock>().FirstOrDefault();
                if (!string.Equals(heading?.Text, title, StringComparison.Ordinal)) continue;
                return group.Children
                    .OfType<StackPanel>()
                    .FirstOrDefault(value => value.Orientation == Orientation.Horizontal);
            }
        }

        return null;
    }

    private void SetToolOptionFromSlider(DocumentSession session, string id, int value)
    {
        if (!ReferenceEquals(Current(), session)) return;

        // Tool option changes are UI-local state, but DocumentSession broadcasts the
        // same StateChanged event used for document mutations. Temporarily detach this
        // window so dragging does not rebuild the inspector and lose pointer capture.
        session.StateChanged -= OnSessionChanged;
        try
        {
            session.SetToolOption(id, value);
        }
        finally
        {
            session.StateChanged += OnSessionChanged;
        }
    }
}
