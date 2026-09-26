using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Tiles;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private Button ActionIcon(ActionId id, string glyph, string tip, bool primary = false) =>
        ActionTextButton(id, glyph, ActionLabel(tip), tip, primary);

    private Button ActionTextButton(ActionId id, string glyph, string label, string tip, bool primary = false)
    {
        var button = TextIconButton(glyph, label, tip, () => InvokeActionAsync(id));
        if (primary) button.Classes.Add("primary");
        Named(button, id.Value, label);
        RegisterActionControl(id, button);
        return button;
    }

    private void RegisterActionControl(ActionId id, Control control)
    {
        if (!_actionControls.TryGetValue(id, out var values)) _actionControls.Add(id, values = []);
        values.Add(control);
    }

    private Button SelectionModeButton(string glyph, string tip, SelectionGestureMode mode)
    {
        var button = IconButton(glyph, tip, () =>
        {
            _selectionGesture = mode;
            _selectionStart = null;
            _selectionVertices.Clear();
            _optionsSignature = null;
            RefreshToolOptions();
        });
        SetSelected(button, _selectionGesture == mode);
        return button;
    }

    private Button ToggleFlag(string glyph, TileCellFlags flag) =>
        ToggleIcon(glyph, flag.ToString(), () => (_tileFlags & flag) != 0, value =>
        {
            if (value) _tileFlags |= flag; else _tileFlags &= ~flag;
            RefreshTiles();
        });

    private Button SwatchButton(Border swatch, string tip, bool primary)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        row.Children.Add(swatch);
        row.Children.Add(new TextBlock { Text = primary ? "Foreground" : "Background", VerticalAlignment = VerticalAlignment.Center });
        var button = new Button { Content = row, HorizontalContentAlignment = HorizontalAlignment.Left };
        Named(button, primary ? "color.foreground" : "color.background", primary ? "Foreground color" : "Background color");
        button.Click += (_, _) => SetStudioColorTarget(!primary);
        return button;
    }

    private static Button IconButton(string glyph, string tip, Action action) => TextIconButton(glyph, ActionLabel(tip), tip, action);
    private static Button IconButton(string glyph, string tip, Func<Task> action) => TextIconButton(glyph, ActionLabel(tip), tip, action);
    private static Button SmallIcon(string glyph, string tip, Action action)
    {
        var button = TextIconButton(glyph, ActionLabel(tip), tip, action);
        button.Padding = new Thickness(5, 4);
        return button;
    }

    private static Button TextIconButton(string glyph, string label, string tip, Action action)
    {
        var button = BuildTextIconButton(glyph, label, tip);
        button.Click += (_, _) =>
        {
            var owner = TopLevel.GetTopLevel(button) as MainWindow;
            if (owner?._busy == true) return;
            owner?.FinishParameterEdit();
            try { action(); }
            catch (Exception ex) { if (owner is not null) owner.SetError(ex.Message); else throw; }
        };
        return button;
    }

    private static Button TextIconButton(string glyph, string label, string tip, Func<Task> action)
    {
        var button = BuildTextIconButton(glyph, label, tip);
        button.Click += async (_, _) =>
        {
            var owner = TopLevel.GetTopLevel(button) as MainWindow;
            if (owner?._busy == true) return;
            owner?.FinishParameterEdit();
            try { await action(); }
            catch (Exception ex) { if (owner is not null) owner.SetError(ex.Message); else throw; }
        };
        return button;
    }

    private static Button BuildCompactButton(string glyph, string tip) => BuildTextIconButton(glyph, ActionLabel(tip), tip);

    private static Button BuildTextIconButton(string glyph, string label, string tip)
    {
        Control? icon = null;
        if (label == "Eyedropper")
            icon = new ShapePath { Data = Geometry.Parse("M10 2L14 6M9 3L13 7M11 5L5 11L2 12L3 9L9 3M11 5L14 2"), Width = 16, Height = 16, Stretch = Stretch.Uniform, Stroke = EditorThemeTokens.TextPrimary, StrokeThickness = 1.5 };
        else if (UiIconSemantics.TryCreate(tip, glyph, 16, out var semantic)) icon = semantic;
        else if (UiIcons.TryResolve(tip, glyph, out var kind)) icon = UiIcons.Create(kind, 16);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions(icon is null ? "*" : "Auto,*"), ColumnSpacing = icon is null ? 0 : 6 };
        if (icon is not null) { icon.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(icon); }
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(text, icon is null ? 0 : 1); row.Children.Add(text);
        var button = new Button { Content = row };
        button.Classes.Add("text-icon");
        AutomationProperties.SetName(button, label);
        if (label != tip)
        {
            AutomationProperties.SetHelpText(button, tip);
            ToolTip.SetTip(button, tip); ToolTip.SetPlacement(button, PlacementMode.Bottom); ToolTip.SetShowDelay(button, 650);
        }
        return button;
    }

    private static Button ToggleIcon(string glyph, string tip, Func<bool> get, Action<bool> set) =>
        ToggleTextButton(glyph, ActionLabel(tip), tip, get, set);

    private static Button ToggleTextButton(string glyph, string label, string tip, Func<bool> get, Action<bool> set)
    {
        var button = new ToggleButton { IsChecked = get(), Padding = new Thickness(8, 5), MinHeight = 32, CornerRadius = EditorThemeTokens.ControlRadius, BorderThickness = new Thickness(1) };
        void Sync()
        {
            var enabled = get(); button.IsChecked = enabled;
            button.Content = label + (enabled ? ": On" : ": Off");
            button.Background = enabled ? EditorThemeTokens.SurfaceSelected : EditorThemeTokens.SurfaceRaised;
            button.BorderBrush = enabled ? EditorThemeTokens.Accent : EditorThemeTokens.PanelBorder;
            button.Foreground = enabled ? EditorThemeTokens.Accent : EditorThemeTokens.TextPrimary;
            AutomationProperties.SetName(button, label + (enabled ? ", on" : ", off"));
        }
        ToolTip.SetTip(button, tip); ToolTip.SetPlacement(button, PlacementMode.Bottom);
        button.Click += (_, _) => { (TopLevel.GetTopLevel(button) as MainWindow)?.FinishParameterEdit(); set(button.IsChecked == true); Sync(); };
        Sync(); return button;
    }

    private static WrapPanel Icons(params Control[] controls)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var control in controls)
        {
            control.Margin = new Thickness(0, 0, 5, 5);
            row.Children.Add(control);
        }
        return row;
    }

    private static Control Labeled(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("100,*"), ColumnSpacing = 8 };
        var text = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        text.Classes.Add("muted");
        AutomationProperties.SetName(control, label);
        AutomationProperties.SetLabeledBy(control, text);
        grid.Children.Add(text);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }

    private static Control ListRow(string text, Control action)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 };
        grid.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        grid.Children.Add(Place(action, 1));
        return grid;
    }

    private static Border SectionCard(string title, string? description, Control content)
    {
        var body = new StackPanel { Spacing = 8 };
        if (!string.IsNullOrWhiteSpace(title)) body.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold });
        body.Children.Add(content);
        return new Border { Child = body, Padding = new Thickness(0, 4), Background = Brushes.Transparent };
    }

    private static TabItem TextTab(string title, Control content) => new() { Header = title, Content = content };
    private static Expander Expander(string header, Control content)
    {
        var expander = new Expander { Header = header, Content = content, IsExpanded = false, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        expander.Expanded += (_, _) => (TopLevel.GetTopLevel(expander) as MainWindow)?.QueueRefreshAll();
        return expander;
    }
    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new() { Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0", MinWidth = 54 };
    private static Border Swatch() => new() { Width = 26, Height = 26, BorderBrush = EditorThemeTokens.StrongBorder, BorderThickness = new Thickness(1), Child = new ColorSwatchView() };
    private static IBrush Brush(Rgba32 c) => new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
    private static Separator SeparatorH() => new() { Margin = new Thickness(0, 6) };
    private static Border SeparatorV() => new() { Width = 1, Margin = new Thickness(5, 4), Background = EditorThemeTokens.PanelBorder };
    private static TextBlock ErrorText(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = EditorThemeTokens.Danger };
    private static T Place<T>(T control, int column) where T : Control { Grid.SetColumn(control, column); return control; }
    private static T Named<T>(T control, string id, string name) where T : Control { AutomationProperties.SetAutomationId(control, id); AutomationProperties.SetName(control, name); return control; }
    private static void SetSelected(Control control, bool selected) { if (selected) { if (!control.Classes.Contains("selected")) control.Classes.Add("selected"); } else control.Classes.Remove("selected"); }
    private static string ToolGlyph(string id) => id switch { "core.pencil" => "✎", "core.eraser" => "⌫", "core.line" => "╱", "core.shape" => "□", "core.fill" => "▣", _ => "◆" };
    private static string ShortOption(string value) => value;

    private static string ActionLabel(string tip)
    {
        var label = tip.Split('·')[0].Trim().TrimEnd('.');
        if (label.StartsWith("Unload ", StringComparison.Ordinal)) return "Unload";
        if (label.StartsWith("Dismiss recovery", StringComparison.Ordinal)) return "Dismiss";
        return label switch
        {
            "Move layer up" or "Move effect up" => "Up",
            "Move layer down" or "Move effect down" => "Down",
            "Move frame left" => "Left",
            "Move frame right" => "Right",
            "Delete layer" or "Delete frame" or "Remove effect" => "Delete",
            "Duplicate frame" => "Duplicate",
            "Clear canvas" => "Clear Canvas",
            "FlipX" => "Flip X", "FlipY" => "Flip Y", "Rotate90" => "Rotate 90°",
            _ => label,
        };
    }

    private void Safe(Action action) { try { action(); } catch (Exception ex) { SetError(ex.Message); } }
    private void Safe(Func<object?> action) { try { _ = action(); } catch (Exception ex) { SetError(ex.Message); } }
    private void SetError(string text) => ShowNotice(text, true);
    private static string GetRecoveryRootDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = AppContext.BaseDirectory;
        return Path.Combine(root, "MyLovePixel", "Recovery");
    }
}
