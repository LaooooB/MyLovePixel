using System.Text.Json;
using MyLovePixel.Persistence;

namespace MyLovePixel.Recovery;

public enum RecoveryErrorCode
{
    InvalidConfiguration = 1,
    CheckpointWriteFailed = 2,
    JournalWriteFailed = 3,
    CandidateNotRecoverable = 4,
    IoFailure = 5,
}

public sealed class RecoveryException : InvalidOperationException
{
    public RecoveryException(RecoveryErrorCode code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public RecoveryErrorCode Code { get; }
}

public sealed class RecoveryStore
{
    private const int JournalVersion = 1;
    private const string JournalSuffix = ".recovery.json";
    private readonly RecoveryOptions _options;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public RecoveryStore(RecoveryOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        try
        {
            _options.Validate();
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            throw new RecoveryException(RecoveryErrorCode.InvalidConfiguration, ex.Message, ex);
        }
    }

    public RecoveryOptions Options => _options;

    public RecoveryCheckpoint WriteCheckpoint(
        PixelProject project,
        string? sourcePath = null,
        DateTimeOffset? createdUtc = null,
        IRecoveryFailureInjector? failureInjector = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var injector = failureInjector ?? NoRecoveryFailureInjector.Instance;
        var timestamp = (createdUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var documentId = project.Document.Id.Value.ToString("N");
        var directory = Path.Combine(_options.FullRootDirectory, documentId);
        Directory.CreateDirectory(directory);
        var recoveryId = FindAvailableRecoveryId(directory, timestamp);
        var checkpointFile = recoveryId + ".pixelproj";
        var journalFile = recoveryId + JournalSuffix;
        var checkpointPath = Path.Combine(directory, checkpointFile);
        var journalPath = Path.Combine(directory, journalFile);
        var semanticHash = ProjectSemanticHash.Compute(project.Document);
        var normalizedSourcePath = string.IsNullOrWhiteSpace(sourcePath) ? null : Path.GetFullPath(sourcePath);

        try
        {
            injector.Checkpoint(RecoveryWriteStage.BeforeCheckpoint);
            PixelProjectFile.Save(checkpointPath, project);
            var verified = PixelProjectFile.Load(checkpointPath);
            var verifiedHash = ProjectSemanticHash.Compute(verified.Document);
            if (!string.Equals(semanticHash, verifiedHash, StringComparison.Ordinal))
                throw new RecoveryException(
                    RecoveryErrorCode.CheckpointWriteFailed,
                    "Recovery checkpoint verification produced a semantic mismatch.");
            injector.Checkpoint(RecoveryWriteStage.AfterCheckpointValidated);

            var journal = new RecoveryJournalDto(
                JournalVersion,
                recoveryId,
                documentId,
                normalizedSourcePath,
                checkpointFile,
                timestamp,
                semanticHash);
            injector.Checkpoint(RecoveryWriteStage.BeforeJournalCommit);
            WriteJournalAtomic(journalPath, journal);
            injector.Checkpoint(RecoveryWriteStage.AfterJournalCommit);

            injector.Checkpoint(RecoveryWriteStage.BeforeRotation);
            RotateVerifiedCheckpoints(directory, recoveryId);
            injector.Checkpoint(RecoveryWriteStage.AfterRotation);

            return new RecoveryCheckpoint(
                recoveryId,
                documentId,
                normalizedSourcePath,
                checkpointPath,
                journalPath,
                timestamp,
                semanticHash);
        }
        catch (RecoveryException)
        {
            throw;
        }
        catch (IOException ex)
        {
            throw new RecoveryException(RecoveryErrorCode.IoFailure, "Recovery checkpoint I/O failed.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new RecoveryException(RecoveryErrorCode.IoFailure, "Recovery checkpoint access was denied.", ex);
        }
    }

    public RecoveryDiscovery Discover()
    {
        var root = _options.FullRootDirectory;
        if (!Directory.Exists(root)) return new RecoveryDiscovery(Array.Empty<RecoveryCandidate>());

        var values = Directory
            .EnumerateFiles(root, "*" + JournalSuffix, SearchOption.AllDirectories)
            .Select(ReadCandidate)
            .OrderByDescending(candidate => candidate.CreatedUtc ?? DateTimeOffset.MinValue)
            .ThenBy(candidate => candidate.JournalPath, StringComparer.Ordinal)
            .ToArray();
        return new RecoveryDiscovery(values);
    }

    public PixelProject Recover(RecoveryCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!candidate.IsRecoverable || candidate.CheckpointPath is null)
            throw new RecoveryException(
                RecoveryErrorCode.CandidateNotRecoverable,
                $"Recovery candidate '{candidate.RecoveryId}' is not recoverable ({candidate.State}).");

        var project = PixelProjectFile.Load(candidate.CheckpointPath);
        var hash = ProjectSemanticHash.Compute(project.Document);
        if (!string.Equals(hash, candidate.SemanticHash, StringComparison.Ordinal))
            throw new RecoveryException(
                RecoveryErrorCode.CandidateNotRecoverable,
                $"Recovery candidate '{candidate.RecoveryId}' no longer matches its journal semantic hash.");
        return project;
    }

    private RecoveryCandidate ReadCandidate(string journalPath)
    {
        RecoveryJournalDto? journal;
        try
        {
            journal = JsonSerializer.Deserialize<RecoveryJournalDto>(File.ReadAllText(journalPath), _jsonOptions);
            if (journal is null ||
                journal.Version != JournalVersion ||
                string.IsNullOrWhiteSpace(journal.RecoveryId) ||
                string.IsNullOrWhiteSpace(journal.DocumentId) ||
                string.IsNullOrWhiteSpace(journal.CheckpointFile) ||
                string.IsNullOrWhiteSpace(journal.SemanticHash) ||
                Path.GetFileName(journal.CheckpointFile) != journal.CheckpointFile)
                throw new JsonException("Recovery journal is missing required versioned fields.");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new RecoveryCandidate(
                Path.GetFileName(journalPath),
                null,
                null,
                null,
                journalPath,
                null,
                null,
                RecoveryCandidateState.InvalidJournal,
                ex.Message);
        }

        var checkpointPath = Path.Combine(Path.GetDirectoryName(journalPath)!, journal.CheckpointFile);
        if (!File.Exists(checkpointPath))
            return ToCandidate(journal, journalPath, checkpointPath, RecoveryCandidateState.MissingCheckpoint, "Checkpoint file is missing.");

        try
        {
            var project = PixelProjectFile.Load(checkpointPath);
            if (!string.Equals(project.Document.Id.Value.ToString("N"), journal.DocumentId, StringComparison.OrdinalIgnoreCase))
                return ToCandidate(journal, journalPath, checkpointPath, RecoveryCandidateState.SemanticMismatch, "Checkpoint DocumentId does not match the recovery journal.");
            var semanticHash = ProjectSemanticHash.Compute(project.Document);
            if (!string.Equals(semanticHash, journal.SemanticHash, StringComparison.Ordinal))
                return ToCandidate(journal, journalPath, checkpointPath, RecoveryCandidateState.SemanticMismatch, "Checkpoint semantic hash does not match the recovery journal.");
            return ToCandidate(journal, journalPath, checkpointPath, RecoveryCandidateState.Valid, null);
        }
        catch (Exception ex) when (ex is PixelProjectException or IOException or UnauthorizedAccessException)
        {
            return ToCandidate(journal, journalPath, checkpointPath, RecoveryCandidateState.CorruptCheckpoint, ex.Message);
        }
    }

    private static RecoveryCandidate ToCandidate(
        RecoveryJournalDto journal,
        string journalPath,
        string checkpointPath,
        RecoveryCandidateState state,
        string? error) =>
        new(
            journal.RecoveryId,
            journal.DocumentId,
            journal.SourcePath,
            checkpointPath,
            journalPath,
            journal.CreatedUtc,
            journal.SemanticHash,
            state,
            error);

    private void RotateVerifiedCheckpoints(string directory, string currentRecoveryId)
    {
        var valid = Directory
            .EnumerateFiles(directory, "*" + JournalSuffix, SearchOption.TopDirectoryOnly)
            .Select(ReadCandidate)
            .Where(candidate => candidate.IsRecoverable)
            .OrderByDescending(candidate => candidate.CreatedUtc)
            .ThenByDescending(candidate => candidate.RecoveryId, StringComparer.Ordinal)
            .ToList();

        if (!valid.Any(candidate => string.Equals(candidate.RecoveryId, currentRecoveryId, StringComparison.Ordinal)))
            throw new RecoveryException(RecoveryErrorCode.JournalWriteFailed, "New recovery checkpoint was not discoverable after journal commit.");

        foreach (var obsolete in valid.Skip(_options.RetentionCount))
        {
            // Journal first: a crash between the two deletes leaves an ignored orphan checkpoint,
            // never a journal that points at a deleted checkpoint.
            File.Delete(obsolete.JournalPath);
            if (obsolete.CheckpointPath is not null) File.Delete(obsolete.CheckpointPath);
        }
    }

    private void WriteJournalAtomic(string path, RecoveryJournalDto journal)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        var committed = false;
        try
        {
            var json = JsonSerializer.Serialize(journal, _jsonOptions);
            File.WriteAllText(tempPath, json);
            var verified = JsonSerializer.Deserialize<RecoveryJournalDto>(File.ReadAllText(tempPath), _jsonOptions);
            if (verified is null || verified.Version != JournalVersion || verified.RecoveryId != journal.RecoveryId)
                throw new RecoveryException(RecoveryErrorCode.JournalWriteFailed, "Recovery journal verification failed before commit.");
            File.Move(tempPath, path, overwrite: false);
            committed = true;
        }
        finally
        {
            if (!committed)
            {
                try { File.Delete(tempPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static string FindAvailableRecoveryId(string directory, DateTimeOffset timestamp)
    {
        var prefix = "checkpoint-" + timestamp.UtcTicks.ToString("D19", System.Globalization.CultureInfo.InvariantCulture);
        var candidate = prefix;
        var suffix = 0;
        while (File.Exists(Path.Combine(directory, candidate + ".pixelproj")) ||
               File.Exists(Path.Combine(directory, candidate + JournalSuffix)))
        {
            suffix++;
            candidate = prefix + "-" + suffix.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
        }
        return candidate;
    }
}
