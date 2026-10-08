using System.Text.Json;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Application;

/// <summary>Local preferences, independent of documents, undo and the installed EXE.</summary>
public sealed partial class UserPaletteStore
{
    public const int MaxColors = 16384;
    public const int MaxTemporaryColors = 128;
    public const int MaxFolders = 512;
    public const int MaxNameLength = 80;
    private IReadOnlyList<UserPaletteColor> _entries = Array.Empty<UserPaletteColor>();
    private IReadOnlyList<UserPaletteFolder> _folders = Array.Empty<UserPaletteFolder>();
    private IReadOnlyList<Rgba32> _colors = Array.Empty<Rgba32>();
    private IReadOnlyList<Rgba32> _temporaryColors = Array.Empty<Rgba32>();

    public UserPaletteStore(string? filePath = null)
    {
        FilePath = Path.GetFullPath(filePath ?? DefaultFilePath);
        try { Publish(ReadState(FilePath)); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            LoadError = $"Could not load My palette. The original file is unchanged. {error.Message}";
        }
    }

    // Keep this path and assembly identity stable across all releases.
    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyLovePixel", "user-palette.json");
    public string FilePath { get; }
    public string? LoadError { get; }
    public IReadOnlyList<Rgba32> Colors => _colors;
    public IReadOnlyList<UserPaletteColor> Entries => _entries;
    public IReadOnlyList<UserPaletteFolder> Folders => _folders;
    public IReadOnlyList<Rgba32> TemporaryColors => _temporaryColors;

    public bool Add(Rgba32 color) => Add(color, null);
    public bool Add(Rgba32 color, string? folderId, string? name = null)
    {
        var label = ValidateName(name ?? "", allowEmpty: true);
        return Change(state =>
        {
            ValidateFolder(state, folderId);
            if (state.Entries.Any(entry => entry.Color == color)) return false;
            if (state.Entries.Count >= MaxColors)
                throw new InvalidOperationException($"My palette is full ({MaxColors:N0} colors).");
            state.Entries.Add(new(color, label, folderId));
            return true;
        });
    }

    public bool Remove(Rgba32 color) => Change(state => state.Entries.RemoveAll(entry => entry.Color == color) > 0);

