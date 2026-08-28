using System.Collections.ObjectModel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Export;

public enum ExportLayout
{
    SeparateFrames = 1,
    SpriteSheet = 2,
    Atlas = 3,
}

public enum ExportFrameSelectionMode
{
    All = 1,
    Clip = 2,
    Tag = 3,
    Explicit = 4,
}

public sealed record ExportFrameSelection
{
    private ExportFrameSelection(
        ExportFrameSelectionMode mode,
        AnimationClipId? clipId = null,
        AnimationTagId? tagId = null,
        IReadOnlyList<FrameId>? frameIds = null)
    {
        Mode = mode;
        ClipId = clipId;
        TagId = tagId;
        FrameIds = frameIds ?? Array.Empty<FrameId>();
    }

    public ExportFrameSelectionMode Mode { get; }
    public AnimationClipId? ClipId { get; }
    public AnimationTagId? TagId { get; }
    public IReadOnlyList<FrameId> FrameIds { get; }

    public static ExportFrameSelection All { get; } = new(ExportFrameSelectionMode.All);
    public static ExportFrameSelection ForClip(AnimationClipId id) => new(ExportFrameSelectionMode.Clip, clipId: id);
    public static ExportFrameSelection ForTag(AnimationTagId id) => new(ExportFrameSelectionMode.Tag, tagId: id);
    public static ExportFrameSelection Explicit(IEnumerable<FrameId> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var values = ids.Distinct().ToArray();
        if (values.Length == 0) throw new ArgumentException("Explicit frame selection cannot be empty.", nameof(ids));
        if (values.Any(id => id.Value == Guid.Empty)) throw new ArgumentException("FrameId cannot be empty.", nameof(ids));
        return new ExportFrameSelection(ExportFrameSelectionMode.Explicit, frameIds: Array.AsReadOnly(values));
    }
}

public sealed record ExportPreset
{
    public string Name { get; init; } = "Default";
    public string ExporterId { get; init; } = BuiltinExporterIds.GameAssets;
    public ExportLayout Layout { get; init; } = ExportLayout.SpriteSheet;
    public ExportFrameSelection Selection { get; init; } = ExportFrameSelection.All;
    public IntRect? Crop { get; init; }
    public bool Trim { get; init; } = true;
    public int Scale { get; init; } = 1;
    public int Padding { get; init; }
    public int Extrude { get; init; }
    public int SpriteSheetColumns { get; init; }
    public int MaxAtlasWidth { get; init; } = 2048;
    public int MaxAtlasHeight { get; init; } = 2048;
    public bool PowerOfTwoAtlas { get; init; }
    public string AtlasPackerId { get; init; } = BuiltinAtlasPackerIds.DeterministicShelf;
    public string ImageBaseName { get; init; } = "sprite";
    public string MetadataFileName { get; init; } = "sprite.json";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("Preset name cannot be empty.", nameof(Name));
        if (string.IsNullOrWhiteSpace(ExporterId)) throw new ArgumentException("ExporterId cannot be empty.", nameof(ExporterId));
        if (Scale is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(Scale));
        if (Padding is < 0 or > 4096) throw new ArgumentOutOfRangeException(nameof(Padding));
        if (Extrude is < 0 or > 4096) throw new ArgumentOutOfRangeException(nameof(Extrude));
        if (SpriteSheetColumns < 0) throw new ArgumentOutOfRangeException(nameof(SpriteSheetColumns));
        if (MaxAtlasWidth <= 0) throw new ArgumentOutOfRangeException(nameof(MaxAtlasWidth));
        if (MaxAtlasHeight <= 0) throw new ArgumentOutOfRangeException(nameof(MaxAtlasHeight));
        if (string.IsNullOrWhiteSpace(AtlasPackerId)) throw new ArgumentException("AtlasPackerId cannot be empty.", nameof(AtlasPackerId));
        ExportPath.ValidateFileStem(ImageBaseName, nameof(ImageBaseName));
        ExportPath.ValidateRelativeFile(MetadataFileName, nameof(MetadataFileName));
        if (Crop is { IsEmpty: true }) throw new ArgumentException("Crop cannot be empty.", nameof(Crop));
    }
}

public sealed class ExportRequest
{
    public ExportRequest(DocumentSnapshot snapshot, ExportPreset preset)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Preset = preset ?? throw new ArgumentNullException(nameof(preset));
        try
        {
            preset.Validate();
        }
        catch (ArgumentException ex)
        {
            throw new AssetPipelineException(AssetPipelineErrorCode.InvalidRequest, "Export preset is invalid.", ex);
        }

        if (preset.Selection.Mode == ExportFrameSelectionMode.Explicit)
        {
            var available = snapshot.FrameOrder.ToHashSet();
            var missing = preset.Selection.FrameIds.Where(id => !available.Contains(id)).ToArray();
            if (missing.Length > 0)
                throw new AssetPipelineException(
                    AssetPipelineErrorCode.InvalidRequest,
                    $"Explicit export selection references missing frame(s): {string.Join(", ", missing)}.");
        }
    }

    public DocumentSnapshot Snapshot { get; }
    public ExportPreset Preset { get; }
}

public sealed record ExportArtifact
{
    public ExportArtifact(string relativePath, string mediaType, ReadOnlyMemory<byte> content)
    {
        ExportPath.ValidateRelativeFile(relativePath, nameof(relativePath));
        if (string.IsNullOrWhiteSpace(mediaType)) throw new ArgumentException("Media type cannot be empty.", nameof(mediaType));
        RelativePath = relativePath.Replace('\\', '/');
        MediaType = mediaType;
        Content = content.ToArray();
    }

