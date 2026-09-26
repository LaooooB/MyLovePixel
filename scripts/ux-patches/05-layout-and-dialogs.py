from pathlib import Path
import re, zipfile
D=Path('src/MyLovePixel.Desktop')
def edit(name, old, new, count=1):
 p=D/name; s=p.read_text(encoding='utf-8'); assert old in s, (name,old[:90]); p.write_text(s.replace(old,new,count),encoding='utf-8')
def method(name, signature, body):
 p=D/name; s=p.read_text(encoding='utf-8'); a=s.index('    '+signature); b=s.index('\n    }',a)+6; p.write_text(s[:a]+body+s[b:],encoding='utf-8')
# Use Fluent's supported theme resources, with compact text-first numeric editors.
p=D/'EditorStyles.cs'; s=p.read_text(encoding='utf-8'); anchor='        // Keep Fluent controls'
insert='''        app.Resources["ExpanderMinHeight"] = 34d;
        app.Resources["ExpanderHeaderPadding"] = new Thickness(9, 0, 0, 0);
        app.Resources["ExpanderContentPadding"] = new Thickness(0);
        app.Resources["ExpanderHeaderBackground"] = EditorThemeTokens.Surface;
        app.Resources["ExpanderContentBackground"] = EditorThemeTokens.Surface;
        app.Resources["ExpanderHeaderBorderBrush"] = EditorThemeTokens.PanelBorder;
        app.Resources["ExpanderContentBorderBrush"] = EditorThemeTokens.PanelBorder;
        app.Resources["ExpanderHeaderBackgroundPointerOver"] = EditorThemeTokens.SurfaceHover;
        app.Resources["ExpanderHeaderForeground"] = EditorThemeTokens.TextPrimary;
        app.Styles.Add(new Style(x => x.OfType<NumericUpDown>())
        {
            Setters =
            {
                new Setter(NumericUpDown.ShowButtonSpinnerProperty, false),
                new Setter(NumericUpDown.MinHeightProperty, 32d),
                new Setter(NumericUpDown.PaddingProperty, new Thickness(6, 3)),
                new Setter(NumericUpDown.FontSizeProperty, 13d),
            },
        });
        app.Styles.Add(new Style(x => x.OfType<Expander>())
        {
            Setters =
            {
                new Setter(Expander.HorizontalAlignmentProperty, Avalonia.Layout.HorizontalAlignment.Stretch),
                new Setter(Expander.HorizontalContentAlignmentProperty, Avalonia.Layout.HorizontalAlignment.Stretch),
                new Setter(Expander.MinHeightProperty, 34d),
                new Setter(Expander.PaddingProperty, new Thickness(0)),
                new Setter(Expander.BackgroundProperty, EditorThemeTokens.Surface),
                new Setter(Expander.BorderBrushProperty, EditorThemeTokens.PanelBorder),
            },
        });

'''
assert anchor in s; s=s.replace(anchor,insert+anchor,1); p.write_text(s,encoding='utf-8')
# Default arrow-button templates consumed all the width of RGBA fields.
edit('MainWindow.ColorsUx.cs','input.MinWidth = 48;', 'input.MinWidth = 48;\n            input.ShowButtonSpinner = false;')
edit('MainWindow.ColorsUx.cs','        if (_syncingStudioColor) return;\n        ApplyStudioColor(new Rgba32', '        if (_syncingStudioColor || _studioR.Value is null || _studioG.Value is null || _studioB.Value is null || _studioA.Value is null) return;\n        ApplyStudioColor(new Rgba32')
edit('MainWindow.LayersUx.cs','public readonly NumericUpDown Opacity = new() {', 'public readonly NumericUpDown Opacity = new() { ShowButtonSpinner = false,')
edit('MainWindow.Shell.cs','_frameDuration.Width = 84;', '_frameDuration.Width = 84;\n        _frameDuration.ShowButtonSpinner = false;')
edit('MainWindow.cs','MinWidth = 760;\n        MinHeight = 480;', 'MinWidth = 640;\n        MinHeight = 400;')
edit('MainWindow.Shell.cs','        var root = new DockPanel { Background = EditorThemeTokens.AppBackground };', '        var root = new DockPanel { Background = EditorThemeTokens.AppBackground };\n        _timelineExpander.HorizontalAlignment = HorizontalAlignment.Stretch;\n        _previewExpander.HorizontalAlignment = HorizontalAlignment.Stretch;')
# A scrollable virtual margin lets a fitted image be moved, with no paint events.
edit('MainWindow.Shell.cs','        _canvasScroll.Content = new Border\n        {\n            BorderBrush = EditorThemeTokens.StrongBorder,\n            BorderThickness = new Thickness(1),\n            Padding = new Thickness(24),\n            HorizontalAlignment = HorizontalAlignment.Center,\n            VerticalAlignment = VerticalAlignment.Center,\n            Child = _canvas,\n        };', '''        _canvasScroll.Content = _navigationFrame = new Border
        {
            Padding = new Thickness(400, 300),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = _canvas,
        };
        _canvasScroll.SizeChanged += (_, _) => ResizeNavigationSpace();''')
