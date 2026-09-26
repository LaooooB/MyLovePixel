from pathlib import Path
import zipfile

root = Path('src/MyLovePixel.Desktop')
(root / 'PixelCanvasView.Automation.cs').write_text('''using Avalonia.Automation;
using Avalonia.Automation.Peers;

namespace MyLovePixel.Desktop;

public sealed partial class PixelCanvasView
{
    protected override AutomationPeer OnCreateAutomationPeer() => new PixelCanvasAutomationPeer(this);

    private sealed class PixelCanvasAutomationPeer(PixelCanvasView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
        protected override string GetClassNameCore() => nameof(PixelCanvasView);
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
    }
}
''', encoding='utf-8')

p = Path('tests/MyLovePixel.Desktop.UxTests/Program.cs')
s = p.read_text(encoding='utf-8')
assert 'Canvas participates in native accessibility' not in s
s = s.replace('using Avalonia.Automation;', 'using Avalonia.Automation;\nusing Avalonia.Automation.Peers;', 1)
needle = '        Run("A sticky error survives coordinate updates", window =>'
assert needle in s
s = s.replace(needle, '''        Run("Canvas participates in native accessibility with a stable identity", window =>
        {
            var canvas = Field<PixelCanvasView>(window, "_canvas");
            var peer = ControlAutomationPeer.CreatePeerForElement(canvas);
            Check(peer is not null, "The custom canvas has no automation peer.");
            Check(peer!.IsControlElement() && peer.IsContentElement(), "Canvas is missing from the accessibility tree.");
            Check(peer.GetAutomationId() == "workspace.canvas" && peer.GetName() == "Pixel canvas", "Canvas accessibility identity is not stable.");
            Check(peer.IsKeyboardFocusable(), "The accessible canvas cannot receive keyboard focus.");
            Check(peer.GetBoundingRectangle().Width > 0, "Canvas automation bounds are empty.");
        });
''' + needle, 1)
p.write_text(s, encoding='utf-8')
p = Path('docs/UX_RELEASE.md')
p.write_text(p.read_text(encoding='utf-8').replace('32 rendered desktop', '33 rendered desktop'), encoding='utf-8')

p = Path('scripts/Test-UxRelease.ps1')
s = p.read_text(encoding='utf-8')
needle = '} finally {\n    if (!$p.HasExited)'
assert needle in s
s = s.replace(needle, '''} catch {
    $results.Add("FAIL " + $_.Exception.Message)
    $results | Set-Content (Join-Path $OutputDirectory 'native-smoke.txt')
    $results | Write-Output
    throw
} finally {
    if (!$p.HasExited)''', 1)
p.write_text(s, encoding='utf-8')

Path('release').mkdir(exist_ok=True)
with zipfile.ZipFile('release/review-source.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for folder in ['src', 'tests', 'docs', '.github', 'scripts']:
        for path in Path(folder).rglob('*'):
            if path.is_file() and not any(p in path.parts for p in ['bin', 'obj', '__pycache__', 'ux-patches']):
                archive.write(path, path.as_posix())
    for name in ['.gitignore', 'Directory.Build.props', 'Directory.Packages.props', 'MyLovePixel.slnx', 'global.json', 'HANDOFF.md', 'README.md', 'THIRD_PARTY_NOTICES.md']:
        archive.write(name)
