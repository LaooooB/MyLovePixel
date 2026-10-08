using System.Text.Json;
using System.Text.Json.Serialization;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Application;

public sealed record ColorLibraryFolder(Guid Id, string Name);
public sealed record ColorLibraryColor(Guid Id, string Hex, string Name, Guid? FolderId);

/// <summary>
/// Application-wide color preferences. Never changes document state or undo history.
/// The legacy palette remains untouched; its contents are imported exactly once into
/// a separate, versioned file in the same stable application-data directory.
/// </summary>
public sealed class ColorLibraryStore
{
    public const int MaxColors = 16_384;
    public const int MaxFolders = 256;
    public const int MaxTemporaryColors = 512;
    private const int MaxFileBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private IReadOnlyList<ColorLibraryColor> _colors = Array.Empty<ColorLibraryColor>();
    private IReadOnlyList<ColorLibraryFolder> _folders = Array.Empty<ColorLibraryFolder>();
    private IReadOnlyList<string> _temporaryColors = Array.Empty<string>();

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyLovePixel", "color-library.json");

    public ColorLibraryStore(string? filePath = null, string? legacyFilePath = null)
    {
        FilePath = Path.GetFullPath(filePath ?? DefaultFilePath);
        LegacyFilePath = Path.GetFullPath(legacyFilePath ?? UserPaletteStore.DefaultFilePath);
        if (string.Equals(FilePath, LegacyFilePath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The new library must not replace the legacy palette file.", nameof(filePath));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            using var lease = AcquireLease();
            if (File.Exists(FilePath))
            {
                Publish(ReadData(FilePath));
                return;
            }
            if (File.Exists(BackupPath))
                throw new InvalidDataException("The main library is missing, but a backup exists. Use Restore backup; an empty library will not be created.");

            var legacy = new UserPaletteStore(LegacyFilePath);
            if (legacy.LoadError is not null) throw new InvalidDataException(legacy.LoadError);
            var data = new LibraryData
            {
                SchemaVersion = 1,
                Colors = legacy.Colors.Select(color => new ColorLibraryColor(
                    Guid.NewGuid(), HexColor.Format(color), string.Empty, null)).ToList(),
                Folders = [],
                TemporaryColors = [],
            };
            Save(data);
            Publish(data);
        }
        catch (Exception error) when (IsDataError(error))
        {
            LoadError = $"Color library could not be loaded. Existing files have not been replaced. {error.Message}";
        }
    }

    public string FilePath { get; }
    public string LegacyFilePath { get; }
    public string BackupPath => FilePath + ".bak";
    public string? LoadError { get; private set; }
    public IReadOnlyList<ColorLibraryColor> Colors => _colors;
    public IReadOnlyList<ColorLibraryFolder> Folders => _folders;
    public IReadOnlyList<string> TemporaryColors => _temporaryColors;

    public static Rgba32 ParseColor(string hex) => HexColor.TryParse(hex, out var color)
        ? color : throw new InvalidDataException("Invalid color in the library.");

    public IReadOnlyList<ColorLibraryColor> Search(string? query, Guid? folderId = null, bool unfiledOnly = false)
    {
        if (folderId is not null && unfiledOnly) throw new ArgumentException("Choose either a folder or Unfiled.");
        var terms = (query ?? string.Empty).Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var names = _folders.ToDictionary(folder => folder.Id, folder => folder.Name);
        return _colors.Where(color =>
        {
            if (folderId is not null && color.FolderId != folderId) return false;
            if (unfiledOnly && color.FolderId is not null) return false;
            var folderName = color.FolderId is { } id && names.TryGetValue(id, out var name) ? name : "Unfiled";
            var text = color.Hex + " " + color.Name + " " + folderName;
            return terms.All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
        }).ToArray();
    }

    public Guid SaveColor(Rgba32 color, string name = "", Guid? folderId = null) => Change(data =>
    {
        RequireFolder(data, folderId);
        name = CleanName(name, allowEmpty: true, 120);
        var hex = HexColor.Format(color);
        var existing = data.Colors!.FirstOrDefault(item => ParseColor(item.Hex) == color);
        // Saving an existing color must not silently rename it or move folders.
        if (existing is not null) return existing.Id;
        if (data.Colors.Count >= MaxColors)
            throw new InvalidOperationException($"The library has reached {MaxColors} colors. No color was removed.");
        var id = Guid.NewGuid();
        data.Colors.Add(new ColorLibraryColor(id, hex, name, folderId));
        return id;
    });

    public void UpdateColor(Guid id, string name, Guid? folderId) => Change(data =>
    {
        RequireFolder(data, folderId);
        var index = data.Colors!.FindIndex(color => color.Id == id);
        if (index < 0) throw new InvalidOperationException("This color no longer exists. Reload the library.");
        data.Colors[index] = data.Colors[index] with { Name = CleanName(name, true, 120), FolderId = folderId };
        return true;
    });

    public void RemoveColor(Guid id) => Change(data => data.Colors!.RemoveAll(color => color.Id == id) > 0);

    public Guid CreateFolder(string name) => Change(data =>
    {
        name = CleanName(name, false, 80);
        if (data.Folders!.Any(folder => folder.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("A folder with that name already exists.");
        if (data.Folders.Count >= MaxFolders)
            throw new InvalidOperationException($"The library has reached {MaxFolders} folders.");
        var id = Guid.NewGuid();
        data.Folders.Add(new ColorLibraryFolder(id, name));
        return id;
    });

    public void RenameFolder(Guid id, string name) => Change(data =>
    {
        name = CleanName(name, false, 80);
        var index = data.Folders!.FindIndex(folder => folder.Id == id);
        if (index < 0) throw new InvalidOperationException("This folder no longer exists. Reload the library.");
        if (data.Folders.Any(folder => folder.Id != id && folder.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("A folder with that name already exists.");
        data.Folders[index] = data.Folders[index] with { Name = name };
        return true;
    });

    public void DeleteFolder(Guid id) => Change(data =>
    {
        RequireFolder(data, id);
        data.Folders!.RemoveAll(folder => folder.Id == id);
        for (var i = 0; i < data.Colors!.Count; i++)
            if (data.Colors[i].FolderId == id) data.Colors[i] = data.Colors[i] with { FolderId = null };
        return true;
    });

    public bool AddTemporary(Rgba32 color) => Change(data =>
    {
        if (data.TemporaryColors!.Any(hex => ParseColor(hex) == color)) return false;
        if (data.TemporaryColors.Count >= MaxTemporaryColors)
            throw new InvalidOperationException($"The temporary tray has {MaxTemporaryColors} colors. Remove a slot first; nothing was evicted.");
        data.TemporaryColors.Add(HexColor.Format(color));
        return true;
    });

    public void RemoveTemporary(string hex)
    {
        var color = ParseColor(hex);
        Change(data => data.TemporaryColors!.RemoveAll(item => ParseColor(item) == color) > 0);
    }

    public void ClearTemporary() => Change(data =>
    {
        data.TemporaryColors!.Clear();
        return true;
    });

    /// <summary>Requires an explicit UI confirmation. Keeps damaged bytes for manual recovery.</summary>
    public void RestoreBackup()
    {
        if (LoadError is null) throw new InvalidOperationException("Reload the failed library before requesting recovery.");
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var lease = AcquireLease();
        // A different window may have repaired the file while confirmation was
        // open. Never roll a currently valid primary back to an older backup.
        if (File.Exists(FilePath))
        {
            var validPrimary = false;
            try { _ = ReadData(FilePath); validPrimary = true; }
            catch (Exception error) when (error is JsonException or InvalidDataException) { }
            if (validPrimary)
                throw new InvalidOperationException("A valid library is now present. Reload it instead of restoring an older backup.");
        }
        var data = ReadData(BackupPath);
        var temporary = WriteTemporary(data);
        try
        {
            if (File.Exists(FilePath))
            {
                var damaged = FilePath + ".damaged-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
                File.Copy(FilePath, damaged, overwrite: false);
            }
            // Do not overwrite the good backup with the damaged primary.
            File.Move(temporary, FilePath, overwrite: true);
            Publish(data);
            LoadError = null;
        }
        finally { DeleteTemporary(temporary); }
    }

    private T Change<T>(Func<LibraryData, T> change)
    {
        if (LoadError is not null) throw new InvalidOperationException(LoadError);
        using var lease = AcquireLease();
        // Re-read under the lease so separate windows cannot lose updates.
        var data = ReadData(FilePath);
        var result = change(data);
        Validate(data);
        Save(data);
        Publish(data);
        return result;
    }

    private FileStream AcquireLease() => new(FilePath + ".lock", FileMode.OpenOrCreate,
        FileAccess.ReadWrite, FileShare.None);

    private static LibraryData ReadData(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > MaxFileBytes) throw new InvalidDataException("Color library file is too large.");
        var data = JsonSerializer.Deserialize<LibraryData>(stream, JsonOptions)
            ?? throw new InvalidDataException("The color library is empty.");
        Validate(data);
        return data;
    }

    private static void Validate(LibraryData data)
    {
        if (data.SchemaVersion != 1 || data.Colors is null || data.Folders is null || data.TemporaryColors is null)
            throw new InvalidDataException("Unsupported or incomplete color-library format.");
        if (data.Colors.Count > MaxColors || data.Folders.Count > MaxFolders || data.TemporaryColors.Count > MaxTemporaryColors)
            throw new InvalidDataException("Color library limits were exceeded. The original file is unchanged.");
        var ids = new HashSet<Guid>();
        var folderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in data.Folders)
        {
            if (folder is null || folder.Id == Guid.Empty || !ids.Add(folder.Id) ||
                string.IsNullOrWhiteSpace(folder.Name) || folder.Name.Length > 80 || !folderNames.Add(folder.Name))
                throw new InvalidDataException("Invalid or duplicate folder in the color library.");
        }
        var colorIds = new HashSet<Guid>();
        var colors = new HashSet<Rgba32>();
        foreach (var color in data.Colors)
        {
            if (color is null || color.Id == Guid.Empty || !colorIds.Add(color.Id) || color.Name is null || color.Name.Length > 120 ||
                !HexColor.TryParse(color.Hex, out var rgba) || !colors.Add(rgba) ||
                (color.FolderId is { } id && !ids.Contains(id)))
                throw new InvalidDataException("Invalid color, duplicate or missing folder reference in the library.");
        }
        colors.Clear();
        foreach (var hex in data.TemporaryColors)
            if (!HexColor.TryParse(hex, out var color) || !colors.Add(color))
                throw new InvalidDataException("Invalid or duplicate temporary color.");
    }

    private static string CleanName(string? name, bool allowEmpty, int maxLength)
    {
        name = (name ?? string.Empty).Trim();
        if ((!allowEmpty && name.Length == 0) || name.Length > maxLength || name.Any(char.IsControl))
            throw new ArgumentException($"Use {(allowEmpty ? 0 : 1)}–{maxLength} characters without control characters.", nameof(name));
        return name;
    }

    private static void RequireFolder(LibraryData data, Guid? id)
    {
        if (id is not null && !data.Folders!.Any(folder => folder.Id == id))
            throw new InvalidOperationException("That folder no longer exists. Reload the library.");
    }

    private string WriteTemporary(LibraryData data)
    {
        Validate(data);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, data, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            _ = ReadData(temporary);
            return temporary;
        }
        catch
        {
            DeleteTemporary(temporary);
            throw;
        }
    }

    private void Save(LibraryData data)
    {
        var temporary = WriteTemporary(data);
        try
        {
            if (File.Exists(FilePath))
                File.Replace(temporary, FilePath, BackupPath, ignoreMetadataErrors: true);
            else
            {
                using (var source = new FileStream(temporary, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var backup = new FileStream(BackupPath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    source.CopyTo(backup);
                    backup.Flush(flushToDisk: true);
                }
                File.Move(temporary, FilePath);
            }
        }
        finally { DeleteTemporary(temporary); }
    }

    private void Publish(LibraryData data)
    {
        _colors = data.Colors!.ToList().AsReadOnly();
        _folders = data.Folders!.ToList().AsReadOnly();
        _temporaryColors = data.TemporaryColors!.ToList().AsReadOnly();
    }

    private static void DeleteTemporary(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static bool IsDataError(Exception error) => error is IOException or InvalidDataException
        or UnauthorizedAccessException or JsonException or NotSupportedException;

    private sealed class LibraryData
    {
        public LibraryData() { }
        public int SchemaVersion { get; set; }
        public List<ColorLibraryColor>? Colors { get; set; }
        public List<ColorLibraryFolder>? Folders { get; set; }
        public List<string>? TemporaryColors { get; set; }
    }
}