edit('MainWindow.InteractionUx.cs','        _canvas.PanDeltaRequested = delta => _canvasScroll.Offset -= delta;', '        _canvas.PanDeltaRequested = delta => { _navigationVersion++; _canvasScroll.Offset -= delta; };\n        InstallWorkspacePanning();')
edit('MainWindow.InteractionUx.cs','            _altHeld = _spaceHeld = false;', '            _altHeld = _spaceHeld = false;\n            CancelWorkspacePanning();')
edit('MainWindow.InteractionUx.cs','        button.HorizontalContentAlignment = HorizontalAlignment.Left;', '        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;')
edit('MainWindow.InteractionUx.cs','        var signature = $"{session?.GetHashCode()}:{session?.CurrentLayerId}:', '        var signature = $"{session?.GetHashCode()}:{session?.CurrentLayerId}:{session?.CurrentFrameId}:{session?.HasEditableCel}:')
edit('MainWindow.InteractionUx.cs','        if (anchor is { } before', '        _navigationVersion++;\n        if (anchor is { } before')
edit('MainWindow.InteractionUx.cs','        _canvasScroll.Offset = new Vector(Math.Max(0,', '        _navigationVersion++;\n        _canvasScroll.Offset = new Vector(Math.Max(0,')
# The shortcut column occupies the full tool row and labels have a bounded width.
edit('MainWindow.InteractionUx.cs','button.Content is StackPanel content', 'button.Content is Control content')
method('MainWindow.Infrastructure.cs','private static Button BuildTextIconButton(string glyph, string label, string tip)', '''    private static Button BuildTextIconButton(string glyph, string label, string tip)
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
    }''')
method('MainWindow.Infrastructure.cs','private static Button ToggleTextButton(string glyph, string label, string tip, Func<bool> get, Action<bool> set)', '''    private static Button ToggleTextButton(string glyph, string label, string tip, Func<bool> get, Action<bool> set)
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
    }''')
edit('MainWindow.Infrastructure.cs','private static Expander Expander(string header, Control content) => new() { Header = header, Content = content, IsExpanded = false, HorizontalContentAlignment = HorizontalAlignment.Stretch };', '''private static Expander Expander(string header, Control content)
    {
        var expander = new Expander { Header = header, Content = content, IsExpanded = false, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        expander.Expanded += (_, _) => (TopLevel.GetTopLevel(expander) as MainWindow)?.QueueRefreshAll();
        return expander;
    }''')
# Remove repeated active-tool subtitle; the canvas context and selected tool already name it.
p=D/'MainWindow.RefreshCore.cs'; s=p.read_text(encoding='utf-8'); a=s.index('        var active = session.GetTools()'); b=s.index('        foreach (var option',a); s=s[:a]+s[b:]; p.write_text(s,encoding='utf-8')
# Keep parameter inputs intact and bind numeric runs to one undo gesture.
p=D/'MainWindow.RefreshAdvanced.cs'; s=p.read_text(encoding='utf-8'); s=s.replace('new ColumnDefinitions("*,30")','new ColumnDefinitions("*,Auto")')
s=s.replace('x.ValueChanged += (_, _) => Safe(Set);','x.ValueChanged += (_, _) => UpdateParameter(session, x, parameter.DisplayName, Set);\n                WireParameterCompletion(x);')
s=s.replace('y.ValueChanged += (_, _) => Safe(Set);','y.ValueChanged += (_, _) => UpdateParameter(session, y, parameter.DisplayName, Set);\n                WireParameterCompletion(y);')
s=s.replace('var tileWrap = new WrapPanel { ItemWidth = 38, ItemHeight = 34 };','var tileWrap = new WrapPanel();')
p.write_text(s,encoding='utf-8')
method('MainWindow.RefreshAdvanced.cs','private void RefreshEffects()', '''    private void RefreshEffects()
    {
        _effectsPanel.Children.Clear();
        var session = Current(); if (session is null) return;
        var add = new ComboBox { ItemsSource = _plugins.GetEffectTypes(), SelectedIndex = 0 };
        _effectsPanel.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 5, Children = { add, Place(TextIconButton("＋", "Add", "Add effect to current layer / frame", () => { if (add.SelectedItem is string type) _selectedEffect = _plugins.AddEffect(session, type); }), 1) } });
        var effects = _plugins.GetEffects(session);
        if (_selectedEffect is { } previous && effects.All(v => v.Id != previous)) _selectedEffect = null;
        foreach (var effect in effects)
        {
            var item = new StackPanel { Spacing = 4 };
            var choose = new Button { Content = effect.DisplayName, HorizontalContentAlignment = HorizontalAlignment.Left, HorizontalAlignment = HorizontalAlignment.Stretch };
            SetSelected(choose, _selectedEffect == effect.Id);
            choose.Click += (_, _) => { FinishParameterEdit(); _selectedEffect = effect.Id; RefreshEffects(); };
            item.Children.Add(choose);
            item.Children.Add(Icons(
                ToggleTextButton("", "Enabled", "Enable this effect", () => effect.Enabled, value => { session.SetEffectEnabled(effect.Id, value); RefreshEffects(); }),
                TextIconButton("↑", "Up", "Move effect up", () => session.MoveEffect(effect.Id, -1)),
                TextIconButton("↓", "Down", "Move effect down", () => session.MoveEffect(effect.Id, 1)),
                TextIconButton("×", "Delete", "Delete effect", () => session.RemoveEffect(effect.Id))));
            _effectsPanel.Children.Add(item);
        }
        if (_selectedEffect is { } id)
        {
            foreach (var parameter in _plugins.GetEffectParameters(session, id)) _effectsPanel.Children.Add(BuildEffectParameter(session, id, parameter));
            _effectsPanel.Children.Add(TextIconButton("", "Bake into Current Frame", "Bake the effect stack into current layer / frame · Undo available", () => _plugins.BakeEffects(session)));
        }
        _effectsPanel.IsEnabled = !session.GetLayers().Any(layer => layer.IsCurrent && layer.Locked);
    }''')
