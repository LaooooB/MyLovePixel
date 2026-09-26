from pathlib import Path

ROOT = Path('.')

def patch(path, old, new):
    p = ROOT / path
    text = p.read_text(encoding='utf-8')
    if old not in text:
        raise RuntimeError(f'missing patch target in {path}: {old[:80]!r}')
    p.write_text(text.replace(old, new, 1), encoding='utf-8')

patch('src/MyLovePixel.Desktop/PixelCanvasView.Interaction.cs',
'''        if (point.Properties.IsRightButtonPressed && _hoveredPixel is { } hover)
        {
            SecondaryPickRequested?.Invoke(hover.X, hover.Y);
            e.Handled = true;
            return;
        }
        if (!point.Properties.IsLeftButtonPressed) return;''',
'''        if (point.Properties.IsRightButtonPressed && _hoveredPixel is not null)
        {
            _drawing = true;
            Capture(e.Pointer);
            DispatchPointer(e, EditorPointerKind.Pressed);
            e.Handled = true;
            return;
        }
        if (!point.Properties.IsLeftButtonPressed) return;''')

patch('src/MyLovePixel.Desktop/MainWindow.Runtime.cs',
'''public sealed partial class MainWindow
{
''',
'''public sealed partial class MainWindow
{
    private EditorEditGesture? _secondaryEraseGesture;
    private IntPoint? _lastSecondaryErasePixel;

''')

patch('src/MyLovePixel.Desktop/MainWindow.Runtime.cs',
'''            if (!_selectionMode && e.Kind == EditorPointerKind.Pressed)
                _canvasPointerActive = true;

            if (_busy) return;
            if (e.Kind == EditorPointerKind.Pressed)
            {
                FinishParameterEdit();
                _playback.Stop(session);
                if (!_selectionMode)
                {
                    if (session.DrawingBlockedReason is { } blocked) throw new InvalidOperationException(blocked);
                    session.EnsureEditableCel();
                }
            }

            if (_selectionMode)
''',
'''            if (_busy) return;
            if (e.Kind == EditorPointerKind.Pressed)
            {
                FinishParameterEdit();
                _playback.Stop(session);
            }

            if (_secondaryEraseGesture is not null || (e.Buttons & EditorPointerButtons.Secondary) != 0)
            {
                HandleSecondaryErase(session, e);
                return;
            }

            if (!_selectionMode && e.Kind == EditorPointerKind.Pressed)
            {
                _canvasPointerActive = true;
                if (session.DrawingBlockedReason is { } blocked) throw new InvalidOperationException(blocked);
                session.EnsureEditableCel();
            }

            if (_selectionMode)
''')

patch('src/MyLovePixel.Desktop/MainWindow.Runtime.cs',
'''        catch (Exception ex)
        {
            _canvasPointerActive = false;
''',
'''        catch (Exception ex)
        {
            FinishSecondaryErase(commit: false);
            _canvasPointerActive = false;
''')

patch('src/MyLovePixel.Desktop/MainWindow.Runtime.cs',
'''    private void CancelCanvasInteraction()
    {
        _canvasPointerActive = false;
''',
'''    private void HandleSecondaryErase(DocumentSession session, EditorPointerEvent e)
    {
        if (e.Kind == EditorPointerKind.Pressed)
        {
            FinishSecondaryErase(commit: false);
            if (session.DrawingBlockedReason is { } blocked) throw new InvalidOperationException(blocked);
            _secondaryEraseGesture = session.BeginUserEdit("Erase Pixels");
            _lastSecondaryErasePixel = null;
            _canvasPointerActive = true;
        }

        if (_secondaryEraseGesture is null) return;

        if (e.Kind is EditorPointerKind.Pressed or EditorPointerKind.Moved or EditorPointerKind.Released)
        {
            if (_lastSecondaryErasePixel != e.CanvasPixel)
            {
                session.EraseCanvasPixel(e.CanvasPixel.X, e.CanvasPixel.Y);
                _lastSecondaryErasePixel = e.CanvasPixel;
                QueueCanvasRefresh();
            }
        }

        if (e.Kind == EditorPointerKind.Released)
        {
            FinishSecondaryErase(commit: true);
            QueueRefreshAll();
        }
    }

    private void FinishSecondaryErase(bool commit)
    {
        var gesture = _secondaryEraseGesture;
        _secondaryEraseGesture = null;
        _lastSecondaryErasePixel = null;
        _canvasPointerActive = false;
        gesture?.Finish(commit);
    }

    private void CancelCanvasInteraction()
    {
        FinishSecondaryErase(commit: false);
        _canvasPointerActive = false;
''')

