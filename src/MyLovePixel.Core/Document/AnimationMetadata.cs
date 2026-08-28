using System.Collections.ObjectModel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Document;

public enum AnimationLoopMode
{
    Once = 0,
    Loop = 1,
    PingPong = 2,
}

public sealed record AnimationClip
{
    public AnimationClip(
        AnimationClipId id,
        string name,
        FrameId startFrameId,
        FrameId endFrameId,
        AnimationLoopMode loopMode = AnimationLoopMode.Loop)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("AnimationClipId cannot be empty.", nameof(id));
        if (startFrameId.Value == Guid.Empty) throw new ArgumentException("Start FrameId cannot be empty.", nameof(startFrameId));
        if (endFrameId.Value == Guid.Empty) throw new ArgumentException("End FrameId cannot be empty.", nameof(endFrameId));
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? "Clip" : name;
        StartFrameId = startFrameId;
        EndFrameId = endFrameId;
        LoopMode = loopMode;
    }

    public AnimationClipId Id { get; }
    public string Name { get; }
    public FrameId StartFrameId { get; }
    public FrameId EndFrameId { get; }
    public AnimationLoopMode LoopMode { get; }
}

public sealed record AnimationTag
{
    public AnimationTag(AnimationTagId id, string name, FrameId startFrameId, FrameId endFrameId)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("AnimationTagId cannot be empty.", nameof(id));
        if (startFrameId.Value == Guid.Empty) throw new ArgumentException("Start FrameId cannot be empty.", nameof(startFrameId));
        if (endFrameId.Value == Guid.Empty) throw new ArgumentException("End FrameId cannot be empty.", nameof(endFrameId));
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? "Tag" : name;
        StartFrameId = startFrameId;
        EndFrameId = endFrameId;
    }

    public AnimationTagId Id { get; }
    public string Name { get; }
    public FrameId StartFrameId { get; }
    public FrameId EndFrameId { get; }
}

public readonly record struct NineSliceInsets
{
    public NineSliceInsets(int left, int top, int right, int bottom)
    {
        if (left < 0) throw new ArgumentOutOfRangeException(nameof(left));
        if (top < 0) throw new ArgumentOutOfRangeException(nameof(top));
        if (right < 0) throw new ArgumentOutOfRangeException(nameof(right));
        if (bottom < 0) throw new ArgumentOutOfRangeException(nameof(bottom));
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public int Left { get; }
    public int Top { get; }
    public int Right { get; }
    public int Bottom { get; }
}

public sealed record SpriteSlice
{
    public SpriteSlice(
        SliceId id,
        string name,
        IntRect bounds,
        IntPoint pivot,
        NineSliceInsets? nineSlice = null)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("SliceId cannot be empty.", nameof(id));
        if (bounds.IsEmpty) throw new ArgumentException("Slice bounds must be non-empty.", nameof(bounds));
        if (nineSlice is { } insets &&
            (checked(insets.Left + insets.Right) > bounds.Width ||
             checked(insets.Top + insets.Bottom) > bounds.Height))
            throw new ArgumentException("Nine-slice insets cannot exceed slice dimensions.", nameof(nineSlice));

        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? "Slice" : name;
        Bounds = bounds;
        Pivot = pivot;
        NineSlice = nineSlice;
    }

    public SliceId Id { get; }
    public string Name { get; }
    public IntRect Bounds { get; }
    public IntPoint Pivot { get; }
    public NineSliceInsets? NineSlice { get; }
}

public sealed record NamedBox
{
    public NamedBox(string name, IntRect bounds)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Box name cannot be empty.", nameof(name));
        if (bounds.IsEmpty) throw new ArgumentException("Box bounds must be non-empty.", nameof(bounds));
        Name = name;
        Bounds = bounds;
    }

    public string Name { get; }
    public IntRect Bounds { get; }
}

public sealed class BoxFrameValue : IEquatable<BoxFrameValue>
{
    private readonly NamedBox[] _boxes;

    public BoxFrameValue(IEnumerable<NamedBox> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        _boxes = boxes.ToArray();
        if (_boxes.Any(box => box is null)) throw new ArgumentException("Box collection cannot contain null values.", nameof(boxes));
    }

    public IReadOnlyList<NamedBox> Boxes => Array.AsReadOnly(_boxes);

    public bool Equals(BoxFrameValue? other) =>
        other is not null && _boxes.AsSpan().SequenceEqual(other._boxes);

    public override bool Equals(object? obj) => obj is BoxFrameValue other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var box in _boxes) hash.Add(box);
        return hash.ToHashCode();
    }
}