# Improve clipping-sensitive tool sliders without shrinking their text.
edit('GestureRackParameterSlider.cs','MinWidth = 180;', 'MinWidth = 100;')
edit('GestureRackParameterSlider.cs','MinWidth = 120,', 'MinWidth = 58,')
# Share accessible dialog actions, with a pinned footer and scrollable fields.
(D/'DialogChrome.cs').write_text(r'''using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace MyLovePixel.Desktop;

internal static class DialogChrome
{
    public static Button TextButton(string label, Action action, bool primary = false)
    {
        var button = new Button { Content = label, MinWidth = 76, IsDefault = primary, IsCancel = label == "Cancel" };
        AutomationProperties.SetName(button, label);
        if (primary) button.Classes.Add("primary");
        button.Click += (_, _) => action(); return button;
    }
    public static Button IconButton(string glyph, string tip, Action action)
    {
        var button = TextButton(tip, action); button.MinWidth = 48;
        button.Content = new TextBlock { Text = tip, TextWrapping = TextWrapping.Wrap };
        return button;
    }
    public static Control ConfirmCancel(Action cancel, Action accept, string acceptLabel = "Apply")
    {
        var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var cancelButton = TextButton("Cancel", cancel); cancelButton.Margin = new Thickness(0, 0, 8, 0);
        row.Children.Add(cancelButton); row.Children.Add(TextButton(acceptLabel, accept, true)); return row;
    }
    public static Control Labeled(string label, Control control, double labelWidth = 96)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions($"{labelWidth},*"), ColumnSpacing = 8 };
        var text = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Foreground = EditorThemeTokens.TextSecondary };
        AutomationProperties.SetName(control, label); AutomationProperties.SetLabeledBy(control, text);
        grid.Children.Add(text); Grid.SetColumn(control, 1); grid.Children.Add(control); return grid;
    }
    public static TextBlock Help(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = EditorThemeTokens.TextSecondary };
    public static void SetContent(Window window, Panel fields)
    {
        var layout = new DockPanel();
        if (fields.Children.LastOrDefault() is Panel footer && footer.Children.OfType<Button>().Any(b => b.IsCancel))
        {
            fields.Children.Remove(footer); footer.Margin = new Thickness(16, 10, 16, 14);
            DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
        }
        fields.Margin = new Thickness(16, 12);
        layout.Children.Add(new ScrollViewer { Content = fields, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        window.Content = layout; window.SizeToContent = SizeToContent.Height; window.MaxHeight = 550; window.MinHeight = 180;
        window.Opened += (_, _) =>
        {
            var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
            if (screen is not null) window.MaxHeight = Math.Max(180, Math.Min(550, screen.WorkingArea.Height / screen.Scaling - 48));
        };
    }
}
''',encoding='utf-8')
for name in ['EditorDialogs.cs','ColorProcessingDialogs.cs','FrameMetadataDialogs.cs','AutoTileDialog.cs']:
 p=D/name; s=p.read_text(encoding='utf-8'); s=s.replace('Content = root;', 'DialogChrome.SetContent(this, root);'); s=s.replace('new ColumnDefinitions("*,28")','new ColumnDefinitions("*,Auto")'); p.write_text(s,encoding='utf-8')
