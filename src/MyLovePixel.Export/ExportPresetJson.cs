using System.Text.Json;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Export;

public static class ExportPresetJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static byte[] Serialize(ExportPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        preset.Validate();
        return JsonSerializer.SerializeToUtf8Bytes(ToDto(preset), Options);
    }

    public static ExportPreset Deserialize(ReadOnlySpan<byte> json)
    {
        var dto = JsonSerializer.Deserialize<PresetDto>(json, Options)
            ?? throw new InvalidDataException("Export preset JSON is empty.");
        var preset = FromDto(dto);
        preset.Validate();
        return preset;
    }

    private static PresetDto ToDto(ExportPreset preset) => new()
    {
        Name = preset.Name,
        ExporterId = preset.ExporterId,
        Layout = preset.Layout.ToString(),
        Selection = new SelectionDto
        {
            Mode = preset.Selection.Mode.ToString(),
            ClipId = preset.Selection.ClipId?.ToString(),
            TagId = preset.Selection.TagId?.ToString(),
            FrameIds = preset.Selection.FrameIds.Select(id => id.ToString()).ToArray(),
        },
        Crop = preset.Crop is { } crop ? new RectDto(crop.X, crop.Y, crop.Width, crop.Height) : null,
        Trim = preset.Trim,
        Scale = preset.Scale,
        Padding = preset.Padding,
        Extrude = preset.Extrude,
        SpriteSheetColumns = preset.SpriteSheetColumns,
        MaxAtlasWidth = preset.MaxAtlasWidth,
        MaxAtlasHeight = preset.MaxAtlasHeight,
        PowerOfTwoAtlas = preset.PowerOfTwoAtlas,
        AtlasPackerId = preset.AtlasPackerId,
        ImageBaseName = preset.ImageBaseName,
        MetadataFileName = preset.MetadataFileName,
    };

    private static ExportPreset FromDto(PresetDto dto)
    {
        if (!Enum.TryParse<ExportLayout>(dto.Layout, true, out var layout))
            throw new InvalidDataException($"Unknown export layout '{dto.Layout}'.");
        if (!Enum.TryParse<ExportFrameSelectionMode>(dto.Selection.Mode, true, out var selectionMode))
            throw new InvalidDataException($"Unknown frame selection mode '{dto.Selection.Mode}'.");
        var selection = selectionMode switch
        {
            ExportFrameSelectionMode.All => ExportFrameSelection.All,
            ExportFrameSelectionMode.Clip => ExportFrameSelection.ForClip(new AnimationClipId(ParseGuid(dto.Selection.ClipId, "selection.clipId"))),
            ExportFrameSelectionMode.Tag => ExportFrameSelection.ForTag(new AnimationTagId(ParseGuid(dto.Selection.TagId, "selection.tagId"))),
            ExportFrameSelectionMode.Explicit => ExportFrameSelection.Explicit(dto.Selection.FrameIds.Select(value => new FrameId(ParseGuid(value, "selection.frameIds")))),
            _ => throw new InvalidDataException("Unsupported frame selection mode."),
        };
        return new ExportPreset
        {
            Name = dto.Name,
            ExporterId = dto.ExporterId,
            Layout = layout,
            Selection = selection,
            Crop = dto.Crop is null ? null : new IntRect(dto.Crop.X, dto.Crop.Y, dto.Crop.Width, dto.Crop.Height),
            Trim = dto.Trim,
            Scale = dto.Scale,
            Padding = dto.Padding,
            Extrude = dto.Extrude,
            SpriteSheetColumns = dto.SpriteSheetColumns,
            MaxAtlasWidth = dto.MaxAtlasWidth,
            MaxAtlasHeight = dto.MaxAtlasHeight,
            PowerOfTwoAtlas = dto.PowerOfTwoAtlas,
            AtlasPackerId = dto.AtlasPackerId,
            ImageBaseName = dto.ImageBaseName,
            MetadataFileName = dto.MetadataFileName,
        };
    }

    private static Guid ParseGuid(string? value, string field)
    {
        if (value is null || !Guid.TryParseExact(value, "N", out var id) || id == Guid.Empty)
            throw new InvalidDataException($"'{field}' must be a non-empty N-format Guid.");
        return id;
    }

    private sealed class PresetDto
    {
        public string Name { get; set; } = "Default";
        public string ExporterId { get; set; } = BuiltinExporterIds.GameAssets;
        public string Layout { get; set; } = nameof(ExportLayout.SpriteSheet);
        public SelectionDto Selection { get; set; } = new();
        public RectDto? Crop { get; set; }
        public bool Trim { get; set; } = true;
        public int Scale { get; set; } = 1;
        public int Padding { get; set; }
        public int Extrude { get; set; }
        public int SpriteSheetColumns { get; set; }
        public int MaxAtlasWidth { get; set; } = 2048;
        public int MaxAtlasHeight { get; set; } = 2048;
        public bool PowerOfTwoAtlas { get; set; }
        public string AtlasPackerId { get; set; } = BuiltinAtlasPackerIds.DeterministicShelf;
        public string ImageBaseName { get; set; } = "sprite";
        public string MetadataFileName { get; set; } = "sprite.json";
    }

    private sealed class SelectionDto
    {
        public string Mode { get; set; } = nameof(ExportFrameSelectionMode.All);
        public string? ClipId { get; set; }
        public string? TagId { get; set; }
        public string[] FrameIds { get; set; } = [];
    }

    private sealed record RectDto(int X, int Y, int Width, int Height);
}
