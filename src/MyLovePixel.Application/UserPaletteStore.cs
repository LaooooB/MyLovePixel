using System.Text.Json;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Application;

/// <summary>
/// Personal, application-wide swatches. This is workspace preference data, not
/// document state: editing it never touches a PixelDocument or its undo history.
/// </summary>
public sealed class UserPaletteStore
{
    public const int MaxColors = 512;
    private const int MaxFileBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
    private IReadOnlyList<Rgba32> _colors = Array.Empty<Rgba32>();

    public UserPaletteStore(string? filePath = null)
    {
        FilePath = Path.GetFullPath(filePath ?? DefaultFilePath);
        try
        {
            _colors = ReadColors().AsReadOnly();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            // InvalidDataException is not an IOException; malformed preference
            // data must be handled explicitly, without overwriting the file.
            LoadError = $"Could not load My palette. The original file is unchanged. {error.Message}";
        }
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyLovePixel", "user-palette.json");

    public string FilePath { get; }
    public string? LoadError { get; }
    public IReadOnlyList<Rgba32> Colors => _colors;

    public bool Add(Rgba32 color) => Change(color, remove: false);
    public bool Remove(Rgba32 color) => Change(color, remove: true);

    private bool Change(Rgba32 color, bool remove)
    {
        if (LoadError is not null) throw new InvalidOperationException(LoadError);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        // Re-read while holding an exclusive lease, so separate app windows do
        // not overwrite each other's swatches. A busy file reports an error.
        using var lease = new FileStream(FilePath + ".lock", FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        var candidate = ReadColors();
        var changed = remove ? candidate.Remove(color) : !candidate.Contains(color);
        if (!changed)
        {
            _colors = candidate.AsReadOnly();
            return false;
        }
        if (!remove)
        {
            if (candidate.Count >= MaxColors)
                throw new InvalidOperationException($"My palette is full ({MaxColors} colors). Remove a color first.");
            candidate.Add(color);
        }
        Save(candidate);
        // Publish in-memory changes only after the durable write succeeds.
        _colors = candidate.AsReadOnly();
        return true;
    }

    private List<Rgba32> ReadColors()
    {
        FileStream stream;
        try
        {
            stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch (FileNotFoundException) { return []; }
        catch (DirectoryNotFoundException) { return []; }
        using (stream)
        {
            if (stream.Length > MaxFileBytes) throw new InvalidDataException("Palette file is too large.");
            var data = JsonSerializer.Deserialize<PaletteData>(stream, JsonOptions);
            if (data is null || data.SchemaVersion != 1 || data.Colors is null)
                throw new InvalidDataException("Unsupported personal palette format.");
            if (data.Colors.Length > MaxColors)
                throw new InvalidDataException($"A personal palette can contain at most {MaxColors} colors.");
            var colors = new List<Rgba32>(data.Colors.Length);
            var seen = new HashSet<Rgba32>();
            foreach (var text in data.Colors)
            {
                if (!HexColor.TryParse(text, out var color))
                    throw new InvalidDataException("The personal palette contains an invalid HEX color.");
                if (seen.Add(color)) colors.Add(color);
            }
            return colors;
        }
    }

    private void Save(IReadOnlyList<Rgba32> colors)
    {
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, new PaletteData
                {
                    SchemaVersion = 1,
                    Colors = colors.Select(HexColor.Format).ToArray(),
                }, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            // Same-directory replacement; do not truncate the existing palette first.
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed class PaletteData
    {
        public PaletteData() { }
        public int SchemaVersion { get; set; }
        public string[]? Colors { get; set; }
    }
}
