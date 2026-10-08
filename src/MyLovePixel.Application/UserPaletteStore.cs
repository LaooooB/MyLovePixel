using System.Text.Json;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Application;

public sealed record SavedColor(Rgba32 Color, string Name, string? FolderId = null);
public sealed record ColorFolder(string Id, string Name);

/// <summary>User preferences: stable across EXE replacements and independent of artwork.</summary>
public sealed class UserPaletteStore
{
    public const int MaxColors = 8192;
    public const int MaxNameLength = 64;
    public const int MaxFolders = 256;
    public const int MaxQuickColors = 128;
    private const int MaxFileBytes = 8 * 1024 * 1024;
    private IReadOnlyList<SavedColor> _swatches = Array.Empty<SavedColor>();
    private IReadOnlyList<ColorFolder> _folders = Array.Empty<ColorFolder>();
    private IReadOnlyList<Rgba32> _quickColors = Array.Empty<Rgba32>();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public UserPaletteStore(string? filePath = null)
    {
        FilePath = Path.GetFullPath(filePath ?? DefaultFilePath);
        Reload();
    }

    // Keep the exact path used by the shipped named-palette builds.
    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyLovePixel", "user-palette.json");
    public string FilePath { get; }
    public string? LoadError { get; private set; }
    public IReadOnlyList<SavedColor> Swatches => _swatches;
    public IReadOnlyList<ColorFolder> Folders => _folders;
    public IReadOnlyList<Rgba32> QuickColors => _quickColors;

