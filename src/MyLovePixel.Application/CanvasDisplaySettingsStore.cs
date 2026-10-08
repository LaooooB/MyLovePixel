using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyLovePixel.Application;

/// <summary>Stable per-user storage, independent of the executable's location.</summary>
public sealed class CanvasDisplaySettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
    };
    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyLovePixel", "display-settings.json");
    public string FilePath { get; }
    public string? LoadError { get; }
    public CanvasDisplaySettings Current { get; private set; } = new();

    public CanvasDisplaySettingsStore(string? filePath = null)
    {
        FilePath = Path.GetFullPath(filePath ?? DefaultFilePath);
        try
        {
            var data = Read();
            if (data is not null) Current = new(data.BackgroundBrightness);
        }
        catch (Exception error) when (IsStorageError(error))
        {
            LoadError = "Display settings could not be loaded. Existing files are unchanged. " + error.Message;
        }
    }

    public void Save(CanvasDisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (LoadError is not null) throw new InvalidOperationException(LoadError);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var lease = new FileStream(FilePath + ".lock", FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        // Re-read under the lease; never replace corrupt or newer-version data.
        var data = Read() ?? new Data { SchemaVersion = 1 };
        data.BackgroundBrightness = settings.BackgroundBrightness;
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, data, Options);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak", true);
            else File.Move(temporary, FilePath);
            Current = settings;
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private Data? Read()
    {
        FileStream stream;
        try { stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
        catch (FileNotFoundException)
        {
            if (File.Exists(FilePath + ".bak")) throw new InvalidDataException("Settings are missing; a backup is available.");
            return null;
        }
        catch (DirectoryNotFoundException) { return null; }
        using (stream)
        {
            if (stream.Length > 16 * 1024) throw new InvalidDataException("Display settings are too large.");
            var data = JsonSerializer.Deserialize<Data>(stream, Options);
            if (data is null || data.SchemaVersion != 1 || data.BackgroundBrightness is < 0 or > 100)
                throw new InvalidDataException("Unsupported display settings.");
            return data;
        }
    }

    public static bool IsStorageError(Exception error) => error is IOException or InvalidDataException
        or UnauthorizedAccessException or JsonException or InvalidOperationException or NotSupportedException;

    private sealed class Data
    {
        public Data() { }
        public int SchemaVersion { get; set; }
        public int BackgroundBrightness { get; set; } = CanvasDisplaySettings.DefaultBrightness;
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }
}
