namespace MyLovePixel.Persistence;

public enum PixelProjectErrorCode
{
    InvalidContainer,
    MissingEntry,
    DuplicateEntry,
    UnsupportedSchemaVersion,
    MigrationMissing,
    MigrationInvalid,
    InvalidJson,
    ContentHashMismatch,
    InvalidSurface,
    InvalidReference,
    ValidationFailed,
    IoFailure,
}

public sealed class PixelProjectException : Exception
{
    public PixelProjectException(
        PixelProjectErrorCode code,
        string message,
        string? entryName = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        EntryName = entryName;
    }

    public PixelProjectErrorCode Code { get; }
    public string? EntryName { get; }
}
