from pathlib import Path
p = Path('tests/MyLovePixel.Desktop.UxTests/Program.cs')
s = p.read_text(encoding='utf-8-sig')
for key, physical in [('G', 'G'), ('D1', 'Digit1'), ('Enter', 'Enter')]:
    for action in ['KeyPress', 'KeyRelease']:
        old = f'window.{action}(Key.{key}, RawInputModifiers.None);'
        new = f'window.{action}(Key.{key}, RawInputModifiers.None, PhysicalKey.{physical}, null);'
        assert old in s, old
        s = s.replace(old, new)
s = s.replace('image!.Save("release/ui/compact-960.png");', 'image!.Save("release/ui/compact-960.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);')
p.write_text(s, encoding='utf-8')
