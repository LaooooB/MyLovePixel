using System.Text.Json;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Application;

public sealed partial class UserPaletteStore
{
    private const int MaxFileBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static LibraryState ReadState(string path)
    {
        FileStream stream;
        try { stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
        catch (FileNotFoundException) { return new(); }
        catch (DirectoryNotFoundException) { return new(); }
        using (stream)
        {
            if (stream.Length > MaxFileBytes) throw new InvalidDataException("Palette file is too large.");
            using var json = JsonDocument.Parse(stream);
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("schemaVersion", out var version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var schema))
                throw new InvalidDataException("Unsupported personal palette format.");
            var state = new LibraryState { SchemaVersion = schema };
            if (schema == 1)
            {
                var data = json.RootElement.Deserialize<LegacyData>(JsonOptions);
                if (data?.Colors is null || data.Colors.Length > MaxColors)
                    throw new InvalidDataException("Invalid legacy palette or too many colors.");
                var seen = new HashSet<Rgba32>();
                foreach (var hex in data.Colors)
                {
                    var color = ParseColor(hex);
                    if (seen.Add(color)) state.Entries.Add(new(color, "", null));
                }
                return state;
            }
            if (schema != 2) throw new InvalidDataException("Unsupported personal palette format.");
            var library = json.RootElement.Deserialize<LibraryData>(JsonOptions);
            if (library?.Entries is null || library.Folders is null || library.TemporaryColors is null ||
                library.Entries.Length > MaxColors || library.Folders.Length > MaxFolders || library.TemporaryColors.Length > MaxTemporaryColors)
                throw new InvalidDataException("Incomplete color library or capacity exceeded.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in library.Folders)
            {
                if (folder is null || string.IsNullOrWhiteSpace(folder.Id) || folder.Id.Length > 128 || !ids.Add(folder.Id) ||
                    !ValidStoredName(folder.Name, false) || !names.Add(folder.Name!))
                    throw new InvalidDataException("Invalid or duplicated color folder.");
                state.Folders.Add(new(folder.Id, folder.Name!));
            }
            var colors = new HashSet<Rgba32>();
            foreach (var entry in library.Entries)
            {
                if (entry is null || !ValidStoredName(entry.Name, true) ||
                    (entry.FolderId is { } id && !ids.Contains(id)))
                    throw new InvalidDataException("Invalid saved-color metadata.");
                var color = ParseColor(entry.Hex);
                if (!colors.Add(color)) throw new InvalidDataException("Duplicated saved color.");
                state.Entries.Add(new(color, entry.Name!, entry.FolderId));
            }
            colors.Clear();
            foreach (var hex in library.TemporaryColors)
            {
                var color = ParseColor(hex);
                if (!colors.Add(color)) throw new InvalidDataException("Duplicated temporary color.");
                state.TemporaryColors.Add(color);
            }
            return state;
        }
    }

    private static bool ValidStoredName(string? name, bool allowEmpty) => name is not null &&
        name.Length <= MaxNameLength && (allowEmpty || !string.IsNullOrWhiteSpace(name)) && !name.Any(char.IsControl);

    private static Rgba32 ParseColor(string? hex) => HexColor.TryParse(hex, out var color)
        ? color : throw new InvalidDataException("The personal palette contains an invalid HEX color.");

    private void Save(LibraryState state)
    {
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, new LibraryData
                {
                    SchemaVersion = 2,
                    Entries = state.Entries.Select(entry => new EntryData { Hex = HexColor.Format(entry.Color), Name = entry.Name, FolderId = entry.FolderId }).ToArray(),
                    Folders = state.Folders.Select(folder => new FolderData { Id = folder.Id, Name = folder.Name }).ToArray(),
                    TemporaryColors = state.TemporaryColors.Select(HexColor.Format).ToArray(),
                }, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            _ = ReadState(temporary); // Validate the complete candidate before publishing it.
            if (File.Exists(FilePath))
            {
                if (state.SchemaVersion == 1 && !File.Exists(FilePath + ".pre-v2.bak"))
                    File.Copy(FilePath, FilePath + ".pre-v2.bak", overwrite: false);
                // Atomic swap plus previous valid generation; never truncate the live file.
                File.Replace(temporary, FilePath, FilePath + ".bak");
            }
            else File.Move(temporary, FilePath);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed class LibraryState
    {
        public int SchemaVersion { get; init; } = 2;
        public List<UserPaletteColor> Entries { get; } = [];
        public List<UserPaletteFolder> Folders { get; } = [];
        public List<Rgba32> TemporaryColors { get; } = [];
    }
    private sealed class LegacyData { public string[]? Colors { get; set; } }
    private sealed class LibraryData
    {
        public int SchemaVersion { get; set; }
        public EntryData[]? Entries { get; set; }
        public FolderData[]? Folders { get; set; }
        public string[]? TemporaryColors { get; set; }
    }
    private sealed class EntryData
    {
        public string? Hex { get; set; }
        public string? Name { get; set; }
        public string? FolderId { get; set; }
    }
    private sealed class FolderData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }
}
