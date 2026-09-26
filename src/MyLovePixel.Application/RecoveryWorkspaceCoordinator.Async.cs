using MyLovePixel.Persistence;
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
