using MyLovePixel.Recovery;

namespace MyLovePixel.Application;

public sealed record AutosavePolicy(
    TimeSpan Interval,
    int RetentionCount = 3)
{
    public static AutosavePolicy Default { get; } = new(TimeSpan.FromMinutes(2), 3);

    public void Validate()
    {
        if (Interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(Interval), "Autosave interval must be positive.");
        if (RetentionCount < 1)
            throw new ArgumentOutOfRangeException(nameof(RetentionCount), "Autosave retention must be at least one checkpoint.");
    }
}

public sealed record RecoveryCandidatePresentation(
    string RecoveryId,
    string? SourcePath,
    DateTimeOffset? CreatedUtc,
    string State,
    bool IsRecoverable,
    string? Error);

public sealed record AutosaveAttemptPresentation(
    string DocumentId,
    bool WroteCheckpoint,
    string? RecoveryId,
    string? Error);

public sealed class RecoveryWorkspaceCoordinator
{
    private readonly EditorWorkspace _workspace;
    private readonly RecoveryStore _store;
    private readonly AutosavePolicy _policy;
    private readonly Dictionary<string, DateTimeOffset> _lastCheckpointUtc = new(StringComparer.Ordinal);

    public RecoveryWorkspaceCoordinator(
        EditorWorkspace workspace,
        string rootDirectory,
        AutosavePolicy? policy = null)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _policy = policy ?? AutosavePolicy.Default;
        _policy.Validate();
        _store = new RecoveryStore(new RecoveryOptions(rootDirectory, _policy.RetentionCount));
    }

    public string RecoveryRootDirectory => _store.Options.FullRootDirectory;

    public IReadOnlyList<RecoveryCandidatePresentation> Discover()
    {
        return _store.Discover().Candidates
            .Select(candidate => new RecoveryCandidatePresentation(
                candidate.RecoveryId,
                candidate.SourcePath,
                candidate.CreatedUtc,
                candidate.State.ToString(),
                candidate.IsRecoverable,
                candidate.Error))
            .ToArray();
    }

    public IReadOnlyList<AutosaveAttemptPresentation> Tick(DateTimeOffset now)
    {
        var utcNow = now.ToUniversalTime();
        var results = new List<AutosaveAttemptPresentation>();

        foreach (var session in _workspace.Sessions)
        {
            if (!session.IsDirty) continue;
            var documentId = session.CaptureSnapshot().Id.Value.ToString("N");
            if (_lastCheckpointUtc.TryGetValue(documentId, out var last) && utcNow - last < _policy.Interval)
                continue;

            try
            {
                var sourcePath = session.FilePath ?? session.RecoverySourcePath;
                var checkpoint = _store.WriteCheckpoint(session.Project, sourcePath, utcNow);
                _lastCheckpointUtc[documentId] = checkpoint.CreatedUtc;
                results.Add(new AutosaveAttemptPresentation(documentId, true, checkpoint.RecoveryId, null));
            }
            catch (RecoveryException ex)
            {
                results.Add(new AutosaveAttemptPresentation(documentId, false, null, ex.Message));
            }
        }

        return results.AsReadOnly();
    }

    public DocumentSession Recover(string recoveryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryId);
        var candidate = _store.Discover().Candidates
            .FirstOrDefault(value => string.Equals(value.RecoveryId, recoveryId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Recovery candidate '{recoveryId}' does not exist.");
        if (!candidate.IsRecoverable)
            throw new InvalidOperationException($"Recovery candidate '{recoveryId}' is not recoverable ({candidate.State}).");

        var project = _store.Recover(candidate);
        return _workspace.OpenRecovered(project, candidate.SourcePath, candidate.RecoveryId);
    }

    public bool Dismiss(string recoveryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryId);
        return _store.Discard(recoveryId);
    }
}
