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
    public List<PaletteDto> Palettes { get; set; } = [];
    public List<SurfaceDto> Surfaces { get; set; } = [];
    public AnimationDto Animation { get; set; } = new();

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

internal sealed class PaletteDto
{
    public string Id { get; set; } = string.Empty;
    public byte? TransparentIndex { get; set; }
    public List<RgbaDto> Colors { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class RgbaDto
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public byte A { get; set; }

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
    public string? PaletteId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class AnimationDto
{
    public List<AnimationClipDto> Clips { get; set; } = [];
    public List<AnimationTagDto> Tags { get; set; } = [];
    public List<SpriteSliceDto> Slices { get; set; } = [];
    public TrackDto<PointDto> PivotTrack { get; set; } = new() { Name = "Pivot" };
    public TrackDto<BoxFrameDto> HitboxTrack { get; set; } = new() { Name = "Hitboxes" };
    public TrackDto<BoxFrameDto> HurtboxTrack { get; set; } = new() { Name = "Hurtboxes" };
    public TrackDto<SocketFrameDto> SocketTrack { get; set; } = new() { Name = "Sockets" };
    public TrackDto<EventFrameDto> EventTrack { get; set; } = new() { Name = "Events" };
    public TrackDto<ColorCycleFrameDto> ColorCycleTrack { get; set; } = new() { Name = "Color Cycles" };

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class AnimationClipDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Clip";
    public string StartFrameId { get; set; } = string.Empty;
    public string EndFrameId { get; set; } = string.Empty;
    public string LoopMode { get; set; } = "loop";

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class AnimationTagDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Tag";
    public string StartFrameId { get; set; } = string.Empty;
    public string EndFrameId { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class SpriteSliceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Slice";
    public RectDto Bounds { get; set; } = new();
    public PointDto Pivot { get; set; } = new();
    public NineSliceDto? NineSlice { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class TrackDto<TValue>
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Track";
    public List<TrackKeyframeDto<TValue>> Keyframes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class TrackKeyframeDto<TValue>
{
    public string FrameId { get; set; } = string.Empty;
    public TValue Value { get; set; } = default!;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class PointDto
{
    public int X { get; set; }
    public int Y { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class RectDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class NineSliceDto
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class NamedBoxDto
{
    public string Name { get; set; } = string.Empty;
    public RectDto Bounds { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class BoxFrameDto
{
    public List<NamedBoxDto> Boxes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class SocketPoseDto
{
    public string Name { get; set; } = string.Empty;
    public PointDto Position { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class SocketFrameDto
{
    public List<SocketPoseDto> Sockets { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class AnimationEventDto
{
    public string Name { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class EventFrameDto
{
    public List<AnimationEventDto> Events { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class PaletteCycleDto
{
    public string PaletteId { get; set; } = string.Empty;
    public byte StartIndex { get; set; }
    public byte EndIndex { get; set; }
    public int Offset { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class ColorCycleFrameDto
{
    public List<PaletteCycleDto> Cycles { get; set; } = [];

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
