"""Compiler-confirmed integration corrections. Removed after source materialization."""
from pathlib import Path
root = Path(__file__).resolve().parents[2]
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
if 'Escape exits eyedropper' not in s:
    needle = '            File.WriteAllText(Path.Combine(_output, "checks.json")'
    s = s.replace(needle, '''            Call("SelectEyedropper");
            Field<PixelCanvasView>("_canvas").Focus();
            _window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            _window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Flush();
            Check(!Field<bool>("_eyedropperMode"), "Escape exits eyedropper without switching inspector tabs");
''' + needle)
p.write_text(s, encoding='utf-8', newline='\n')
p = root / 'src/MyLovePixel.Desktop/MainWindow.Convenience.cs'
s = p.read_text().replace('var rgbFlyout = new Flyout { Content = rgb, Placement = PlacementMode.Bottom };',
    'var rgbBody = new StackPanel { Spacing = 8, Width = 280 };\n        rgbBody.Children.Add(rgb);\n        rgbBody.Children.Add(BuildTransparentColorButton());\n        var rgbFlyout = new Flyout { Content = rgbBody, Placement = PlacementMode.Bottom };')
p.write_text(s, encoding='utf-8', newline='\n')
p = root / 'src/MyLovePixel.Desktop/MainWindow.ColorPicker.cs'
s = p.read_text()
needle = '        AutomationProperties.SetAutomationId(button, "studio-color-picker");'
if 'Drag the circle to choose a color' not in s:
    s = s.replace(needle, needle + '\n        ToolTip.SetTip(button, "Drag the circle to choose a color. Done keeps it; Escape cancels.");')
p.write_text(s, encoding='utf-8', newline='\n')
