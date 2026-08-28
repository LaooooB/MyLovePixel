using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Persistence;

internal static class AnimationDtoMapper
{
    public static AnimationDto ToDto(
        AnimationMetadata animation,
        IReadOnlyList<FrameId> frameOrder,
        AnimationDto? template)
    {
        ArgumentNullException.ThrowIfNull(animation);
        ArgumentNullException.ThrowIfNull(frameOrder);

        var clipTemplates = IndexById(template?.Clips, value => value.Id);
        var tagTemplates = IndexById(template?.Tags, value => value.Id);
        var sliceTemplates = IndexById(template?.Slices, value => value.Id);
        var frameIndex = frameOrder
            .Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index);

        var dto = new AnimationDto
        {
            ExtensionData = ExtensionData.Clone(template?.ExtensionData),
            PivotTrack = MapTrack(
                animation.PivotTrack,
                frameIndex,
                template?.PivotTrack,
                (value, previous) => ToPoint(value, previous)),
            HitboxTrack = MapTrack(
                animation.HitboxTrack,
                frameIndex,
                template?.HitboxTrack,
                (value, previous) => ToBoxFrame(value, previous)),
            HurtboxTrack = MapTrack(
                animation.HurtboxTrack,
                frameIndex,
                template?.HurtboxTrack,
                (value, previous) => ToBoxFrame(value, previous)),
            SocketTrack = MapTrack(
                animation.SocketTrack,
                frameIndex,
                template?.SocketTrack,
                (value, previous) => ToSocketFrame(value, previous)),
            EventTrack = MapTrack(
                animation.EventTrack,
                frameIndex,
                template?.EventTrack,
                (value, previous) => ToEventFrame(value, previous)),
        };

        foreach (var clipId in animation.ClipOrder)
        {
            var clip = animation.GetClip(clipId);
            clipTemplates.TryGetValue(clip.Id.ToString(), out var previous);
            dto.Clips.Add(new AnimationClipDto
            {
                Id = clip.Id.ToString(),
                Name = clip.Name,
                StartFrameId = clip.StartFrameId.ToString(),
                EndFrameId = clip.EndFrameId.ToString(),
                LoopMode = clip.LoopMode switch
                {
                    AnimationLoopMode.Once => "once",
                    AnimationLoopMode.Loop => "loop",
                    AnimationLoopMode.PingPong => "pingPong",
                    _ => throw InvalidJson($"Unsupported animation loop mode '{clip.LoopMode}'."),
                },
                ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
            });
        }

        foreach (var tagId in animation.TagOrder)
        {
            var tag = animation.GetTag(tagId);
            tagTemplates.TryGetValue(tag.Id.ToString(), out var previous);
            dto.Tags.Add(new AnimationTagDto
            {
                Id = tag.Id.ToString(),
                Name = tag.Name,
                StartFrameId = tag.StartFrameId.ToString(),
                EndFrameId = tag.EndFrameId.ToString(),
                ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
            });
        }

        foreach (var sliceId in animation.SliceOrder)
        {
            var slice = animation.GetSlice(sliceId);
            sliceTemplates.TryGetValue(slice.Id.ToString(), out var previous);
            dto.Slices.Add(new SpriteSliceDto
            {
                Id = slice.Id.ToString(),
                Name = slice.Name,
                Bounds = ToRect(slice.Bounds, previous?.Bounds),
                Pivot = ToPoint(slice.Pivot, previous?.Pivot),
                NineSlice = slice.NineSlice is { } insets
                    ? new NineSliceDto
                    {
                        Left = insets.Left,
                        Top = insets.Top,
                        Right = insets.Right,
                        Bottom = insets.Bottom,
                        ExtensionData = ExtensionData.Clone(previous?.NineSlice?.ExtensionData),
                    }
                    : null,
                ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
            });
        }