public sealed record SocketPose
{
    public SocketPose(string name, IntPoint position)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Socket name cannot be empty.", nameof(name));
        Name = name;
        Position = position;
    }

    public string Name { get; }
    public IntPoint Position { get; }
}

public sealed class SocketFrameValue : IEquatable<SocketFrameValue>
{
    private readonly SocketPose[] _sockets;

    public SocketFrameValue(IEnumerable<SocketPose> sockets)
    {
        ArgumentNullException.ThrowIfNull(sockets);
        _sockets = sockets.ToArray();
        if (_sockets.Any(socket => socket is null)) throw new ArgumentException("Socket collection cannot contain null values.", nameof(sockets));
        if (_sockets.Select(socket => socket.Name).Distinct(StringComparer.Ordinal).Count() != _sockets.Length)
            throw new ArgumentException("Socket names must be unique within a frame.", nameof(sockets));
    }

    public IReadOnlyList<SocketPose> Sockets => Array.AsReadOnly(_sockets);

    public bool Equals(SocketFrameValue? other) =>
        other is not null && _sockets.AsSpan().SequenceEqual(other._sockets);

    public override bool Equals(object? obj) => obj is SocketFrameValue other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var socket in _sockets) hash.Add(socket);
        return hash.ToHashCode();
    }
}

public sealed record AnimationEventMarker
{
    public AnimationEventMarker(string name, string payload = "")
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Animation event name cannot be empty.", nameof(name));
        Name = name;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    public string Name { get; }
    public string Payload { get; }
}

public sealed class EventFrameValue : IEquatable<EventFrameValue>
{
    private readonly AnimationEventMarker[] _events;

    public EventFrameValue(IEnumerable<AnimationEventMarker> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        _events = events.ToArray();
        if (_events.Any(value => value is null)) throw new ArgumentException("Event collection cannot contain null values.", nameof(events));
    }

    public IReadOnlyList<AnimationEventMarker> Events => Array.AsReadOnly(_events);

    public bool Equals(EventFrameValue? other) =>
        other is not null && _events.AsSpan().SequenceEqual(other._events);

    public override bool Equals(object? obj) => obj is EventFrameValue other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in _events) hash.Add(value);
        return hash.ToHashCode();
    }
}

public readonly record struct PaletteCycle
{
    public PaletteCycle(PaletteId paletteId, byte startIndex, byte endIndex, int offset)
    {
        if (paletteId.Value == Guid.Empty) throw new ArgumentException("PaletteId cannot be empty.", nameof(paletteId));
        if (startIndex > endIndex) throw new ArgumentException("Color cycle start index cannot exceed end index.", nameof(startIndex));
        PaletteId = paletteId;
        StartIndex = startIndex;
        EndIndex = endIndex;
        Offset = offset;
    }

    public PaletteId PaletteId { get; }
    public byte StartIndex { get; }
    public byte EndIndex { get; }
    public int Offset { get; }

    public byte RemapIndex(byte index)
    {
        if (index < StartIndex || index > EndIndex) return index;
        var length = EndIndex - StartIndex + 1;
        var position = index - StartIndex;
        var shift = Offset % length;
        var remapped = (position + shift) % length;
        if (remapped < 0) remapped += length;
        return checked((byte)(StartIndex + remapped));
    }
}

public sealed class ColorCycleFrameValue : IEquatable<ColorCycleFrameValue>
{
    private readonly PaletteCycle[] _cycles;

    public ColorCycleFrameValue(IEnumerable<PaletteCycle> cycles)
    {
        ArgumentNullException.ThrowIfNull(cycles);
        _cycles = cycles.ToArray();
        for (var left = 0; left < _cycles.Length; left++)
        for (var right = left + 1; right < _cycles.Length; right++)
        {
            var a = _cycles[left];
            var b = _cycles[right];
            if (a.PaletteId != b.PaletteId) continue;
            if (a.StartIndex <= b.EndIndex && b.StartIndex <= a.EndIndex)
                throw new ArgumentException("Color cycle ranges for the same palette cannot overlap.", nameof(cycles));
        }
    }

    public IReadOnlyList<PaletteCycle> Cycles => Array.AsReadOnly(_cycles);

    public byte ResolveIndex(PaletteId paletteId, byte index)
    {
        foreach (var cycle in _cycles)
        {
            if (cycle.PaletteId == paletteId && index >= cycle.StartIndex && index <= cycle.EndIndex)
                return cycle.RemapIndex(index);
        }
        return index;
    }

    public bool Equals(ColorCycleFrameValue? other) =>
        other is not null && _cycles.AsSpan().SequenceEqual(other._cycles);

