using MyLovePixel.Persistence;

namespace MyLovePixel.Recovery;

public enum RecoveryCandidateState
{
    Valid = 1,
    InvalidJournal = 2,
    MissingCheckpoint = 3,
    CorruptCheckpoint = 4,
    SemanticMismatch = 5,
}

public enum RecoveryWriteStage
{
    BeforeCheckpoint = 1,
    AfterCheckpointValidated = 2,
    BeforeJournalCommit = 3,
    AfterJournalCommit = 4,
    BeforeRotation = 5,
    AfterRotation = 6,
}

public interface IRecoveryFailureInjector
{
    void Checkpoint(RecoveryWriteStage stage);
}

public sealed class NoRecoveryFailureInjector : IRecoveryFailureInjector
{
    public static NoRecoveryFailureInjector Instance { get; } = new();
    private NoRecoveryFailureInjector() { }
    public void Checkpoint(RecoveryWriteStage stage) { }
}

public sealed record RecoveryOptions(string RootDirectory, int RetentionCount = 3)
{
    public string FullRootDirectory => Path.GetFullPath(RootDirectory);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RootDirectory))
            throw new ArgumentException("Recovery root directory cannot be empty.", nameof(RootDirectory));
        if (RetentionCount < 1)
            throw new ArgumentOutOfRangeException(nameof(RetentionCount), "Recovery retention must be at least one checkpoint.");
    }
}

public sealed record RecoveryCheckpoint(
    string RecoveryId,
    string DocumentId,
    string? SourcePath,
    string CheckpointPath,
    string JournalPath,
    DateTimeOffset CreatedUtc,
    string SemanticHash);

public sealed record RecoveryCandidate(
    string RecoveryId,
    string? DocumentId,
    string? SourcePath,
    string? CheckpointPath,
    string JournalPath,
    DateTimeOffset? CreatedUtc,
    string? SemanticHash,
    RecoveryCandidateState State,
    string? Error)
{
    public bool IsRecoverable => State == RecoveryCandidateState.Valid && CheckpointPath is not null;
}

public sealed record RecoveryDiscovery(IReadOnlyList<RecoveryCandidate> Candidates)
{
    public IReadOnlyList<RecoveryCandidate> ValidCandidates =>
        Candidates.Where(candidate => candidate.IsRecoverable).ToArray();

    public RecoveryCandidate? NewestValid => ValidCandidates.FirstOrDefault();
}

internal sealed record RecoveryJournalDto(
    int Version,
    string RecoveryId,
    string DocumentId,
    string? SourcePath,
    string CheckpointFile,
    DateTimeOffset CreatedUtc,
    string SemanticHash);