        return dto;
    }

    public static AnimationMetadata CreateMetadata(AnimationDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var animation = new AnimationMetadata(
            new AnimationTrackId(ParseGuid(dto.PivotTrack.Id, "animation.pivotTrack.id")),
            new AnimationTrackId(ParseGuid(dto.HitboxTrack.Id, "animation.hitboxTrack.id")),
            new AnimationTrackId(ParseGuid(dto.HurtboxTrack.Id, "animation.hurtboxTrack.id")),
            new AnimationTrackId(ParseGuid(dto.SocketTrack.Id, "animation.socketTrack.id")),
            new AnimationTrackId(ParseGuid(dto.EventTrack.Id, "animation.eventTrack.id")));
        animation.PivotTrack.Name = dto.PivotTrack.Name;
        animation.HitboxTrack.Name = dto.HitboxTrack.Name;
        animation.HurtboxTrack.Name = dto.HurtboxTrack.Name;
        animation.SocketTrack.Name = dto.SocketTrack.Name;
        animation.EventTrack.Name = dto.EventTrack.Name;
        return animation;
    }

    public static void Populate(
        AnimationMetadata animation,
        AnimationDto dto,
        IReadOnlySet<FrameId> frameIds)
    {
        ArgumentNullException.ThrowIfNull(animation);
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(frameIds);

        var clipIds = new HashSet<AnimationClipId>();
        foreach (var item in dto.Clips)
        {
            var id = new AnimationClipId(ParseGuid(item.Id, "animation.clip.id"));
            if (!clipIds.Add(id)) throw InvalidReference($"Duplicate animation clip id '{item.Id}'.");
            var start = ParseFrame(item.StartFrameId, "animation.clip.startFrameId", frameIds);
            var end = ParseFrame(item.EndFrameId, "animation.clip.endFrameId", frameIds);
            var loopMode = item.LoopMode switch
            {
                "once" => AnimationLoopMode.Once,
                "loop" => AnimationLoopMode.Loop,
                "pingPong" => AnimationLoopMode.PingPong,
                _ => throw InvalidJson($"Unsupported animation loop mode '{item.LoopMode}'."),
            };
            animation.UpsertClip(new AnimationClip(id, item.Name, start, end, loopMode));
        }

        var tagIds = new HashSet<AnimationTagId>();
        foreach (var item in dto.Tags)
        {
            var id = new AnimationTagId(ParseGuid(item.Id, "animation.tag.id"));
            if (!tagIds.Add(id)) throw InvalidReference($"Duplicate animation tag id '{item.Id}'.");
            animation.UpsertTag(new AnimationTag(
                id,
                item.Name,
                ParseFrame(item.StartFrameId, "animation.tag.startFrameId", frameIds),
                ParseFrame(item.EndFrameId, "animation.tag.endFrameId", frameIds)));
        }

        var sliceIds = new HashSet<SliceId>();
        foreach (var item in dto.Slices)
        {
            var id = new SliceId(ParseGuid(item.Id, "animation.slice.id"));
            if (!sliceIds.Add(id)) throw InvalidReference($"Duplicate sprite slice id '{item.Id}'.");
            var nineSlice = item.NineSlice is null
                ? null
                : new NineSliceInsets(
                    item.NineSlice.Left,
                    item.NineSlice.Top,
                    item.NineSlice.Right,
                    item.NineSlice.Bottom);
            animation.UpsertSlice(new SpriteSlice(
                id,
                item.Name,
                FromRect(item.Bounds),
                FromPoint(item.Pivot),
                nineSlice));
        }

        PopulateTrack(animation.PivotTrack, dto.PivotTrack, frameIds, FromPoint);
        PopulateTrack(animation.HitboxTrack, dto.HitboxTrack, frameIds, FromBoxFrame);
        PopulateTrack(animation.HurtboxTrack, dto.HurtboxTrack, frameIds, FromBoxFrame);
        PopulateTrack(animation.SocketTrack, dto.SocketTrack, frameIds, FromSocketFrame);
        PopulateTrack(animation.EventTrack, dto.EventTrack, frameIds, FromEventFrame);
    }

    private static TrackDto<TDto> MapTrack<TValue, TDto>(
        AnimationTrack<TValue> track,
        IReadOnlyDictionary<FrameId, int> frameIndex,
        TrackDto<TDto>? template,
        Func<TValue, TDto?, TDto> convert)
    {
        var keyframeTemplates = IndexById(template?.Keyframes, value => value.FrameId);
        var dto = new TrackDto<TDto>
        {
            Id = track.Id.ToString(),
            Name = track.Name,
            ExtensionData = ExtensionData.Clone(template?.ExtensionData),
        };

        foreach (var pair in track.Values.OrderBy(pair => frameIndex[pair.Key]))
        {
            keyframeTemplates.TryGetValue(pair.Key.ToString(), out var previous);
            dto.Keyframes.Add(new TrackKeyframeDto<TDto>
            {
                FrameId = pair.Key.ToString(),
                Value = convert(pair.Value, previous is null ? default : previous.Value),
                ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
            });
        }
        return dto;
    }

    private static void PopulateTrack<TValue, TDto>(
        AnimationTrack<TValue> track,
        TrackDto<TDto> dto,
        IReadOnlySet<FrameId> frameIds,
        Func<TDto, TValue> convert)
    {
        var seen = new HashSet<FrameId>();
        foreach (var keyframe in dto.Keyframes)
        {
            var frameId = ParseFrame(keyframe.FrameId, $"animation.{track.Name}.frameId", frameIds);
            if (!seen.Add(frameId))
                throw InvalidReference($"Animation track '{track.Name}' contains duplicate keyframes for frame '{frameId}'.");
            track.Set(frameId, convert(keyframe.Value));
        }
    }

    private static PointDto ToPoint(IntPoint value, PointDto? previous) => new()
    {
        X = value.X,
        Y = value.Y,
        ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
    };

    private static IntPoint FromPoint(PointDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new IntPoint(value.X, value.Y);
    }

    private static RectDto ToRect(IntRect value, RectDto? previous) => new()
    {
        X = value.X,
        Y = value.Y,
        Width = value.Width,
        Height = value.Height,
        ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
    };

    private static IntRect FromRect(RectDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new IntRect(value.X, value.Y, value.Width, value.Height);
    }

    private static BoxFrameDto ToBoxFrame(BoxFrameValue value, BoxFrameDto? previous)
    {
        var dto = new BoxFrameDto
        {
            ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
        };
        for (var index = 0; index < value.Boxes.Count; index++)
        {
            var box = value.Boxes[index];
            var previousBox = previous is not null && index < previous.Boxes.Count ? previous.Boxes[index] : null;
            dto.Boxes.Add(new NamedBoxDto
            {
                Name = box.Name,
                Bounds = ToRect(box.Bounds, previousBox?.Bounds),
                ExtensionData = ExtensionData.Clone(previousBox?.ExtensionData),
            });
        }
        return dto;
    }

    private static BoxFrameValue FromBoxFrame(BoxFrameDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new BoxFrameValue(value.Boxes.Select(box => new NamedBox(box.Name, FromRect(box.Bounds))));
    }

    private static SocketFrameDto ToSocketFrame(SocketFrameValue value, SocketFrameDto? previous)
    {
        var previousByName = IndexById(previous?.Sockets, socket => socket.Name);
        var dto = new SocketFrameDto
        {
            ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
        };
        foreach (var socket in value.Sockets)
        {
            previousByName.TryGetValue(socket.Name, out var previousSocket);
            dto.Sockets.Add(new SocketPoseDto
            {
                Name = socket.Name,
                Position = ToPoint(socket.Position, previousSocket?.Position),
                ExtensionData = ExtensionData.Clone(previousSocket?.ExtensionData),
            });
        }
        return dto;
    }

    private static SocketFrameValue FromSocketFrame(SocketFrameDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new SocketFrameValue(value.Sockets.Select(socket => new SocketPose(socket.Name, FromPoint(socket.Position))));
    }

    private static EventFrameDto ToEventFrame(EventFrameValue value, EventFrameDto? previous)
    {
        var dto = new EventFrameDto
        {
            ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
        };
        for (var index = 0; index < value.Events.Count; index++)
        {
            var marker = value.Events[index];
            var previousEvent = previous is not null && index < previous.Events.Count ? previous.Events[index] : null;
            dto.Events.Add(new AnimationEventDto
            {
                Name = marker.Name,
                Payload = marker.Payload,
                ExtensionData = ExtensionData.Clone(previousEvent?.ExtensionData),
            });
        }
        return dto;
    }

    private static EventFrameValue FromEventFrame(EventFrameDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EventFrameValue(value.Events.Select(marker => new AnimationEventMarker(marker.Name, marker.Payload)));
    }

    private static Dictionary<string, T> IndexById<T>(IEnumerable<T>? values, Func<T, string> getId) where T : class
    {
        if (values is null) return new Dictionary<string, T>(StringComparer.Ordinal);
        return values.ToDictionary(getId, StringComparer.Ordinal);
    }

    private static FrameId ParseFrame(string value, string field, IReadOnlySet<FrameId> knownFrames)
    {
        var id = new FrameId(ParseGuid(value, field));
        if (!knownFrames.Contains(id)) throw InvalidReference($"Field '{field}' references missing frame '{value}'.");
        return id;
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