    public override bool Equals(object? obj) => obj is ColorCycleFrameValue other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var cycle in _cycles) hash.Add(cycle);
        return hash.ToHashCode();
    }
}

public interface IAnimationEasing
{
    string Id { get; }
    double Map(double progress);
}

public sealed class LinearAnimationEasing : IAnimationEasing
{
    public static LinearAnimationEasing Instance { get; } = new();
    private LinearAnimationEasing() { }
    public string Id => "linear";
    public double Map(double progress) => Math.Clamp(progress, 0d, 1d);
}

public sealed class StepAnimationEasing : IAnimationEasing
{
    public static StepAnimationEasing Instance { get; } = new();
    private StepAnimationEasing() { }
    public string Id => "step";
    public double Map(double progress) => progress >= 1d ? 1d : 0d;
}

public sealed class AnimationTrack<T>
{
    private readonly Dictionary<FrameId, T> _values = [];

    internal AnimationTrack(AnimationTrackId id, string name)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("AnimationTrackId cannot be empty.", nameof(id));
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? "Track" : name;
    }

    public AnimationTrackId Id { get; }
    public string Name { get; internal set; }
    public IReadOnlyDictionary<FrameId, T> Values => new ReadOnlyDictionary<FrameId, T>(_values);

    public bool TryGetValue(FrameId frameId, out T value) => _values.TryGetValue(frameId, out value!);

    internal void Set(FrameId frameId, T value)
    {
        if (frameId.Value == Guid.Empty) throw new ArgumentException("FrameId cannot be empty.", nameof(frameId));
        ArgumentNullException.ThrowIfNull(value);
        _values[frameId] = value;
    }

    internal bool Remove(FrameId frameId, out T value) => _values.Remove(frameId, out value!);

    internal void Restore(FrameId frameId, T value) => _values[frameId] = value;

    internal AnimationTrackSnapshot<T> Snapshot() =>
        new(Id, Name, new ReadOnlyDictionary<FrameId, T>(new Dictionary<FrameId, T>(_values)));
}

public sealed record AnimationTrackSnapshot<T>(
    AnimationTrackId Id,
    string Name,
    IReadOnlyDictionary<FrameId, T> Values);

public sealed class AnimationMetadata
{
    private readonly Dictionary<AnimationClipId, AnimationClip> _clips = [];
    private readonly List<AnimationClipId> _clipOrder = [];
    private readonly Dictionary<AnimationTagId, AnimationTag> _tags = [];
    private readonly List<AnimationTagId> _tagOrder = [];
    private readonly Dictionary<SliceId, SpriteSlice> _slices = [];
    private readonly List<SliceId> _sliceOrder = [];

    public AnimationMetadata()
        : this(
            AnimationTrackId.New(),
            AnimationTrackId.New(),
            AnimationTrackId.New(),
            AnimationTrackId.New(),
            AnimationTrackId.New(),
            AnimationTrackId.New())
    {
    }

    internal AnimationMetadata(
        AnimationTrackId pivotTrackId,
        AnimationTrackId hitboxTrackId,
        AnimationTrackId hurtboxTrackId,
        AnimationTrackId socketTrackId,
        AnimationTrackId eventTrackId,
        AnimationTrackId colorCycleTrackId)
    {
        PivotTrack = new AnimationTrack<IntPoint>(pivotTrackId, "Pivot");
        HitboxTrack = new AnimationTrack<BoxFrameValue>(hitboxTrackId, "Hitboxes");
        HurtboxTrack = new AnimationTrack<BoxFrameValue>(hurtboxTrackId, "Hurtboxes");
        SocketTrack = new AnimationTrack<SocketFrameValue>(socketTrackId, "Sockets");
        EventTrack = new AnimationTrack<EventFrameValue>(eventTrackId, "Events");
        ColorCycleTrack = new AnimationTrack<ColorCycleFrameValue>(colorCycleTrackId, "Color Cycles");
    }

    public IReadOnlyList<AnimationClipId> ClipOrder => _clipOrder;
    public IReadOnlyList<AnimationTagId> TagOrder => _tagOrder;
    public IReadOnlyList<SliceId> SliceOrder => _sliceOrder;
    public AnimationTrack<IntPoint> PivotTrack { get; }
    public AnimationTrack<BoxFrameValue> HitboxTrack { get; }
    public AnimationTrack<BoxFrameValue> HurtboxTrack { get; }
    public AnimationTrack<SocketFrameValue> SocketTrack { get; }
    public AnimationTrack<EventFrameValue> EventTrack { get; }
    public AnimationTrack<ColorCycleFrameValue> ColorCycleTrack { get; }

    public AnimationClip GetClip(AnimationClipId id) => _clips.TryGetValue(id, out var value)
        ? value
        : throw new KeyNotFoundException($"Animation clip '{id}' does not exist.");