    public string CreateFolder(string name)
    {
        var label = ValidateName(name);
        var id = Guid.NewGuid().ToString("N");
        Change(state =>
        {
            if (state.Folders.Any(folder => folder.Name.Equals(label, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("A folder with that name already exists.", nameof(name));
            if (state.Folders.Count >= MaxFolders) throw new InvalidOperationException("Too many color folders.");
            state.Folders.Add(new(id, label));
            return true;
        });
        return id;
    }

    public bool RenameFolder(string folderId, string name)
    {
        var label = ValidateName(name);
        return Change(state =>
        {
            ValidateFolder(state, folderId);
            var index = state.Folders.FindIndex(folder => folder.Id == folderId);
            if (index < 0) throw new ArgumentException("Choose a saved folder.", nameof(folderId));
            if (state.Folders.Any(folder => folder.Id != folderId && folder.Name.Equals(label, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("A folder with that name already exists.", nameof(name));
            if (state.Folders[index].Name == label) return false;
            state.Folders[index] = new(folderId, label);
            return true;
        });
    }

    /// <summary>Deleting a folder NEVER deletes its colors; they become unfiled.</summary>
    public bool DeleteFolder(string folderId) => Change(state =>
    {
        if (state.Folders.RemoveAll(folder => folder.Id == folderId) == 0) return false;
        for (var i = 0; i < state.Entries.Count; i++)
            if (state.Entries[i].FolderId == folderId) state.Entries[i] = state.Entries[i] with { FolderId = null };
        return true;
    });

    public bool MoveColor(Rgba32 color, string? folderId) => Change(state =>
    {
        ValidateFolder(state, folderId);
        var index = RequireColor(state, color);
        if (state.Entries[index].FolderId == folderId) return false;
        state.Entries[index] = state.Entries[index] with { FolderId = folderId };
        return true;
    });

    public bool RenameColor(Rgba32 color, string name)
    {
        var label = ValidateName(name, allowEmpty: true);
        return Change(state =>
        {
            var index = RequireColor(state, color);
            if (state.Entries[index].Name == label) return false;
            state.Entries[index] = state.Entries[index] with { Name = label };
            return true;
        });
    }

    public bool UpdateColor(Rgba32 color, string name, string? folderId)
    {
        var label = ValidateName(name, allowEmpty: true);
        return Change(state =>
        {
            ValidateFolder(state, folderId);
            var index = RequireColor(state, color);
            var replacement = new UserPaletteColor(color, label, folderId);
            if (state.Entries[index] == replacement) return false;
            state.Entries[index] = replacement;
            return true;
        });
    }

    /// <summary>Query the current read-only snapshot without disk IO on every keystroke.</summary>
    public IReadOnlyList<UserPaletteColor> Query(string? query, string? folderId = null, bool unfiledOnly = false)
    {
        var tokens = (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var folders = _folders.ToDictionary(folder => folder.Id, folder => folder.Name);
        return _entries.Where(entry =>
        {
            if (unfiledOnly && entry.FolderId is not null) return false;
            if (folderId is not null && entry.FolderId != folderId) return false;
            var folderName = entry.FolderId is { } id && folders.TryGetValue(id, out var name) ? name : "";
            var searchable = $"{HexColor.Format(entry.Color)} {entry.Name} {folderName}";
            return tokens.All(token => searchable.Contains(token, StringComparison.OrdinalIgnoreCase));
        }).ToList().AsReadOnly();
    }

    public bool AddTemporary(Rgba32 color) => Change(state =>
    {
        if (state.TemporaryColors.Contains(color)) return false;
        if (state.TemporaryColors.Count >= MaxTemporaryColors)
            throw new InvalidOperationException($"Temporary colors are full ({MaxTemporaryColors}). Remove or save a color first.");
        state.TemporaryColors.Add(color);
        return true;
    });
    public bool RemoveTemporary(Rgba32 color) => Change(state => state.TemporaryColors.Remove(color));
    public bool ClearTemporary() => Change(state =>
    {
        if (state.TemporaryColors.Count == 0) return false;
        state.TemporaryColors.Clear();
        return true;
    });

    public void Reload()
    {
        if (LoadError is not null) throw new InvalidOperationException(LoadError);
        Publish(ReadState(FilePath));
    }

    private bool Change(Func<LibraryState, bool> mutate)
    {
        if (LoadError is not null) throw new InvalidOperationException(LoadError);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var lease = new FileStream(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        // Never mutate an old window's snapshot. Re-read under the same lease as the write.
        var candidate = ReadState(FilePath);
        var changed = mutate(candidate);
        if (changed) Save(candidate);
        Publish(candidate); // A failed write must not appear saved in memory.
        return changed;
    }

    private void Publish(LibraryState state)
    {
        _entries = state.Entries.AsReadOnly();
        _folders = state.Folders.AsReadOnly();
        _temporaryColors = state.TemporaryColors.AsReadOnly();
        _colors = state.Entries.Select(entry => entry.Color).ToList().AsReadOnly();
    }

    private static string ValidateName(string name, bool allowEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        var value = name.Trim();
        if ((!allowEmpty && value.Length == 0) || value.Length > MaxNameLength || value.Any(char.IsControl))
            throw new ArgumentException($"Use {(allowEmpty ? "0" : "1")}–{MaxNameLength} characters without control characters.", nameof(name));
        return value;
    }

    private static void ValidateFolder(LibraryState state, string? id)
    {
        if (id is not null && !state.Folders.Any(folder => folder.Id == id))
            throw new ArgumentException("That folder no longer exists. Choose another folder.", nameof(id));
    }
    private static int RequireColor(LibraryState state, Rgba32 color)
    {
        var index = state.Entries.FindIndex(entry => entry.Color == color);
        if (index < 0) throw new InvalidOperationException("That saved color no longer exists.");
        return index;
    }
}