    public string RelativePath { get; }
    public string MediaType { get; }
    public ReadOnlyMemory<byte> Content { get; }
}

public sealed class ExportBundle
{
    private readonly ExportArtifact[] _artifacts;

    public ExportBundle(IEnumerable<ExportArtifact> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        _artifacts = artifacts.ToArray();
        if (_artifacts.Length == 0) throw new ArgumentException("Export bundle must contain at least one artifact.", nameof(artifacts));
        if (_artifacts.Select(item => item.RelativePath).Distinct(StringComparer.Ordinal).Count() != _artifacts.Length)
            throw new ArgumentException("Export artifact paths must be unique.", nameof(artifacts));
    }

    public IReadOnlyList<ExportArtifact> Artifacts => Array.AsReadOnly(_artifacts);
}

public interface IExporter
{
    string Id { get; }
    ExportBundle Export(ExportRequest request);
}

public sealed class ExportPipeline
{
    private readonly Dictionary<string, IExporter> _exporters = new(StringComparer.Ordinal);

    public ExportPipeline(IEnumerable<IExporter>? exporters = null)
    {
        foreach (var exporter in exporters ?? Array.Empty<IExporter>()) Register(exporter);
    }

    public IReadOnlyCollection<string> ExporterIds => new ReadOnlyCollection<string>(_exporters.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray());

    public void Register(IExporter exporter)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        if (string.IsNullOrWhiteSpace(exporter.Id)) throw new ArgumentException("Exporter Id cannot be empty.", nameof(exporter));
        if (!_exporters.TryAdd(exporter.Id, exporter)) throw new InvalidOperationException($"Exporter '{exporter.Id}' is already registered.");
    }

    public ExportBundle Execute(ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_exporters.TryGetValue(request.Preset.ExporterId, out var exporter))
            throw new AssetPipelineException(
                AssetPipelineErrorCode.ExporterNotFound,
                $"Exporter '{request.Preset.ExporterId}' is not registered.");

        try
        {
            return exporter.Export(request);
        }
        catch (AssetPipelineException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or NotSupportedException)
        {
            throw new AssetPipelineException(AssetPipelineErrorCode.InvalidRequest, "Export request cannot be resolved.", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new AssetPipelineException(AssetPipelineErrorCode.ExportFailed, "Export pipeline could not produce the requested artifacts.", ex);
        }
    }

    public static ExportPipeline CreateDefault() => new([new GameAssetExporter()]);
}

public sealed class ImportRequest
{
    public ImportRequest(string name, ReadOnlyMemory<byte> content)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Import name cannot be empty.", nameof(name));
        if (content.IsEmpty) throw new ArgumentException("Import content cannot be empty.", nameof(content));
        Name = name;
        Content = content.ToArray();
    }

    public string Name { get; }
    public ReadOnlyMemory<byte> Content { get; }
}

public interface IImporter
{
    string Id { get; }
    bool CanImport(ImportRequest request);
    PixelDocument Import(ImportRequest request);
}

public sealed class ImportPipeline
{
    private readonly Dictionary<string, IImporter> _importers = new(StringComparer.Ordinal);

    public ImportPipeline(IEnumerable<IImporter>? importers = null)
    {
        foreach (var importer in importers ?? Array.Empty<IImporter>()) Register(importer);
    }

    public void Register(IImporter importer)
    {
        ArgumentNullException.ThrowIfNull(importer);
        if (string.IsNullOrWhiteSpace(importer.Id)) throw new ArgumentException("Importer Id cannot be empty.", nameof(importer));
        if (!_importers.TryAdd(importer.Id, importer)) throw new InvalidOperationException($"Importer '{importer.Id}' is already registered.");
    }

    public PixelDocument Execute(string importerId, ImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_importers.TryGetValue(importerId, out var importer))
            throw new AssetPipelineException(AssetPipelineErrorCode.ImporterNotFound, $"Importer '{importerId}' is not registered.");
        if (!importer.CanImport(request))
            throw new AssetPipelineException(AssetPipelineErrorCode.UnsupportedInput, $"Importer '{importerId}' cannot import '{request.Name}'.");
        try
        {
            return importer.Import(request);
        }
        catch (AssetPipelineException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            throw new AssetPipelineException(AssetPipelineErrorCode.ImportFailed, $"Import of '{request.Name}' failed.", ex);
        }
    }

    public static ImportPipeline CreateDefault() => new([new PngImporter()]);
}

public static class BuiltinExporterIds
{
    public const string GameAssets = "builtin.game-assets";
}

public static class BuiltinImporterIds
{
    public const string Png = "builtin.png";
}

internal static class ExportPath
{
    public static void ValidateFileStem(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['/', '\\']) >= 0 || value is "." or "..")
            throw new ArgumentException("File stem must be a single safe path segment.", paramName);
    }

    public static void ValidateRelativeFile(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Relative file path cannot be empty.", paramName);
        var normalized = value.Replace('\\', '/');
        if (normalized[0] == '/') throw new ArgumentException("Path must be relative.", paramName);
        var segments = normalized.Split('/');
        if (segments.Any(segment => segment is "" or "." or "..")) throw new ArgumentException("Path contains an unsafe segment.", paramName);
    }
}
