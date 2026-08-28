using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyLovePixel.Persistence;

internal sealed class EffectInstanceDto
{
    public string Id { get; set; } = string.Empty;
    public string TypeId { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<EffectParameterDto> Parameters { get; set; } = [];
    public List<EffectTrackDto> Tracks { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class EffectParameterDto
{
    public string Key { get; set; } = string.Empty;
    public EffectValueDto Value { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class EffectTrackDto
{
    public string Key { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<EffectKeyframeDto> Keyframes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class EffectKeyframeDto
{
    public string FrameId { get; set; } = string.Empty;
    public EffectValueDto Value { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class EffectValueDto
{
    public string Kind { get; set; } = string.Empty;
    public long? IntegerValue { get; set; }
    public double? NumberValue { get; set; }
    public bool? BooleanValue { get; set; }
    public RgbaDto? ColorValue { get; set; }
    public PointDto? PointValue { get; set; }
    public string? PaletteIdValue { get; set; }
    public string? TextValue { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
