namespace MyLovePixel.Export;

public enum AssetPipelineErrorCode
{
    InvalidRequest = 1,
    ExporterNotFound = 2,
    ExportFailed = 3,
    ImporterNotFound = 4,
    UnsupportedInput = 5,
    ImportFailed = 6,
}

public sealed class AssetPipelineException : Exception
{
    public AssetPipelineException(
        AssetPipelineErrorCode code,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public AssetPipelineErrorCode Code { get; }
}
