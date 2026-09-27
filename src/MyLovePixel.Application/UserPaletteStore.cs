using System.Text.Json;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Application;

public sealed record SavedColor(Rgba32 Color, string Name);

/// <summary>Application preferences, independent of artwork and document history.</summary>
public sealed class UserPaletteStore
{
    public const int MaxColors = 512;
    public const int MaxNameLength = 64;
    private const int MaxFileBytes = 512 * 1024;
    private IReadOnlyList<SavedColor> _swatches = Array.Empty<SavedColor>();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public UserPaletteStore(string? filePath = null)
    {
        FilePath = Path.GetFullPath(filePath ?? DefaultFilePath);
        Reload();
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyLovePixel", "user-palette.json");
    public string FilePath { get; }
    public string? LoadError { get; private set; }
    public IReadOnlyList<SavedColor> Swatches => _swatches;

    public void Reload()
    {
        try { _swatches = Read().Colors.AsReadOnly(); LoadError = null; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        { LoadError = $"Could not read My palette. {ex.Message}"; }
    }

    public bool Add(Rgba32 color, string? name = null) => Change(color, NormalizeName(name, color), Operation.Add);
    public bool Rename(Rgba32 color, string name) => Change(color, NormalizeName(name, color), Operation.Rename);
    public bool Remove(Rgba32 color) => Change(color, null, Operation.Remove);

    public static string NormalizeName(string? name, Rgba32 color)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length == 0) return HexColor.Format(color);
        if (value.Length > MaxNameLength) throw new ArgumentException($"Use at most {MaxNameLength} characters for the name.");
        if (value.Any(char.IsControl)) throw new ArgumentException("The name must be a single line.");
        return value;
    }

    private bool Change(Rgba32 color, string? name, Operation operation)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var lease = new FileStream(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        // Reload inside the lease so other application windows cannot lose changes.
        var (candidate, legacy) = Read();
        var index = candidate.FindIndex(s => s.Color == color);
        var changed = false;
        switch (operation)
        {
            case Operation.Add when index < 0:
                if (candidate.Count >= MaxColors) throw new InvalidOperationException($"My palette is full ({MaxColors} colors).");
                candidate.Add(new SavedColor(color, name!)); changed = true; break;
            case Operation.Rename when index >= 0 && candidate[index].Name != name:
                candidate[index] = new SavedColor(color, name!); changed = true; break;
            case Operation.Remove when index >= 0:
                candidate.RemoveAt(index); changed = true; break;
        }
        if (changed)
        {
            if (legacy && !File.Exists(FilePath + ".v1.bak")) File.Copy(FilePath, FilePath + ".v1.bak");
            Save(candidate);
        }
        _swatches = candidate.AsReadOnly();
        LoadError = null;
        return changed;
    }

    private (List<SavedColor> Colors, bool Legacy) Read()
    {
        FileStream stream;
        try { stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
        catch (FileNotFoundException) { return ([], false); }
        catch (DirectoryNotFoundException) { return ([], false); }
        using (stream)
        {
            if (stream.Length > MaxFileBytes) throw new InvalidDataException("Palette file is too large.");
            using var json = JsonDocument.Parse(stream);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var schema) || schema is not (1 or 2) ||
                !root.TryGetProperty("colors", out var items) || items.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Unsupported palette format.");
            if (items.GetArrayLength() > MaxColors) throw new InvalidDataException("Too many saved colors.");
            var result = new List<SavedColor>();
            var seen = new HashSet<Rgba32>();
            foreach (var item in items.EnumerateArray())
            {
                string? hex;
                string? name = null;
                if (schema == 1 && item.ValueKind == JsonValueKind.String) hex = item.GetString();
                else if (schema == 2 && item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("hex", out var h) && h.ValueKind == JsonValueKind.String &&
                    item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                { hex = h.GetString(); name = n.GetString(); }
                else throw new InvalidDataException("Invalid saved color entry.");
                if (!HexColor.TryParse(hex, out var color)) throw new InvalidDataException("Invalid HEX color in palette.");
                string normalized;
                try { normalized = NormalizeName(name, color); }
                catch (ArgumentException ex) { throw new InvalidDataException(ex.Message, ex); }
                if (seen.Add(color)) result.Add(new SavedColor(color, normalized));
            }
            return (result, schema == 1);
        }
    }

    private void Save(IReadOnlyList<SavedColor> colors)
    {
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, new
                {
                    schemaVersion = 2,
                    colors = colors.Select(s => new { hex = HexColor.Format(s.Color), name = s.Name }).ToArray(),
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

    private enum Operation { Add, Rename, Remove }
}
