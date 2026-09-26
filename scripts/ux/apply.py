from pathlib import Path
import runpy

ROOT = Path(__file__).resolve().parents[2]

def replace(path, old, new):
    file = ROOT / path
    source = file.read_text(encoding='utf-8-sig')
    if new in source:
        return
    if source.count(old) != 1:
        raise RuntimeError(f'{path}: expected one patch anchor, found {source.count(old)}: {old[:100]}')
    file.write_text(source.replace(old, new, 1), encoding='utf-8', newline='\n')

replace('src/MyLovePixel.Application/Workspace.cs',
    'public sealed class DocumentSession\n', 'public sealed partial class DocumentSession\n')
replace('src/MyLovePixel.Application/Workspace.cs',
    '        _activeToolId = tool.Descriptor.Id;\n        _toolHost?.SetActiveTool(tool);',
    '        RememberToolOptions();\n        _activeToolId = tool.Descriptor.Id;\n        _toolHost?.SetActiveTool(tool);\n        RestoreToolOptions();')
replace('src/MyLovePixel.Application/Workspace.cs',
    '        _toolHost.SetOption(id, value);\n        StateChanged?.Invoke(this, EventArgs.Empty);',
    '        _toolHost.SetOption(id, value);\n        RememberToolOptions();\n        StateChanged?.Invoke(this, EventArgs.Empty);')
replace('src/MyLovePixel.Application/Workspace.cs',
    '    public ToolDispatchPresentation DispatchPointer(EditorPointerEvent pointerEvent)\n    {',
    '    public ToolDispatchPresentation DispatchPointer(EditorPointerEvent pointerEvent)\n    {\n        if (pointerEvent.Kind != EditorPointerKind.Cancelled) this.RequireWritableLayer();')
replace('src/MyLovePixel.Application/Workspace.cs',
    '                _secondaryColor);\n        }\n        else',
    '                _secondaryColor);\n            RestoreToolOptions();\n        }\n        else')
replace('src/MyLovePixel.Application/Workspace.cs',
    '        _primaryColor = primary;\n        _secondaryColor = secondary;',
    '        if (_primaryColor == primary && _secondaryColor == secondary) return;\n        _primaryColor = primary;\n        _secondaryColor = secondary;')
replace('src/MyLovePixel.Application/AdvancedEditing.cs',
    '        if (session.HasEditableCel) return;\n        session.Execute(new EnsureCelCommand',
    '        session.RequireWritableLayer();\n        if (session.HasEditableCel) return;\n        session.Execute(new EnsureCelCommand')
replace('src/MyLovePixel.Application/AdvancedEditing.Canvas.cs',
    '.Where(value => value.FrameId == frameId)',
    '.Where(value => value.FrameId == frameId && !initial.GetLayer(value.LayerId).Locked)')
replace('src/MyLovePixel.Application/AdvancedEditing.Canvas.cs',
    '                    value.FrameId != frameId &&\n                    value.SurfaceId == cel.SurfaceId)',
    '                    (value.FrameId != frameId || snapshot.GetLayer(value.LayerId).Locked) &&\n                    value.SurfaceId == cel.SurfaceId)')
replace('src/MyLovePixel.Application/PluginWorkspaceRuntime.cs',
    '    public ToolDispatchPresentation DispatchPointer(DocumentSession session, EditorPointerEvent pointerEvent)\n    {\n        EnsureOwned(session);',
    '    public ToolDispatchPresentation DispatchPointer(DocumentSession session, EditorPointerEvent pointerEvent)\n    {\n        EnsureOwned(session);\n        if (pointerEvent.Kind != EditorPointerKind.Cancelled) session.RequireWritableLayer();')
replace('src/MyLovePixel.Application/PluginWorkspaceRuntime.cs',
    '        if (patch is null) return new PluginPanelActionResult(true, false, null);',
    '        if (patch is null) return new PluginPanelActionResult(true, false, null);\n        var restriction = session.EditingRestriction();\n        if (restriction is not null) return new PluginPanelActionResult(false, false, restriction);')

for stage in sorted((ROOT / 'scripts/ux').glob('stage*.py')):
    runpy.run_path(str(stage), init_globals={'ROOT': ROOT, 'replace': replace})
