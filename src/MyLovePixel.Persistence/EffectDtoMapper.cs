using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Persistence;

internal static class EffectDtoMapper
{
    public static List<EffectInstanceDto> ToDto(
        EffectGraph graph,
        IReadOnlyList<FrameId> frameOrder,
        IEnumerable<EffectInstanceDto>? template)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(frameOrder);
        var frameIndex = frameOrder
            .Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index);
        var templates = IndexBy(template, item => item.Id);
        var result = new List<EffectInstanceDto>();

        foreach (var effectId in graph.EffectOrder)
        {
            var effect = graph.GetEffect(effectId);
            templates.TryGetValue(effectId.ToString(), out var previous);
            var parameterTemplates = IndexBy(previous?.Parameters, item => item.Key);
            var trackTemplates = IndexBy(previous?.Tracks, item => item.Key);
            var item = new EffectInstanceDto
            {
                Id = effect.Id.ToString(),
                TypeId = effect.TypeId,
                Enabled = effect.Enabled,
                ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
            };

            foreach (var pair in effect.Parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                parameterTemplates.TryGetValue(pair.Key, out var previousParameter);
                item.Parameters.Add(new EffectParameterDto
                {
                    Key = pair.Key,
                    Value = ToDto(pair.Value, previousParameter?.Value),
                    ExtensionData = ExtensionData.Clone(previousParameter?.ExtensionData),
                });
            }

            foreach (var pair in effect.ParameterTracks.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                trackTemplates.TryGetValue(pair.Key, out var previousTrack);
                var keyframeTemplates = IndexBy(previousTrack?.Keyframes, keyframe => keyframe.FrameId);
                var track = new EffectTrackDto
                {
                    Key = pair.Key,
                    Id = pair.Value.Id.ToString(),
                    Name = pair.Value.Name,
                    ExtensionData = ExtensionData.Clone(previousTrack?.ExtensionData),
                };
                foreach (var keyframe in pair.Value.Values.OrderBy(value => frameIndex[value.Key]))
                {
                    keyframeTemplates.TryGetValue(keyframe.Key.ToString(), out var previousKeyframe);
                    track.Keyframes.Add(new EffectKeyframeDto
                    {
                        FrameId = keyframe.Key.ToString(),
                        Value = ToDto(keyframe.Value, previousKeyframe?.Value),
                        ExtensionData = ExtensionData.Clone(previousKeyframe?.ExtensionData),
                    });
                }
                item.Tracks.Add(track);
            }

            result.Add(item);
        }

        return result;
    }

    public static EffectGraph FromDto(
        IEnumerable<EffectInstanceDto> items,
        IReadOnlySet<FrameId> frameIds)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(frameIds);
        var graph = new EffectGraph();
        var effectIds = new HashSet<EffectInstanceId>();

        foreach (var item in items)
        {
            var effectId = new EffectInstanceId(ParseGuid(item.Id, "cel.effect.id"));
            if (!effectIds.Add(effectId))
                throw InvalidReference($"Cel contains duplicate effect id '{item.Id}'.");
            if (string.IsNullOrWhiteSpace(item.TypeId))
                throw InvalidJson($"Effect '{item.Id}' has an empty typeId.");

            var effect = new EffectInstance(effectId, item.TypeId, item.Enabled);
            var parameterKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in item.Parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.Key))
                    throw InvalidJson($"Effect '{item.Id}' contains an empty parameter key.");
                if (!parameterKeys.Add(parameter.Key))
                    throw InvalidReference($"Effect '{item.Id}' contains duplicate parameter key '{parameter.Key}'.");
                effect.SetParameter(parameter.Key, FromDto(parameter.Value), out _);
            }

            var trackKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var trackItem in item.Tracks)
            {
                if (string.IsNullOrWhiteSpace(trackItem.Key))
                    throw InvalidJson($"Effect '{item.Id}' contains an empty track key.");
                if (!trackKeys.Add(trackItem.Key))
                    throw InvalidReference($"Effect '{item.Id}' contains duplicate track key '{trackItem.Key}'.");
                var trackId = new AnimationTrackId(ParseGuid(trackItem.Id, "cel.effect.track.id"));
                var track = new AnimationTrack<EffectValue>(trackId, trackItem.Name);
                var keyFrames = new HashSet<FrameId>();
                foreach (var keyframe in trackItem.Keyframes)
                {
                    var frameId = new FrameId(ParseGuid(keyframe.FrameId, "cel.effect.track.keyframe.frameId"));
                    if (!frameIds.Contains(frameId))
                        throw InvalidReference($"Effect '{item.Id}' track '{trackItem.Key}' references missing frame '{keyframe.FrameId}'.");
                    if (!keyFrames.Add(frameId))
                        throw InvalidReference($"Effect '{item.Id}' track '{trackItem.Key}' contains duplicate frame '{keyframe.FrameId}'.");
                    track.Restore(frameId, FromDto(keyframe.Value));
                }
                effect.RestoreParameterTrack(trackItem.Key, track);
            }

            graph.Add(effect);
        }

        return graph;
    }

    private static EffectValueDto ToDto(EffectValue value, EffectValueDto? template)
    {
        ArgumentNullException.ThrowIfNull(value);
        var dto = new EffectValueDto
        {
            Kind = value.Kind switch
            {
                EffectParameterKind.Integer => "integer",
                EffectParameterKind.Number => "number",
                EffectParameterKind.Boolean => "boolean",
                EffectParameterKind.Color => "color",
                EffectParameterKind.Point => "point",
                EffectParameterKind.PaletteReference => "paletteReference",
                EffectParameterKind.Text => "text",
                _ => throw new PixelProjectException(PixelProjectErrorCode.ValidationFailed, $"Unsupported effect value kind '{value.Kind}'."),
            },
            ExtensionData = ExtensionData.Clone(template?.ExtensionData),
        };

        switch (value.Kind)
        {
            case EffectParameterKind.Integer:
                dto.IntegerValue = value.IntegerValue;
                break;
            case EffectParameterKind.Number:
                dto.NumberValue = value.NumberValue;
                break;
            case EffectParameterKind.Boolean:
                dto.BooleanValue = value.BooleanValue;
                break;
            case EffectParameterKind.Color:
                dto.ColorValue = new RgbaDto
                {
                    R = value.ColorValue.R,
                    G = value.ColorValue.G,
                    B = value.ColorValue.B,
                    A = value.ColorValue.A,
                    ExtensionData = ExtensionData.Clone(template?.ColorValue?.ExtensionData),
                };
                break;
            case EffectParameterKind.Point:
                dto.PointValue = new PointDto
                {
                    X = value.PointValue.X,
                    Y = value.PointValue.Y,
                    ExtensionData = ExtensionData.Clone(template?.PointValue?.ExtensionData),
                };
                break;
            case EffectParameterKind.PaletteReference:
                dto.PaletteIdValue = value.PaletteIdValue.ToString();
                break;
            case EffectParameterKind.Text:
                dto.TextValue = value.TextValue;
                break;
            default:
                throw new PixelProjectException(PixelProjectErrorCode.ValidationFailed, $"Unsupported effect value kind '{value.Kind}'.");
        }
        return dto;
    }

    private static EffectValue FromDto(EffectValueDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return dto.Kind switch
        {
            "integer" when dto.IntegerValue is { } value => EffectValue.Integer(value),
            "number" when dto.NumberValue is { } value => EffectValue.Number(value),
            "boolean" when dto.BooleanValue is { } value => EffectValue.Boolean(value),
            "color" when dto.ColorValue is { } value => EffectValue.Color(new Rgba32(value.R, value.G, value.B, value.A)),
            "point" when dto.PointValue is { } value => EffectValue.Point(new IntPoint(value.X, value.Y)),
            "paletteReference" when !string.IsNullOrWhiteSpace(dto.PaletteIdValue) =>
                EffectValue.PaletteReference(new PaletteId(ParseGuid(dto.PaletteIdValue!, "cel.effect.value.paletteId"))),
            "text" when dto.TextValue is not null => EffectValue.Text(dto.TextValue),
            "integer" or "number" or "boolean" or "color" or "point" or "paletteReference" or "text" =>
                throw InvalidJson($"Effect value kind '{dto.Kind}' is missing its value payload."),
            _ => throw InvalidJson($"Unsupported effect value kind '{dto.Kind}'."),
        };
    }

    private static Dictionary<string, T> IndexBy<T>(
        IEnumerable<T>? values,
        Func<T, string> key) where T : class
    {
        if (values is null) return new Dictionary<string, T>(StringComparer.Ordinal);
        return values.ToDictionary(key, StringComparer.Ordinal);
    }

    private static Guid ParseGuid(string value, string field)
    {
        if (!Guid.TryParseExact(value, "N", out var id) || id == Guid.Empty)
            throw InvalidJson($"Field '{field}' must be a non-empty 32-digit Guid.");
        return id;
    }

    private static PixelProjectException InvalidJson(string message) =>
        new(PixelProjectErrorCode.InvalidJson, message, PixelProjectFormat.DocumentEntry);

    private static PixelProjectException InvalidReference(string message) =>
        new(PixelProjectErrorCode.InvalidReference, message, PixelProjectFormat.DocumentEntry);
}