    public AnimationTag GetTag(AnimationTagId id) => _tags.TryGetValue(id, out var value)
        ? value
        : throw new KeyNotFoundException($"Animation tag '{id}' does not exist.");

    public SpriteSlice GetSlice(SliceId id) => _slices.TryGetValue(id, out var value)
        ? value
        : throw new KeyNotFoundException($"Sprite slice '{id}' does not exist.");

    internal void UpsertClip(AnimationClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (!_clips.ContainsKey(clip.Id)) _clipOrder.Add(clip.Id);
        _clips[clip.Id] = clip;
    }

    internal bool RemoveClip(AnimationClipId id, out AnimationClip clip)
    {
        if (!_clips.Remove(id, out clip!)) return false;
        _clipOrder.Remove(id);
        return true;
    }

    internal void InsertClip(int index, AnimationClip clip)
    {
        if (_clips.ContainsKey(clip.Id)) throw new InvalidOperationException($"Animation clip '{clip.Id}' already exists.");
        if ((uint)index > (uint)_clipOrder.Count) throw new ArgumentOutOfRangeException(nameof(index));
        _clips.Add(clip.Id, clip);
        _clipOrder.Insert(index, clip.Id);
    }

    internal int IndexOfClip(AnimationClipId id) => _clipOrder.IndexOf(id);

    internal void UpsertTag(AnimationTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (!_tags.ContainsKey(tag.Id)) _tagOrder.Add(tag.Id);
        _tags[tag.Id] = tag;
    }

    internal bool RemoveTag(AnimationTagId id, out AnimationTag tag)
    {
        if (!_tags.Remove(id, out tag!)) return false;
        _tagOrder.Remove(id);
        return true;
    }

    internal void InsertTag(int index, AnimationTag tag)
    {
        if (_tags.ContainsKey(tag.Id)) throw new InvalidOperationException($"Animation tag '{tag.Id}' already exists.");
        if ((uint)index > (uint)_tagOrder.Count) throw new ArgumentOutOfRangeException(nameof(index));
        _tags.Add(tag.Id, tag);
        _tagOrder.Insert(index, tag.Id);
    }

    internal int IndexOfTag(AnimationTagId id) => _tagOrder.IndexOf(id);

    internal void UpsertSlice(SpriteSlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);
        if (!_slices.ContainsKey(slice.Id)) _sliceOrder.Add(slice.Id);
        _slices[slice.Id] = slice;
    }

    internal bool RemoveSlice(SliceId id, out SpriteSlice slice)
    {
        if (!_slices.Remove(id, out slice!)) return false;
        _sliceOrder.Remove(id);
        return true;
    }

    internal void InsertSlice(int index, SpriteSlice slice)
    {
        if (_slices.ContainsKey(slice.Id)) throw new InvalidOperationException($"Sprite slice '{slice.Id}' already exists.");
        if ((uint)index > (uint)_sliceOrder.Count) throw new ArgumentOutOfRangeException(nameof(index));
        _slices.Add(slice.Id, slice);
        _sliceOrder.Insert(index, slice.Id);
    }

    internal int IndexOfSlice(SliceId id) => _sliceOrder.IndexOf(id);

    internal AnimationMetadataSnapshot Snapshot()
    {
        var clips = _clipOrder.Select(GetClip).ToArray();
        var tags = _tagOrder.Select(GetTag).ToArray();
        var slices = _sliceOrder.Select(GetSlice).ToArray();
        return new AnimationMetadataSnapshot(
            Array.AsReadOnly(clips),
            Array.AsReadOnly(tags),
            Array.AsReadOnly(slices),
            PivotTrack.Snapshot(),
            HitboxTrack.Snapshot(),
            HurtboxTrack.Snapshot(),
            SocketTrack.Snapshot(),
            EventTrack.Snapshot(),
            ColorCycleTrack.Snapshot());
    }
}

public sealed record AnimationMetadataSnapshot(
    IReadOnlyList<AnimationClip> Clips,
    IReadOnlyList<AnimationTag> Tags,
    IReadOnlyList<SpriteSlice> Slices,
    AnimationTrackSnapshot<IntPoint> PivotTrack,
    AnimationTrackSnapshot<BoxFrameValue> HitboxTrack,
    AnimationTrackSnapshot<BoxFrameValue> HurtboxTrack,
    AnimationTrackSnapshot<SocketFrameValue> SocketTrack,
    AnimationTrackSnapshot<EventFrameValue> EventTrack,
    AnimationTrackSnapshot<ColorCycleFrameValue> ColorCycleTrack);
