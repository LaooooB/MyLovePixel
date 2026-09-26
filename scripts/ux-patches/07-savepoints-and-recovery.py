from pathlib import Path
A=Path('src/MyLovePixel.Application'); P=Path('src/MyLovePixel.Persistence'); D=Path('src/MyLovePixel.Desktop')
p=A/'Workspace.cs'; s=p.read_text(encoding='utf-8')
s=s.replace('        IsDirty = IsRecovered;', '        IsDirty = IsRecovered;\n        _savedSemanticHash = IsRecovered ? null : ProjectSemanticHash.Compute(Document);',1)
s=s.replace('        Commands.Undo();\n        RefreshToolTarget();', '        Commands.Undo();\n        RefreshToolTarget();\n        RefreshSavedState();',1)
s=s.replace('        Commands.Redo();\n        RefreshToolTarget();', '        Commands.Redo();\n        RefreshToolTarget();\n        RefreshSavedState();',1)
s=s.replace('        IsDirty = false;\n        StateChanged?.Invoke', '        _savedSemanticHash = ProjectSemanticHash.Compute(Document);\n        IsDirty = false;\n        StateChanged?.Invoke',1)
a=s.index('    internal void MarkImported()'); s=s[:a]+s[a:].replace('        IsDirty = true;', '        _savedSemanticHash = null;\n        IsDirty = true;',1)
p.write_text(s,encoding='utf-8')
(A/'Workspace.Savepoint.cs').write_text('''using MyLovePixel.Persistence;

namespace MyLovePixel.Application;

public sealed partial class DocumentSession
{
    private string? _savedSemanticHash;

    internal void RefreshSavedState()
    {
        var dirty = _savedSemanticHash is null || !string.Equals(_savedSemanticHash, ProjectSemanticHash.Compute(Document), StringComparison.Ordinal);
        if (IsDirty == dirty) return;
        IsDirty = dirty;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
''',encoding='utf-8')
p=A/'EditorEditGesture.cs'; s=p.read_text(encoding='utf-8')
s=s.replace('    private CommandTransaction? _transaction;\n    internal EditorEditGesture(DocumentSession session, string name) => _transaction = session.Commands.BeginTransaction(name);', '''    private CommandTransaction? _transaction;
    private readonly DocumentSession _session;
    internal EditorEditGesture(DocumentSession session, string name)
    {
        _session = session;
        _transaction = session.Commands.BeginTransaction(name);
    }''')
s=s.replace('        transaction.Dispose();','        transaction.Dispose();\n        _session.RefreshSavedState();')
p.write_text(s,encoding='utf-8')
p=P/'PixelProjectFile.cs'; s=p.read_text(encoding='utf-8').replace('public static class PixelProjectFile','public static partial class PixelProjectFile',1); p.write_text(s,encoding='utf-8')
(P/'PixelProjectFile.Snapshot.cs').write_text('''namespace MyLovePixel.Persistence;

public static partial class PixelProjectFile
{
    /// <summary>Detaches editable resources and persistence metadata before background I/O.</summary>
    public static PixelProject CaptureDetached(PixelProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var package = BuildPackage(project);
        var document = ProjectMapper.FromDto(package.PersistenceState.DocumentTemplate!, package.LogicalEntries);
        return new PixelProject(document, package.PersistenceState);
    }
}
''',encoding='utf-8')
p=A/'RecoveryWorkspaceCoordinator.cs'; s=p.read_text(encoding='utf-8').replace('public sealed class RecoveryWorkspaceCoordinator','public sealed partial class RecoveryWorkspaceCoordinator',1); p.write_text(s,encoding='utf-8')
(A/'RecoveryWorkspaceCoordinator.Async.cs').write_text('''using MyLovePixel.Persistence;
using MyLovePixel.Recovery;

namespace MyLovePixel.Application;

public sealed partial class RecoveryWorkspaceCoordinator
{
    private int _asyncTickRunning;

    public async Task<IReadOnlyList<AutosaveAttemptPresentation>> TickAsync(DateTimeOffset now)
    {
        if (Interlocked.Exchange(ref _asyncTickRunning, 1) != 0) return Array.Empty<AutosaveAttemptPresentation>();
        try
        {
            var utc = now.ToUniversalTime();
            var pending = new List<(string Id, string? Source, PixelProject Project)>();
            // This part executes on the owning thread before the first await. The
            // writer never reads the live document while the user keeps editing.
            foreach (var session in _workspace.Sessions)
            {
                if (!session.IsDirty) continue;
                var id = session.CaptureSnapshot().Id.Value.ToString("N");
                if (_lastCheckpointUtc.TryGetValue(id, out var last) && utc - last < _policy.Interval) continue;
                pending.Add((id, session.FilePath ?? session.RecoverySourcePath, PixelProjectFile.CaptureDetached(session.Project)));
            }
            if (pending.Count == 0) return Array.Empty<AutosaveAttemptPresentation>();
            var results = await Task.Run(() =>
            {
                var attempts = new List<AutosaveAttemptPresentation>();
                foreach (var item in pending)
                {
                    try
                    {
                        var checkpoint = _store.WriteCheckpoint(item.Project, item.Source, utc);
                        attempts.Add(new AutosaveAttemptPresentation(item.Id, true, checkpoint.RecoveryId, null));
                    }
                    catch (RecoveryException ex) { attempts.Add(new AutosaveAttemptPresentation(item.Id, false, null, ex.Message)); }
                }
                return (IReadOnlyList<AutosaveAttemptPresentation>)attempts.AsReadOnly();
            });
            foreach (var result in results)
                if (result.WroteCheckpoint) _lastCheckpointUtc[result.DocumentId] = utc;
            return results;
        }
        finally { Volatile.Write(ref _asyncTickRunning, 0); }
    }
}
''',encoding='utf-8')
p=D/'MainWindow.DocumentsUx.cs'; s=p.read_text(encoding='utf-8'); a=s.index('    private void OnAutosaveTick('); b=s.index('\n    }',a)+6
s=s[:a]+'''    private async void OnAutosaveTick(object? sender, EventArgs e)
    {
        if (_closed || _busy || _autosaveRunning || _canvasPointerActive || _parameterEdit is not null) return;
        _autosaveRunning = true;
        try
        {
            var attempts = await _recovery.TickAsync(DateTimeOffset.UtcNow);
            if (_closed) return;
            var failure = attempts.FirstOrDefault(a => !a.WroteCheckpoint);
            if (failure is not null) SetError(failure.Error ?? "Could not save a recovery copy.");
            if (attempts.Count > 0) _panelStamps.Remove(_recoveryPanel);
        }
        catch (Exception ex) { if (!_closed) SetError(ex.Message); }
        finally { _autosaveRunning = false; }
    }'''+s[b:]; p.write_text(s,encoding='utf-8')
# Record the exact assembled source regardless of the test outcome.
import zipfile
Path('release').mkdir(exist_ok=True)
with zipfile.ZipFile('release/review-source.zip','w',zipfile.ZIP_DEFLATED) as z:
 for directory in ['src','tests','docs','.github','scripts']:
  for p in Path(directory).rglob('*'):
   if p.is_file() and not any(v in p.parts for v in ['bin','obj','__pycache__']): z.write(p,p.as_posix())
 for name in ['Directory.Build.props','Directory.Packages.props','MyLovePixel.slnx','global.json','HANDOFF.md','README.md','THIRD_PARTY_NOTICES.md']:
  if Path(name).is_file(): z.write(name)
