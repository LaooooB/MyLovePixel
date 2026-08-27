namespace MyLovePixel.Persistence;

public static class PixelProjectFormat
{
    public const string FormatMarker = "MyLovePixel.Project";
    public const int CurrentSchemaVersion = 1;
    public const string ManifestEntry = "manifest.json";
    public const string DocumentEntry = "document.json";
    public const string SurfaceDirectory = "surfaces/";

    public static string GetSurfaceEntry(string resourceId) => $"{SurfaceDirectory}{resourceId}.mlpx";
}

public sealed record PixelProjectLoadLimits(
    int MaxEntries = 100_000,
    long MaxEntryBytes = 1_073_741_824,
    long MaxTotalBytes = 4_294_967_296);
