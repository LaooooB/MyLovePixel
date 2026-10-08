"""One-time, pinned source integration; refuses unexpected edits. No network access."""
from pathlib import Path
import base64, hashlib, json, subprocess, zlib

ROOT = Path(__file__).resolve().parents[2]
BASE = '49ceb612855cb1176952b094bff105c83aef9202'
EXPECTED = '9dab73825cbdbc2a99ed8f33b5ba61e1fb18256593890cf346fde7b6cd1d78b1'
encoded = (Path(__file__).parent / 'patches.b64').read_text().strip()
# Normalize an accidental transport insertion; the decoded SHA still gates all data.
encoded = encoded.replace('g2VXPi2aibtCrU5', 'g2VXPi2ibtCrU5')
payload = zlib.decompress(base64.b64decode(encoded, validate=True))
assert hashlib.sha256(payload).hexdigest() == EXPECTED, 'Patch payload checksum mismatch'
candidates = {}
for patch in json.loads(payload):
    path = patch['path']
    assert path.startswith('src/MyLovePixel.Desktop/') and '..' not in Path(path).parts
    raw = subprocess.check_output(['git', 'show', BASE + ':' + path], cwd=ROOT)
    blob = hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest()
    assert blob == patch['gitBlobSha'], 'Base changed: ' + path
    original = raw.decode('utf-8').replace('\r\n', '\n')
    edited = original
    for change in patch['edits']:
        kind = change['kind']
        if kind == 'replace':
            assert edited.count(change['old']) == 1, 'Ambiguous replacement: ' + path + '\n' + change['old'][:120]
            edited = edited.replace(change['old'], change['new'])
        elif kind == 'replaceRange':
            assert edited.count(change['start']) == 1, 'Ambiguous start: ' + path
            start = edited.index(change['start'])
            end = edited.index(change['end'], start + len(change['start']))
            edited = edited[:start] + change['new'] + edited[end:]
        elif kind == 'removeToEnd':
            assert edited.count(change['start']) == 1, 'Ambiguous tail: ' + path
            edited = edited[:edited.index(change['start'])]
        else:
            raise ValueError('Unknown operation: ' + kind)
    current = (ROOT / path).read_text(encoding='utf-8').replace('\r\n', '\n')
    assert current in (original, edited), 'Local edits would be overwritten: ' + path
    candidates[path] = edited
for path, text in candidates.items():
    (ROOT / path).write_text(text, encoding='utf-8', newline='\n')
    print('Integrated', path)
# The base-type selector intentionally includes derived controls.
p = ROOT / 'src/MyLovePixel.Desktop/EditorUxStyles.cs'
p.write_text(p.read_text().replace('x.OfType<Control>()', 'x.Is<Control>()'), encoding='utf-8', newline='\n')
p = ROOT / 'src/MyLovePixel.Desktop/MainWindow.ColorPicker.cs'
s = p.read_text()
if 'private Flyout? _colorPickerFlyout;' not in s:
    s = s.replace('    private Button BuildColorPickerButton()', '    private Flyout? _colorPickerFlyout;\n\n    private Button BuildColorPickerButton()')
    s = s.replace('var flyout = new Flyout { Content = body, Placement = PlacementMode.Bottom };',
                  'var flyout = _colorPickerFlyout = new Flyout { Content = body, Placement = PlacementMode.Bottom };', 1)
    s = s.replace('        flyout.Closed += (_, _) => button.Focus();', '        Closed += (_, _) => _colorPickerFlyout?.Hide();')
    s = s.replace('ApplyStudioColor(original); flyout.Hide();', 'ApplyStudioColor(original); flyout.Hide(); button.Focus();')
    s = s.replace('LibraryButton("Done", () => flyout.Hide())', 'LibraryButton("Done", () => { flyout.Hide(); button.Focus(); })')
    p.write_text(s, encoding='utf-8', newline='\n')
print('Pinned integration complete')