patch('src/MyLovePixel.Desktop/MainWindow.Runtime.cs',
'''    private void ErasePixelFromCanvas(int x, int y)
    {
        var session = Current();
        if (session is null) return;
        if (_busy) return;
        FinishParameterEdit();
        _playback.Stop(session);
        Safe(() => session.EraseCanvasPixel(x, y));
        QueueRefreshAll();
    }

''',
'')

patch('src/MyLovePixel.Desktop/MainWindow.cs',
'''        _canvas.SecondaryPickRequested = ErasePixelFromCanvas;
''',
'')

patch('src/MyLovePixel.Desktop/PixelCanvasView.cs',
'''    public Action<int, int>? SecondaryPickRequested { get; set; }
''',
'')

p = ROOT / 'src/MyLovePixel.Desktop/EditorStyles.cs'
text = p.read_text(encoding='utf-8')
if 'using Avalonia.Animation;' not in text:
    text = text.replace('using Avalonia;\n', 'using Avalonia;\nusing Avalonia.Animation;\n', 1)
p.write_text(text, encoding='utf-8')

patch('src/MyLovePixel.Desktop/EditorStyles.cs',
'''                new Setter(Expander.BackgroundProperty, EditorThemeTokens.Surface),
                new Setter(Expander.BorderBrushProperty, EditorThemeTokens.PanelBorder),
''',
'''                new Setter(Expander.BackgroundProperty, EditorThemeTokens.Surface),
                new Setter(Expander.BorderBrushProperty, EditorThemeTokens.PanelBorder),
                new Setter(Expander.TransitionsProperty, HoverBrushTransitions(
                    Expander.BackgroundProperty, Expander.BorderBrushProperty, Expander.ForegroundProperty)),
''')

patch('src/MyLovePixel.Desktop/EditorStyles.cs',
'''                new Setter(Button.FontSizeProperty, 13d),
                new Setter(Button.MinHeightProperty, 30d),
''',
'''                new Setter(Button.FontSizeProperty, 13d),
                new Setter(Button.MinHeightProperty, 30d),
                new Setter(Button.TransitionsProperty, HoverBrushTransitions(
                    Button.BackgroundProperty, Button.BorderBrushProperty, Button.ForegroundProperty)),
''')

patch('src/MyLovePixel.Desktop/EditorStyles.cs',
'''                new Setter(CheckBox.ForegroundProperty, EditorThemeTokens.TextPrimary),
                new Setter(CheckBox.FontSizeProperty, 13d),
''',
'''                new Setter(CheckBox.ForegroundProperty, EditorThemeTokens.TextPrimary),
                new Setter(CheckBox.FontSizeProperty, 13d),
                new Setter(CheckBox.TransitionsProperty, HoverBrushTransitions(
                    CheckBox.BackgroundProperty, CheckBox.BorderBrushProperty, CheckBox.ForegroundProperty)),
''')

patch('src/MyLovePixel.Desktop/EditorStyles.cs',
'''                new Setter(TabItem.PaddingProperty, new Thickness(10, 7)),
                new Setter(TabItem.FontSizeProperty, 13d),
''',
'''                new Setter(TabItem.PaddingProperty, new Thickness(10, 7)),
                new Setter(TabItem.FontSizeProperty, 13d),
                new Setter(TabItem.TransitionsProperty, HoverBrushTransitions(
                    TabItem.BackgroundProperty, TabItem.BorderBrushProperty, TabItem.ForegroundProperty)),
''')

patch('src/MyLovePixel.Desktop/EditorStyles.cs',
'''                new Setter(borderBrush, EditorThemeTokens.PanelBorder),
                new Setter(borderThickness, new Thickness(1)),
''',
'''                new Setter(borderBrush, EditorThemeTokens.PanelBorder),
                new Setter(borderThickness, new Thickness(1)),
                new Setter(Animatable.TransitionsProperty, HoverBrushTransitions(background, borderBrush, foreground)),
''')

p = ROOT / 'src/MyLovePixel.Desktop/EditorStyles.cs'
text = p.read_text(encoding='utf-8')
needle = '\n}\n'
pos = text.rfind(needle)
if pos < 0:
    raise RuntimeError('EditorStyles class closing brace missing')
helper = '''
    private static Transitions HoverBrushTransitions(params AvaloniaProperty[] properties)
    {
        var transitions = new Transitions();
        foreach (var property in properties)
        {
            transitions.Add(new BrushTransition
            {
                Property = property,
                Duration = TimeSpan.FromMilliseconds(140),
            });
        }
        return transitions;
    }
'''
text = text[:pos] + helper + text[pos:]
p.write_text(text, encoding='utf-8')