p=D/'EditorDialogs.cs'; s=p.read_text(encoding='utf-8'); s=s.replace('Title = "Export Game Assets";', 'Title = "Export";').replace('Width = 470;\n        Height = 610;', 'Width = 430;\n        Height = 520;')
s=s.replace('        root.Children.Add(new TextBlock { Text = "Game-ready export", FontSize = 15, FontWeight = FontWeight.SemiBold });\n','')
s=re.sub(r'        root.Children.Add\(DialogChrome.Help\(\n            "(?:PNG output|Every export)[^\n]+\n', '', s)
s=s.replace('Content = "Trim transparent edges (metadata-aware pipelines only)"','Content = "Trim transparent edges"').replace('Content = "Power-of-two atlas (streaming / mipmap compatibility)"','Content = "Power-of-two atlas"')
s=s.replace('Safest drag-and-drop format. Each frame is an independent transparent PNG; sprite.json keeps animation/gameplay metadata.','One transparent PNG per frame.')
s=s.replace('Engine-safe grid by default: Trim is off so frame alignment stays stable for native Unity/Godot sprite slicing. Keep Trim off unless your importer consumes sprite.json sourceRect/sourceSize.','Keep Trim off for regular grid slicing.')
s=s.replace('Packed runtime atlas. Trim is useful here because sprite.json carries exact source rects, pivots, hitboxes, sockets and events. Use Padding/Extrude when filtered atlas sampling needs edge guards.','Packed atlas. Use the exported JSON to preserve trimmed frame positions.')
s=s.replace('        root.Children.Add(DialogChrome.Help("Choose a preset or enter a custom pixel size."));\n','')
p.write_text(s,encoding='utf-8')
(D/'MainWindow.NavigationUx.cs').write_text(r'''using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private Border? _navigationFrame;
    private long _navigationVersion;
    private IPointer? _workspacePanPointer;
    private Point _workspacePanPoint;

    private void ResizeNavigationSpace()
    {
        if (_navigationFrame is null || _canvasScroll.Bounds.Width < 1 || _canvasScroll.Bounds.Height < 1) return;
        var old = _navigationFrame.Padding;
        var next = new Thickness(Math.Max(80, _canvasScroll.Bounds.Width), Math.Max(80, _canvasScroll.Bounds.Height));
        if (old == next) return;
        var offset = _canvasScroll.Offset + new Vector(next.Left - old.Left, next.Top - old.Top);
        _navigationFrame.Padding = next;
        var version = ++_navigationVersion;
        Dispatcher.UIThread.Post(() => { if (!_closed && version == _navigationVersion) _canvasScroll.Offset = offset; }, DispatcherPriority.Background);
    }
    private void InstallWorkspacePanning()
    {
        _canvasScroll.PointerPressed += (_, e) =>
        {
            var p = e.GetCurrentPoint(_canvasScroll).Properties;
            if (_busy || !(p.IsMiddleButtonPressed || p.IsLeftButtonPressed && _spaceHeld)) return;
            _workspacePanPointer = e.Pointer; _workspacePanPoint = e.GetPosition(this); e.Pointer.Capture(_canvasScroll);
            _canvasScroll.Cursor = new Cursor(StandardCursorType.Hand); e.Handled = true;
        };
        _canvasScroll.PointerMoved += (_, e) =>
        {
            if (!ReferenceEquals(e.Pointer, _workspacePanPointer)) return;
            var point = e.GetPosition(this); _navigationVersion++;
            _canvasScroll.Offset -= point - _workspacePanPoint; _workspacePanPoint = point; e.Handled = true;
        };
        _canvasScroll.PointerReleased += (_, e) => { if (ReferenceEquals(e.Pointer, _workspacePanPointer)) { CancelWorkspacePanning(); e.Handled = true; } };
        _canvasScroll.PointerCaptureLost += (_, _) => CancelWorkspacePanning();
    }
    private void CancelWorkspacePanning()
    {
        var pointer = _workspacePanPointer; _workspacePanPointer = null;
        if (pointer is not null && ReferenceEquals(pointer.Captured, _canvasScroll)) pointer.Capture(null);
        _canvasScroll.Cursor = null;
    }
}
''',encoding='utf-8')
# Keep screenshot evidence synchronized with the exact source being built.
Path('release').mkdir(exist_ok=True)
with zipfile.ZipFile('release/review-source.zip','w',zipfile.ZIP_DEFLATED) as z:
 for directory in ['src','tests','docs','.github','scripts']:
  for p in Path(directory).rglob('*'):
   if p.is_file() and not any(v in p.parts for v in ['bin','obj','__pycache__']): z.write(p,p.as_posix())
 for name in ['Directory.Build.props','Directory.Packages.props','MyLovePixel.slnx','global.json','HANDOFF.md','README.md','THIRD_PARTY_NOTICES.md']:
  if Path(name).is_file(): z.write(name)
