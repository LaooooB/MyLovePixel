using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private bool _eyedropperMode;
    private bool _altHeld;
    private bool _spaceHeld;
    private bool _pickerCurrentLayer;
    private readonly Dictionary<string, Button> _toolButtons = [];
    private string? _toolSignature;
    private string? _optionsSignature;
    private Button? _gridButton;

    private void InstallUxInput()
    {
        Named(_canvas, "workspace.canvas", "Pixel canvas");
        _canvas.SamplingRequested = () => _eyedropperMode || _altHeld;
        _canvas.PanningRequested = () => _spaceHeld;
        _canvas.PixelSampleRequested = PickCanvasColor;
        _canvas.PanDeltaRequested = delta => { _navigationVersion++; _canvasScroll.Offset -= delta; };
        InstallWorkspacePanning();
        _canvas.ZoomAtRequested = ChangeZoomAt;
        KeyUp += OnUxKeyUp;
        Deactivated += (_, _) =>
        {
            _altHeld = _spaceHeld = false;
            CancelWorkspacePanning();
            _canvas.CancelActivePointer();
            CancelSelectionTransformGesture();
            UpdateCanvasCursor();
        };
        Closing += OnWindowClosing;
    }

    private void RefreshTools()
    {
        var session = Current();
        if (session is null) { _toolsPanel.Children.Clear(); _toolButtons.Clear(); _toolSignature = null; return; }
        var tools = _plugins.GetTools(session);
        var signature = string.Join('|', tools.Select(t => t.Id + ':' + t.DisplayName));
        if (signature != _toolSignature)
        {
            _toolSignature = signature;
            _toolButtons.Clear();
            _toolsPanel.Children.Clear();
            AddTool("workspace.selection", "Selection", "▧");
            AddTool("core.eyedropper", "Eyedropper", "");
            _toolsPanel.Children.Add(SeparatorH());
            foreach (var tool in tools) AddTool(tool.Id, tool.DisplayName, ToolGlyph(tool.Id));
        }
        var activeId = _eyedropperMode ? "core.eyedropper" : _selectionMode ? "workspace.selection" : tools.FirstOrDefault(t => t.IsActive)?.Id;
        foreach (var pair in _toolButtons) SetSelected(pair.Value, pair.Key == activeId);
        UpdateCanvasCursor();
    }

    private void AddTool(string id, string name, string glyph)
    {
        var shortcut = EditorToolShortcuts.ForTool(id);
        var tip = shortcut is null ? name : $"{name} · {shortcut}";
        var button = TextIconButton(glyph, name, tip, () => SelectQuickTool(id));
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.Padding = new Thickness(7, 6);
        button.MinHeight = 36;
        if (shortcut is not null && button.Content is Control content)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4 };
            button.Content = null;
            row.Children.Add(content);
            row.Children.Add(Place(new TextBlock { Text = shortcut, Foreground = EditorThemeTokens.TextSecondary, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, 1));
            button.Content = row;
            AutomationProperties.SetAcceleratorKey(button, shortcut);
        }
        Named(button, "tool." + id, name);
        ToolTip.SetPlacement(button, PlacementMode.Right);
        _toolsPanel.Children.Add(button);
        _toolButtons.Add(id, button);
    }

    private void SelectQuickTool(string id)
    {
        var session = Current();
        if (session is null || _busy) return;
        FinishParameterEdit();
        _canvas.CancelActivePointer();
        CancelSelectionTransformGesture();
        _plugins.CancelTool(session);
        _eyedropperMode = id == "core.eyedropper";
        _selectionMode = id == "workspace.selection";
        if (!_eyedropperMode && !_selectionMode) _plugins.SelectTool(session, id);
        _optionsSignature = null;
        RefreshTools();
        RefreshToolOptions();
        RefreshCanvas(updatePreview: false);
        RefreshStatus();
        _canvas.Focus();
    }

    private bool ReuseToolOptions()
    {
        var session = Current();
        var active = session is null ? "none" : _plugins.GetTools(session).FirstOrDefault(t => t.IsActive)?.Id;
        var signature = $"{session?.GetHashCode()}:{session?.CurrentLayerId}:{session?.CurrentFrameId}:{session?.HasEditableCel}:{_eyedropperMode}:{_selectionMode}:{_selectionGesture}:{active}";
        if (_optionsSignature == signature) return true;
        _optionsSignature = signature;
        if (!_eyedropperMode) return false;
        _toolOptionsPanel.Children.Clear();
        var source = new ComboBox { ItemsSource = new[] { "Visible layers", "Current layer" }, SelectedIndex = _pickerCurrentLayer ? 1 : 0 };
        Named(source, "eyedropper.source", "Eyedropper sampling source");
        source.SelectionChanged += (_, _) => _pickerCurrentLayer = source.SelectedIndex == 1;
        _toolOptionsPanel.Children.Add(Labeled("Sample", source));
        _toolOptionsPanel.Children.Add(new TextBlock { Text = "Hold Alt to pick while drawing.", TextWrapping = TextWrapping.Wrap, Foreground = EditorThemeTokens.TextSecondary });
        return true;
    }

    private void PickCanvasColor(int x, int y)
    {
        if (_busy || Current() is not { } session) return;
        _playback.Stop(session);
        try
        {
            var sample = _plugins.SampleCanvasPixel(session, x, y, _pickerCurrentLayer);
            if (sample is null) return;
            session.ApplySampledColor(sample, _studioSecondaryTarget);
            _selectedPalette = sample.PaletteId;
            _selectedPaletteIndex = sample.PaletteIndex;
            SyncStudioColor(sample.Color);
            RefreshPalette();
            _sampleInfo.Text = $"{x}, {y} · {(_pickerCurrentLayer ? "Current layer" : "Visible layers")}" + (sample.PaletteIndex is { } index ? $" · Index {index}" : string.Empty);
            _sampleInfo.IsVisible = true;
            RefreshStatus();
        }
        catch (Exception ex) { SetError(ex.Message); }
    }

    private void UpdateCanvasCursor()
    {
        var size = Current()?.GetToolOptions().FirstOrDefault(o => o.DisplayName == "Brush Size")?.Value;
        _canvas.SetInteractionAppearance(_eyedropperMode || _altHeld, _spaceHeld, _selectionMode, size is int pixels ? pixels : 1);
    }

    private static bool IsEditingText(object? source)
    {
        if (source is not Control control) return false;
        return control is TextBox or NumericUpDown or ComboBox or Slider ||
            control.GetVisualAncestors().Any(v => v is TextBox or NumericUpDown or ComboBox or Slider);
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _busy) return;
        var editing = IsEditingText(e.Source) || IsEditingText(FocusManager?.GetFocusedElement());
        if (editing && !(e.KeyModifiers == KeyModifiers.Control && e.Key == Key.S)) return;
        if (e.Key is Key.LeftAlt or Key.RightAlt)
        {
            _altHeld = true; UpdateCanvasCursor(); e.Handled = true; return;
        }
        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None)
        {
            _spaceHeld = true; UpdateCanvasCursor(); e.Handled = true; return;
        }
        if (e.Key == Key.Escape)
        {
            foreach (var control in this.GetVisualDescendants().OfType<Control>()) ToolTip.SetIsOpen(control, false);
            if (_parameterEdit is not null) FinishParameterEdit(false);
            else if (_canvas.HasActivePointer || _selectionTransformGesture is not null || _selectionStart is not null)
            {
                _canvas.CancelActivePointer(); CancelSelectionTransformGesture(); CancelCanvasInteraction();
            }
            else if (Current() is { } session && _selection.GetOverlay(session) is not null)
            {
                _selection.Clear(session); RefreshCanvas(false);
            }
            else if (_eyedropperMode)
            {
                _eyedropperMode = false; _optionsSignature = null; RefreshTools(); RefreshToolOptions();
            }
            e.Handled = true; return;
        }
        if (_canvas.HasActivePointer && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Key == Key.P))
            _canvas.CancelActivePointer();
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.W) { e.Handled = true; await CloseCurrentDocumentAsync(); return; }
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.A && Current() is { } selectSession)
        {
            _selection.SelectAll(selectSession); SelectQuickTool("workspace.selection"); e.Handled = true; return;
        }
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.D && Current() is { } clearSession)
        {
            _selection.Clear(clearSession); RefreshCanvas(false); e.Handled = true; return;
        }
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Z)
        {
            FinishParameterEdit(); await InvokeActionAsync(BuiltinActionIds.Redo); e.Handled = true; return;
        }
        if (e.KeyModifiers == KeyModifiers.Shift && e.Key == Key.G)
        {
            _gridVisible = !_gridVisible; _canvas.SetGrid(_gridVisible); SyncGridShortcutButton(); e.Handled = true; return;
        }
        var modifiers = ShortcutModifiers.None;
        if ((e.KeyModifiers & KeyModifiers.Control) != 0) modifiers |= ShortcutModifiers.Control;
        if ((e.KeyModifiers & KeyModifiers.Shift) != 0) modifiers |= ShortcutModifiers.Shift;
        if ((e.KeyModifiers & KeyModifiers.Alt) != 0) modifiers |= ShortcutModifiers.Alt;
        if ((e.KeyModifiers & KeyModifiers.Meta) != 0) modifiers |= ShortcutModifiers.Meta;
        if (_shortcuts.TryResolve(new ShortcutGesture(e.Key.ToString(), modifiers), out var action))
        {
            e.Handled = true; FinishParameterEdit(); await InvokeActionAsync(action); return;
        }
        if (e.KeyModifiers != KeyModifiers.None) return;
        if (EditorToolShortcuts.ForKey(e.Key.ToString()) is { } tool)
        {
            SelectQuickTool(tool); e.Handled = true; return;
        }
        switch (e.Key)
        {
            case Key.F: FitCanvas(); break;
            case Key.D1: SetZoom(1d); break;
            case Key.X:
            case Key.R: SwapColors(); break;
            case Key.P: TogglePlayback(); RefreshPlaybackState(); break;
            default: return;
        }
        e.Handled = true;
    }

    private void OnUxKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftAlt or Key.RightAlt) _altHeld = false;
        if (e.Key == Key.Space) _spaceHeld = false;
        UpdateCanvasCursor();
    }

    private Button BuildGridToggleButton()
    {
        _gridButton = TextIconButton("", "Grid On", "Pixel grid · Shift+G", () =>
        {
            _gridVisible = !_gridVisible;
            _canvas.SetGrid(_gridVisible);
            SyncGridShortcutButton();
        });
        SyncGridShortcutButton();
        return _gridButton;
    }

    private void SyncGridShortcutButton()
    {
        if (_gridButton is null) return;
        _gridButton.Content = _gridVisible ? "Grid On" : "Grid Off";
        SetSelected(_gridButton, _gridVisible);
    }

    private void ChangeZoom(double factor)
    {
        var point = _canvasScroll.TranslatePoint(new Point(_canvasScroll.Viewport.Width / 2, _canvasScroll.Viewport.Height / 2), _canvas) ?? new Point(_canvas.Bounds.Width / 2, _canvas.Bounds.Height / 2);
        ChangeZoomAt(factor, point);
    }

    private void SetZoom(double zoom)
    {
        if (Current() is { } session && session.Zoom > 0) ChangeZoom(zoom / session.Zoom);
    }

    private void ChangeZoomAt(double factor, Point canvasPoint)
    {
        if (_busy || Current() is not { } session || !double.IsFinite(factor) || factor <= 0) return;
        var oldZoom = session.Zoom;
        var anchor = _canvas.TranslatePoint(canvasPoint, _canvasScroll);
        var pixel = canvasPoint / oldZoom;
        session.SetZoom(oldZoom * factor);
        RefreshCanvas(updatePreview: false);
        UpdateLayout();
        _navigationVersion++;
        if (anchor is { } before && _canvas.TranslatePoint(pixel * session.Zoom, _canvasScroll) is { } after)
            _canvasScroll.Offset += after - before;
        RefreshStatus();
    }

    private void FitCanvas()
    {
        if (Current() is not { } session || _busy) return;
        var size = session.CaptureSnapshot().Canvas.Size;
        var width = Math.Max(32, _canvasScroll.Viewport.Width - 50);
        var height = Math.Max(32, _canvasScroll.Viewport.Height - 50);
        var zoom = Math.Min(width / size.Width, height / size.Height);
        if (zoom >= 1d) zoom = Math.Floor(zoom);
        session.SetZoom(Math.Clamp(zoom, 0.125d, 128d));
        RefreshCanvas(updatePreview: false);
        UpdateLayout();
        _navigationVersion++;
        _canvasScroll.Offset = new Vector(Math.Max(0, (_canvasScroll.Extent.Width - _canvasScroll.Viewport.Width) / 2), Math.Max(0, (_canvasScroll.Extent.Height - _canvasScroll.Viewport.Height) / 2));
        RefreshStatus();
    }

    private void FitWindowToScreen()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        Width = Math.Min(Width, screen.WorkingArea.Width / screen.Scaling * 0.96);
        Height = Math.Min(Height, screen.WorkingArea.Height / screen.Scaling * 0.94);
    }
}
