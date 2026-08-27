using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyLovePixel.Persistence;

internal sealed class ManifestDto
{
    public string Format { get; set; } = PixelProjectFormat.FormatMarker;
    public int SchemaVersion { get; set; } = PixelProjectFormat.CurrentSchemaVersion;
    public string DocumentEntry { get; set; } = PixelProjectFormat.DocumentEntry;
    public string ContentHash { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class DocumentDto
{
    public string Id { get; set; } = string.Empty;
    public CanvasDto Canvas { get; set; } = new();
    public List<LayerDto> Layers { get; set; } = [];
    public List<FrameDto> Frames { get; set; } = [];
    public List<CelDto> Cels { get; set; } = [];
    public List<SurfaceDto> Surfaces { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class CanvasDto
{
    public int Width { get; set; }
    public int Height { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class LayerDto
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = "pixel";
    public string Name { get; set; } = "Layer";
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public byte Opacity { get; set; } = byte.MaxValue;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class FrameDto
{
    public string Id { get; set; } = string.Empty;
    public long DurationTicks { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class CelDto
{
    public string Id { get; set; } = string.Empty;
    public string LayerId { get; set; } = string.Empty;
    public string FrameId { get; set; } = string.Empty;
    public string SurfaceId { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public byte Opacity { get; set; } = byte.MaxValue;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class SurfaceDto
{
    public string Id { get; set; } = string.Empty;
    public string Entry { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; } = "rgba32";

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal static class ExtensionData
{
    public static Dictionary<string, JsonElement>? Clone(Dictionary<string, JsonElement>? source)
    {
        if (source is null || source.Count == 0) return null;
        return source.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
    }
}
