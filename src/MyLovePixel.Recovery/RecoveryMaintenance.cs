namespace MyLovePixel.Recovery;

public static class RecoveryStoreMaintenance
{
    public static bool Discard(this RecoveryStore store, string recoveryId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryId);

        var candidate = store.Discover().Candidates
            .FirstOrDefault(value => string.Equals(value.RecoveryId, recoveryId, StringComparison.Ordinal));
        if (candidate is null) return false;

        try
        {
            // Remove the journal first so a crash can only leave an ignored orphan checkpoint,
            // never a journal that points at a deleted checkpoint.
            File.Delete(candidate.JournalPath);
            if (candidate.CheckpointPath is not null)
                File.Delete(candidate.CheckpointPath);
            return true;
        }
        catch (IOException ex)
        {
            throw new RecoveryException(RecoveryErrorCode.IoFailure, $"Failed to discard recovery candidate '{recoveryId}'.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new RecoveryException(RecoveryErrorCode.IoFailure, $"Access was denied while discarding recovery candidate '{recoveryId}'.", ex);
        }
    }
}