    public void Reload()
    {
        try { Publish(Read()); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        { LoadError = $"Could not read My palette. Your saved file is unchanged. {ex.Message}"; }
    }

    public bool Add(Rgba32 color, string? name = null, string? folderId = null)
    {
        var label = NormalizeName(name, color);
        return Change(data =>
        {
            ValidateFolder(data, folderId);
            if (data.Colors.Any(s => s.Color == color)) return false;
            if (data.Colors.Count >= MaxColors) throw new InvalidOperationException($"My palette is full ({MaxColors} colors).");
            data.Colors.Add(new SavedColor(color, label, folderId));
            return true;
        });
    }

    public bool Rename(Rgba32 color, string name)
    {
        var label = NormalizeName(name, color);
        return Change(data => UpdateEntry(data, color, s => s with { Name = label }));
    }

    public bool Move(Rgba32 color, string? folderId) => Change(data =>
    {
        ValidateFolder(data, folderId);
        return UpdateEntry(data, color, s => s with { FolderId = folderId });
    });

    public bool Update(Rgba32 color, string? name, string? folderId)
    {
        var label = NormalizeName(name, color);
        return Change(data =>
        {
            ValidateFolder(data, folderId);
            return UpdateEntry(data, color, s => s with { Name = label, FolderId = folderId });
        });
    }

    public bool Remove(Rgba32 color) => Change(data => data.Colors.RemoveAll(s => s.Color == color) > 0);

    public string CreateFolder(string name)
    {
        var label = NormalizeFolderName(name);
        var id = Guid.NewGuid().ToString("N");
        Change(data =>
        {
            if (data.Folders.Any(f => string.Equals(f.Name, label, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("A folder with that name already exists.");
            if (data.Folders.Count >= MaxFolders) throw new InvalidOperationException("Too many folders.");
            data.Folders.Add(new ColorFolder(id, label)); return true;
        });
        return id;
    }

    public bool RenameFolder(string id, string name)
    {
        var label = NormalizeFolderName(name);
        return Change(data =>
        {
            var index = data.Folders.FindIndex(f => f.Id == id);
            if (index < 0) throw new InvalidOperationException("This folder no longer exists.");
            if (data.Folders.Any(f => f.Id != id && string.Equals(f.Name, label, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("A folder with that name already exists.");
            if (data.Folders[index].Name == label) return false;
            data.Folders[index] = new ColorFolder(id, label); return true;
        });
    }

    // Deleting a folder NEVER deletes its colors. They return to Unfiled.
    public bool DeleteFolder(string id) => Change(data =>
    {
        if (data.Folders.RemoveAll(f => f.Id == id) == 0) return false;
        for (var i = 0; i < data.Colors.Count; i++)
            if (data.Colors[i].FolderId == id) data.Colors[i] = data.Colors[i] with { FolderId = null };
        return true;
    });

    public bool Keep(Rgba32 color) => Change(data =>
    {
        if (data.Quick.Contains(color)) return false;
        if (data.Quick.Count >= MaxQuickColors) throw new InvalidOperationException($"Quick colors is full ({MaxQuickColors}). Remove a slot first.");
        data.Quick.Add(color); return true;
    });
    public bool RemoveQuick(Rgba32 color) => Change(data => data.Quick.Remove(color));

    public IReadOnlyList<SavedColor> Query(string? text = null, string? folderId = null, bool unfiledOnly = false)
    {
        var words = (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var names = _folders.ToDictionary(f => f.Id, f => f.Name);
        return _swatches.Where(s => (folderId is null || s.FolderId == folderId) && (!unfiledOnly || s.FolderId is null))
            .Where(s => words.All(word => s.Name.Contains(word, StringComparison.OrdinalIgnoreCase) ||
                HexColor.Format(s.Color).Contains(word, StringComparison.OrdinalIgnoreCase) ||
                (s.FolderId is { } id && names.TryGetValue(id, out var name) && name.Contains(word, StringComparison.OrdinalIgnoreCase))))
            .ToArray();
    }

    public static string NormalizeName(string? name, Rgba32 color)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length == 0) return HexColor.Format(color);
        ValidateName(value);
        return value;
    }
    private static string NormalizeFolderName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length == 0) throw new ArgumentException("Enter a folder name.");
        ValidateName(value); return value;
    }
    private static void ValidateName(string value)
    {
        if (value.Length > MaxNameLength) throw new ArgumentException($"Use at most {MaxNameLength} characters.");
        if (value.Any(char.IsControl)) throw new ArgumentException("The name must be a single line.");
    }
    private static void ValidateFolder(PaletteData data, string? id)
    {
        if (id is not null && !data.Folders.Any(f => f.Id == id)) throw new InvalidOperationException("This folder no longer exists.");
    }
    private static bool UpdateEntry(PaletteData data, Rgba32 color, Func<SavedColor, SavedColor> update)
    {
        var index = data.Colors.FindIndex(s => s.Color == color);
        if (index < 0) return false;
        var changed = update(data.Colors[index]);
        if (changed == data.Colors[index]) return false;
        data.Colors[index] = changed; return true;
    }

    private bool Change(Func<PaletteData, bool> edit)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var lease = new FileStream(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var data = Read(); // Merge the latest on-disk data, not a stale window's copy.
        var changed = edit(data);
        if (changed)
        {
            if (File.Exists(FilePath))
            {
                if (data.Schema < 3 && !File.Exists(FilePath + $".v{data.Schema}.bak"))
                    File.Copy(FilePath, FilePath + $".v{data.Schema}.bak");
                File.Copy(FilePath, FilePath + ".bak", overwrite: true);
            }
            Save(data);
        }
        Publish(data);
        return changed;
    }
    private void Publish(PaletteData data)
    {
        _swatches = data.Colors.AsReadOnly(); _folders = data.Folders.AsReadOnly(); _quickColors = data.Quick.AsReadOnly();
        LoadError = null;
    }

    private PaletteData Read()
    {
        FileStream stream;
        try { stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
        catch (FileNotFoundException) { return new PaletteData(); }
        catch (DirectoryNotFoundException) { return new PaletteData(); }
        using (stream)
        {
            if (stream.Length > MaxFileBytes) throw new InvalidDataException("Palette file is too large.");
            using var json = JsonDocument.Parse(stream);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var schema) || schema is not (1 or 2 or 3) ||
                !root.TryGetProperty("colors", out var items) || items.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Unsupported palette format.");
            if (items.GetArrayLength() > MaxColors) throw new InvalidDataException("Too many saved colors.");
            var data = new PaletteData { Schema = schema };
            if (schema == 3)
            {
                if (!root.TryGetProperty("folders", out var folders) || folders.ValueKind != JsonValueKind.Array || folders.GetArrayLength() > MaxFolders)
                    throw new InvalidDataException("Invalid palette folders.");
                var ids = new HashSet<string>();
                foreach (var folder in folders.EnumerateArray())
                {
                    var id = ReadString(folder, "id"); var name = ReadString(folder, "name");
                    if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || !ids.Add(id)) throw new InvalidDataException("Invalid folder identity.");
                    try { name = NormalizeFolderName(name); }
                    catch (ArgumentException ex) { throw new InvalidDataException(ex.Message, ex); }
                    data.Folders.Add(new ColorFolder(id, name));
                }
                if (root.TryGetProperty("quickColors", out var quick))
                {
                    if (quick.ValueKind != JsonValueKind.Array || quick.GetArrayLength() > MaxQuickColors) throw new InvalidDataException("Invalid quick colors.");
                    foreach (var item in quick.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String || !HexColor.TryParse(item.GetString(), out var color))
                            throw new InvalidDataException("Invalid quick color.");
                        if (!data.Quick.Contains(color)) data.Quick.Add(color);
                    }
                }
            }
            var seen = new HashSet<Rgba32>();
            foreach (var item in items.EnumerateArray())
            {
                string? hex; string? name = null; string? folderId = null;
                if (schema == 1 && item.ValueKind == JsonValueKind.String) hex = item.GetString();
                else if (schema >= 2)
                {
                    hex = ReadString(item, "hex"); name = ReadString(item, "name");
                    if (schema == 3 && item.TryGetProperty("folderId", out var folder) && folder.ValueKind != JsonValueKind.Null)
                    {
                        if (folder.ValueKind != JsonValueKind.String) throw new InvalidDataException("Invalid color folder.");
                        folderId = folder.GetString();
                        if (!data.Folders.Any(f => f.Id == folderId)) throw new InvalidDataException("A saved color refers to a missing folder.");
                    }
                }
                else throw new InvalidDataException("Invalid saved color entry.");
                if (!HexColor.TryParse(hex, out var color)) throw new InvalidDataException("Invalid HEX color in palette.");
                string normalized;
                try { normalized = NormalizeName(name, color); }
                catch (ArgumentException ex) { throw new InvalidDataException(ex.Message, ex); }
                if (seen.Add(color)) data.Colors.Add(new SavedColor(color, normalized, folderId));
            }
            return data;
        }
    }
    private static string ReadString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Invalid palette {name}.");
        return value.GetString()!;
    }
    private void Save(PaletteData data)
    {
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, new
                {
                    schemaVersion = 3,
                    colors = data.Colors.Select(s => new { hex = HexColor.Format(s.Color), name = s.Name, folderId = s.FolderId }).ToArray(),
                    folders = data.Folders.Select(f => new { id = f.Id, name = f.Name }).ToArray(),
                    quickColors = data.Quick.Select(HexColor.Format).ToArray(),
                }, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
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
        public int Schema { get; init; } = 3;
        public List<SavedColor> Colors { get; } = [];
        public List<ColorFolder> Folders { get; } = [];
        public List<Rgba32> Quick { get; } = [];
    }
}
