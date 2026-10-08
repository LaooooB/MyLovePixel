"""Compiler- and UI-confirmed corrections. Removed after source materialization."""
from pathlib import Path
root = Path(__file__).resolve().parents[2]
def edit(path, replacements):
    p = root / path
    s = p.read_text(encoding='utf-8')
    for old, new in replacements:
        if old in s:
            assert s.count(old) == 1, 'Ambiguous correction: ' + path
            s = s.replace(old, new)
        else:
            assert new in s, 'Unexpected source: ' + path
    p.write_text(s, encoding='utf-8', newline='\n')

p = root / 'src/MyLovePixel.Application/ColorLibraryStore.cs'
s = p.read_text()
for field in ('Colors', 'Folders', 'TemporaryColors'):
    s = s.replace(f'if (data.{field}.Count >= ', f'if (data.{field}!.Count >= ')
p.write_text(s, encoding='utf-8', newline='\n')
p = root / 'tests/MyLovePixel.UxSmoke/Program.cs'
s = p.read_text().replace('Save(Path.Combine(_output, name + ".png"));', 'Save(Path.Combine(_output, name + ".png"), new PngBitmapEncoderOptions());')
s = s.replace('((TabItem)tabs.Items[1]!).Header?.ToString()', 'HeaderText(((TabItem)tabs.Items[1]!).Header)').replace('t.Header?.ToString()', 'HeaderText(t.Header)')
if 'private static string? HeaderText' not in s:
    s = s.replace('    private static T Field<T>', '    private static string? HeaderText(object? header) => header is TextBlock text ? text.Text : header?.ToString();\n    private static T Field<T>')
if 'New document synchronizes' not in s:
    s = s.replace('            workspace.NewDocument(1024, 1024); Flush();', '''            workspace.NewDocument(1024, 1024); Flush();
            Check(hex.Text == HexColor.Format((Rgba32)Call("ActiveLibraryColor")!), "New document synchronizes the active HEX with its drawing color");''')
if 'Escape exits eyedropper' not in s:
    needle = '            File.WriteAllText(Path.Combine(_output, "checks.json")'
    s = s.replace(needle, '''            var frameBrush = (Avalonia.Media.ISolidColorBrush)Field<Border>("_comfortPreviewFrame").Background!;
            Check(frameBrush.Color.R == new CanvasDisplaySettings(37).Frame.R, "Restart also restores the preview frame brightness");
            Call("SelectEyedropper");
            Field<PixelCanvasView>("_canvas").Focus();
            _window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            _window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Flush();
            Check(!Field<bool>("_eyedropperMode"), "Escape exits eyedropper without switching inspector tabs");
''' + needle)
p.write_text(s, encoding='utf-8', newline='\n')
edit('src/MyLovePixel.Desktop/MainWindow.Convenience.cs', [
    ('var rgbFlyout = new Flyout { Content = rgb, Placement = PlacementMode.Bottom };',
     'var rgbBody = new StackPanel { Spacing = 8, Width = 280 };\n        rgbBody.Children.Add(rgb);\n        rgbBody.Children.Add(BuildTransparentColorButton());\n        var rgbFlyout = new Flyout { Content = rgbBody, Placement = PlacementMode.Bottom };'),
    ('    private bool _convenienceInstalled;', '    private DocumentSession? _studioBoundSession;\n    private bool _convenienceInstalled;'),
    ('        RefreshConvenienceUi();\n        Dispatcher.UIThread.Post', '        RefreshConvenienceUi();\n        ApplyCanvasDisplaySettings();\n        Dispatcher.UIThread.Post'),
    ('        if (_studioHex.IsKeyboardFocusWithin || _studioR.IsKeyboardFocusWithin || _studioG.IsKeyboardFocusWithin || _studioB.IsKeyboardFocusWithin)\n            return;',
     '        var contextChanged = !ReferenceEquals(_studioBoundSession, session);\n        _studioBoundSession = session;\n        if (!contextChanged && (_studioHex.IsKeyboardFocusWithin || _studioR.IsKeyboardFocusWithin || _studioG.IsKeyboardFocusWithin || _studioB.IsKeyboardFocusWithin))\n            return;'),
    ('        if (active != _studioColor) SyncStudioColor(active);', '        if (contextChanged || active != _studioColor) SyncStudioColor(active);'),
])
edit('src/MyLovePixel.Desktop/MainWindow.RefreshCore.cs', [
    ('            RefreshPalette();\n            RefreshEffects();', '            RefreshPalette();\n            RefreshConvenienceUi();\n            RefreshEffects();'),
])
edit('src/MyLovePixel.Desktop/MainWindow.Actions.cs', [
    ('    private async void OnKeyDown(object? sender, KeyEventArgs e)\n    {', '    private async void OnKeyDown(object? sender, KeyEventArgs e)\n    {\n        if (e.Handled) return;'),
    ('        if (e.Key == Key.Escape)\n        {\n            if (Current()', '        if (e.Key == Key.Escape)\n        {\n            if (_eyedropperMode) { _eyedropperMode = false; RefreshTools(); RefreshToolOptions(); }\n            if (Current()'),
])
edit('src/MyLovePixel.Desktop/MainWindow.GestureRackUx.cs', [
    ('            e.Source is TextBox or NumericUpDown or ComboBox or Slider)', '            IsEditingText(e.Source))'),
])
p = root / 'src/MyLovePixel.Desktop/MainWindow.ColorPicker.cs'
s = p.read_text()
needle = '        AutomationProperties.SetAutomationId(button, "studio-color-picker");'
if 'Drag the circle to choose a color' not in s:
    s = s.replace(needle, needle + '\n        ToolTip.SetTip(button, "Drag the circle to choose a color. Done keeps it; Escape cancels.");')
s = s.replace('Text = "Live preview · Done / outside keeps · Esc cancels", FontSize = 11', 'Text = "Live preview · Done / outside keeps · Esc cancels", FontSize = 11, TextWrapping = TextWrapping.Wrap')
s = s.replace('body.Children.Add(LibraryRow(preview, value));', 'body.Children.Add(LibraryRow(new Border { Background = CanvasBackdrop.Create(new CanvasDisplaySettings()), Child = preview }, value));')
p.write_text(s, encoding='utf-8', newline='\n')
edit('src/MyLovePixel.Desktop/MainWindow.UserPalette.cs', [
    ('Height = 20, Background = Brush(ColorLibraryStore.ParseColor(hex)),', 'Height = 20, Background = CanvasBackdrop.Create(new CanvasDisplaySettings()), Child = new Border { Background = Brush(ColorLibraryStore.ParseColor(hex)) },'),
])
edit('src/MyLovePixel.Desktop/MainWindow.ColorWorkspace.cs', [
    ('                body.Children.Add(swatch);', '                body.Children.Add(new Border { Background = CanvasBackdrop.Create(new CanvasDisplaySettings()), Child = swatch });'),
])
