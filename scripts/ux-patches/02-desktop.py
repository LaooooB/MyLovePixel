from pathlib import Path
import re

ROOT = Path('src/MyLovePixel.Desktop')
def load(name): return (ROOT / name).read_text(encoding='utf-8-sig')
def save(name, text): (ROOT / name).write_text(text, encoding='utf-8')
def replace(name, old, new):
    text = load(name)
    if old not in text: raise RuntimeError(f'Missing source in {name}: {old[:90]}')
    save(name, text.replace(old, new, 1))
def remove(name, signature):
    text = load(name)
    start = text.find('    ' + signature)
    if start < 0: raise RuntimeError(f'Missing method in {name}: {signature}')
    lineend = text.find('\n', start)
    first = text[start:lineend]
    if '=>' in first or ('{' in first and '}' in first): end = lineend + 1
    else:
        end = text.find('\n    }', lineend)
        if end < 0: raise RuntimeError(f'No method end: {signature}')
        end += len('\n    }')
        if text[end:end+1] == '\n': end += 1
    save(name, text[:start] + text[end:])

text = load('MainWindow.cs')
start = text.index('    private Control BuildShell()')
text = text[:start] + '}\n'
text = text.replace('TimelinePageSize = 24', 'TimelinePageSize = 12')
text = text.replace('Width = 1480;', 'Width = 1280;').replace('Height = 920;', 'Height = 820;')
text = text.replace('MinWidth = 1080;', 'MinWidth = 760;').replace('MinHeight = 700;', 'MinHeight = 480;')
text = text.replace('Content = BuildShell();', 'Content = BuildShell();\n        InstallUxInput();')
text = text.replace('            _autosaveTimer.Stop();', '            _closed = true;\n            FinishParameterEdit();\n            _autosaveTimer.Stop();')
save('MainWindow.cs', text)

for sig in ['private void RefreshAll()', 'private void RefreshTools()', 'private void RefreshLayers()', 'private void RefreshPalette()']:
    remove('MainWindow.RefreshCore.cs', sig)
replace('MainWindow.RefreshCore.cs', '    private void RefreshToolOptions()\n    {', '    private void RefreshToolOptions()\n    {\n        if (ReuseToolOptions()) return;')
text = load('MainWindow.RefreshCore.cs')
text = re.sub(r'Text = "Free Transform:[^"\n]*"', 'Text = "Shift: constrain · Esc: cancel"', text)
save('MainWindow.RefreshCore.cs', text)

for sig in ['private void RefreshTimeline()', 'private void RefreshStatus()']:
    remove('MainWindow.Runtime.cs', sig)
replace('MainWindow.Runtime.cs', '            if (e.Kind == EditorPointerKind.Pressed)\n                session.EnsureEditableCel();', '''            if (_busy) return;
            if (e.Kind == EditorPointerKind.Pressed)
            {
                FinishParameterEdit();
                _playback.Stop(session);
                if (!_selectionMode)
                {
                    if (session.DrawingBlockedReason is { } blocked) throw new InvalidOperationException(blocked);
                    session.EnsureEditableCel();
                }
            }''')
replace('MainWindow.Runtime.cs', '        Safe(() => session.EraseCanvasPixel(x, y));', '        if (_busy) return;\n        FinishParameterEdit();\n        _playback.Stop(session);\n        Safe(() => session.EraseCanvasPixel(x, y));')

for sig in ['private async Task NewProjectAsync()', 'private async Task ExportAsync()', 'private async Task ImportAssetAsync()',
            'private void OnWorkspaceChanged(', 'private void ObserveCurrentSession()', 'private void OnSessionChanged(',
            'private void QueueCanvasRefresh()', 'private void QueueRefreshAll()', 'private async Task InvokeActionAsync(',
            'private async void OnKeyDown(', 'private void OnAutosaveTick(', 'private void ChangeZoom(', 'private void SetZoom(']:
    remove('MainWindow.Actions.cs', sig)
replace('MainWindow.Actions.cs', '    private void OnPlaybackTick(object? sender, EventArgs e)\n    {', '    private void OnPlaybackTick(object? sender, EventArgs e)\n    {\n        if (_busy || _closed) return;')

text = load('MainWindow.Convenience.cs')
text = text.split('\ninternal sealed class PixelPreviewView')[0].rstrip() + '\n'
save('MainWindow.Convenience.cs', text)
for sig in ['protected override void OnOpened(', 'protected override void OnClosed(', 'private Control BuildInspectorPreviewBox()',
            'private Button BuildGridToggleButton()', 'private Control BuildStudioPaletteEditor()', 'private void RefreshConvenienceUi()',
            'private void SetStudioColorTarget(', 'private void ApplyStudioColor(', 'private void ApplyStudioRgb()', 'private void ApplyStudioHex()',
            'private void SyncStudioColor(', 'private void OnConvenienceCanvasWheel(', 'private void OnConvenienceKeyDown(',
            'private static bool IsEditingText(', 'private void SelectQuickTool(', 'private void FitCanvas()']:
    remove('MainWindow.Convenience.cs', sig)
