from pathlib import Path

def replace(path, before, after):
    p = Path(path)
    s = p.read_text(encoding='utf-8-sig')
    if before not in s:
        raise RuntimeError(f'Expected source not found: {path}: {before[:80]}')
    s = s.replace(before, after, 1)
    p.write_text(s, encoding='utf-8')

replace('src/MyLovePixel.Application/Workspace.cs', 'public sealed class DocumentSession\n', 'public sealed partial class DocumentSession\n')
replace('src/MyLovePixel.Application/Workspace.cs', '        IsDirty = true;\n        foreach (var dirty in change.DirtySurfaces)', '        IsDirty = true;\n        DocumentVersion++;\n        foreach (var dirty in change.DirtySurfaces)')
replace('src/MyLovePixel.Application/Workspace.cs', '    public ToolDispatchPresentation DispatchPointer(EditorPointerEvent pointerEvent)\n    {', '    public ToolDispatchPresentation DispatchPointer(EditorPointerEvent pointerEvent)\n    {\n        if (DrawingBlockedReason is { } blocked)\n            throw new InvalidOperationException(blocked);')
replace('src/MyLovePixel.Application/AdvancedEditing.cs', '        session.CancelToolInteraction();\n        session.EnsureEditableCel();', '        if (session.DrawingBlockedReason is { } blocked) throw new InvalidOperationException(blocked);\n        session.CancelToolInteraction();\n        session.EnsureEditableCel();')