text = load('MainWindow.Convenience.cs')
pos = text.rfind('}')
text = text[:pos] + '''    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_convenienceInstalled) return;
        _convenienceInstalled = true;
        FitWindowToScreen();
        RefreshAll();
        Dispatcher.UIThread.Post(FitCanvas, DispatcherPriority.Background);
    }
''' + text[pos:]
save('MainWindow.Convenience.cs', text)

replace('PixelCanvasView.cs', 'public sealed class PixelCanvasView : Control', 'public sealed partial class PixelCanvasView : Control')
for sig in ['public PixelCanvasView()', 'public override void Render(', 'protected override void OnPointerPressed(',
            'protected override void OnPointerMoved(', 'protected override void OnPointerReleased(', 'protected override void OnPointerWheelChanged(']:
    remove('PixelCanvasView.cs', sig)
text = load('PixelCanvasView.cs')
# Raw document coordinates allow raster clipping, rather than dragging along an artificial clamped edge.
text = re.sub(r'^        x = Math.Clamp\(x, 0, _presentation.Size.Width - 1\);\n', '', text, flags=re.M)
text = re.sub(r'^        y = Math.Clamp\(y, 0, _presentation.Size.Height - 1\);\n', '', text, flags=re.M)
save('PixelCanvasView.cs', text)
replace('MainWindow.SelectionTransform.cs', '            case SelectionTransformPhase.Pressed:\n            {', '            case SelectionTransformPhase.Pressed:\n            {\n                FinishParameterEdit();\n                _playback.Stop(session);\n                if (session.DrawingBlockedReason is { } blocked) { SetError(blocked); return; }')

replace('MainWindow.InteractionUx.cs', 'session?.SessionId', 'session?.GetHashCode()')
replace('MainWindow.StateUx.cs', '.FrameOrder.IndexOf(session.CurrentFrameId)', '.FrameOrder.ToList().IndexOf(session.CurrentFrameId)')
replace('MainWindow.ColorsUx.cs', '        return new Border { Padding = new Thickness(10, 8), Child = body,', '        SyncStudioColor(_studioColor);\n        return new Border { Padding = new Thickness(10, 8), Child = body,')

text = load('EditorThemeTokens.cs').replace('ToolRailWidth = 64d', 'ToolRailWidth = 156d').replace('RightPanelWidth = 380d', 'RightPanelWidth = 348d')
text = text.replace('Rgb(108, 120, 112)', 'Rgb(146, 159, 149)')
text = text.replace('Rgb(222, 226, 221)', 'Rgb(42, 48, 44)').replace('Rgb(255, 255, 255)', 'Rgb(30, 35, 31)')
save('EditorThemeTokens.cs', text)
text = load('EditorStyles.cs').replace('FontSizeProperty, 12d', 'FontSizeProperty, 13d').replace('FontSizeProperty, 11d', 'FontSizeProperty, 12d').replace('FontSizeProperty, 10d', 'FontSizeProperty, 12d')
save('EditorStyles.cs', text)

replace('DialogChrome.cs', 'var button = new Button { Content = label, MinWidth = 76 };', 'var button = new Button { Content = label, MinWidth = 76, IsDefault = primary, IsCancel = label == "Cancel" };\n        Avalonia.Automation.AutomationProperties.SetName(button, label);')
text = load('DialogChrome.cs')
start = text.index('        var button = new Button();', text.index('public static Button IconButton'))
end = text.index('        button.Click +=', start)
text = text[:start] + '        var button = new Button { Content = tip, MinWidth = 54 };\n        Avalonia.Automation.AutomationProperties.SetName(button, tip);\n' + text[end:]
save('DialogChrome.cs', text)

# Preserve all advanced commands while allowing their now-visible names to fit.
text = load('MainWindow.RefreshAdvanced.cs')
text = text.replace('new ColumnDefinitions("30,*,30,30,30")', 'new ColumnDefinitions("Auto,*,Auto,Auto,Auto")')
text = text.replace('new ColumnDefinitions("*,30")', 'new ColumnDefinitions("*,Auto")')
text = text.replace('new WrapPanel { ItemWidth = 38, ItemHeight = 34 }', 'new WrapPanel()')
save('MainWindow.RefreshAdvanced.cs', text)

# The produced source is included with verification artifacts for direct review.
Path('release').mkdir(exist_ok=True)
import zipfile
with zipfile.ZipFile('release/review-source.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for directory in ['src', 'tests', 'docs']:
        for path in Path(directory).rglob('*'):
            if path.is_file() and not any(part in ['bin', 'obj'] for part in path.parts): archive.write(path)
    for name in ['Directory.Build.props', 'Directory.Packages.props', 'MyLovePixel.slnx', 'global.json', 'HANDOFF.md']:
        archive.write(name)
