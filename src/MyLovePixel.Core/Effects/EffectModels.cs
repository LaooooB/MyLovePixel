using System.Collections.ObjectModel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Effects;

public enum EffectParameterKind
{
    Integer = 1,
    Number = 2,
    Boolean = 3,
    Color = 4,
    Point = 5,
    PaletteReference = 6,
    Text = 7,
}

public sealed record EffectValue
{
    private EffectValue(
        EffectParameterKind kind,
        long integerValue = 0,
        double numberValue = 0,
        bool booleanValue = false,
        Rgba32 colorValue = default,
        IntPoint pointValue = default,
        PaletteId paletteIdValue = default,
        string? textValue = null)
    {
        Kind = kind;
        IntegerValue = integerValue;
        NumberValue = numberValue;
        BooleanValue = booleanValue;
        ColorValue = colorValue;
        PointValue = pointValue;
        PaletteIdValue = paletteIdValue;
        TextValue = textValue;
    }

    public EffectParameterKind Kind { get; }
    public long IntegerValue { get; }
    public double NumberValue { get; }
    public bool BooleanValue { get; }
    public Rgba32 ColorValue { get; }
    public IntPoint PointValue { get; }
    public PaletteId PaletteIdValue { get; }
    public string? TextValue { get; }

    public static EffectValue Integer(long value) => new(EffectParameterKind.Integer, integerValue: value);

    public static EffectValue Number(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), "Effect number values must be finite.");
        return new EffectValue(EffectParameterKind.Number, numberValue: value);
    }

    public static EffectValue Boolean(bool value) => new(EffectParameterKind.Boolean, booleanValue: value);
    public static EffectValue Color(Rgba32 value) => new(EffectParameterKind.Color, colorValue: value);
    public static EffectValue Point(IntPoint value) => new(EffectParameterKind.Point, pointValue: value);

    public static EffectValue PaletteReference(PaletteId value)
    {
        if (value.Value == Guid.Empty) throw new ArgumentException("PaletteId cannot be empty.", nameof(value));
        return new EffectValue(EffectParameterKind.PaletteReference, paletteIdValue: value);
    }

    public static EffectValue Text(string value) =>
        new(EffectParameterKind.Text, textValue: value ?? throw new ArgumentNullException(nameof(value)));
}

public sealed record EffectParameterDescriptor
{
    public EffectParameterDescriptor(
        string key,
        string displayName,
        EffectParameterKind kind,
        EffectValue defaultValue,
        bool animatable = true,
        double? minimum = null,
        double? maximum = null)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Effect parameter key cannot be empty.", nameof(key));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Effect parameter display name cannot be empty.", nameof(displayName));
        ArgumentNullException.ThrowIfNull(defaultValue);
        if (defaultValue.Kind != kind) throw new ArgumentException("Default value kind must match the descriptor kind.", nameof(defaultValue));
        if (minimum is { } min && !double.IsFinite(min)) throw new ArgumentOutOfRangeException(nameof(minimum));
        if (maximum is { } max && !double.IsFinite(max)) throw new ArgumentOutOfRangeException(nameof(maximum));
        if (minimum is { } lower && maximum is { } upper && lower > upper)
            throw new ArgumentException("Effect parameter minimum cannot exceed maximum.");

        Key = key;
        DisplayName = displayName;
        Kind = kind;
        DefaultValue = defaultValue;
        Animatable = animatable;
        Minimum = minimum;
        Maximum = maximum;
        Validate(defaultValue);
    }

    public string Key { get; }
    public string DisplayName { get; }
    public EffectParameterKind Kind { get; }
    public EffectValue DefaultValue { get; }
    public bool Animatable { get; }
    public double? Minimum { get; }
    public double? Maximum { get; }

    public void Validate(EffectValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Kind != Kind)
            throw new ArgumentException($"Effect parameter '{Key}' expects {Kind}, received {value.Kind}.", nameof(value));

        var numericValue = Kind switch
        {
            EffectParameterKind.Integer => (double)value.IntegerValue,
            EffectParameterKind.Number => value.NumberValue,
            _ => (double?)null,
        };

        if (numericValue is not { } numeric) return;
        if (Minimum is { } minimum && numeric < minimum)
            throw new ArgumentOutOfRangeException(nameof(value), $"Effect parameter '{Key}' cannot be less than {minimum}.");
        if (Maximum is { } maximum && numeric > maximum)
            throw new ArgumentOutOfRangeException(nameof(value), $"Effect parameter '{Key}' cannot exceed {maximum}.");
    }
}

public sealed class EffectDescriptor
{
    private readonly ReadOnlyDictionary<string, EffectParameterDescriptor> _parameters;

    public EffectDescriptor(
        string typeId,
        string displayName,
        IEnumerable<EffectParameterDescriptor> parameters)
    {
        if (string.IsNullOrWhiteSpace(typeId)) throw new ArgumentException("Effect type id cannot be empty.", nameof(typeId));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Effect display name cannot be empty.", nameof(displayName));
        ArgumentNullException.ThrowIfNull(parameters);

        var values = new Dictionary<string, EffectParameterDescriptor>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            ArgumentNullException.ThrowIfNull(parameter);
            if (!values.TryAdd(parameter.Key, parameter))
                throw new ArgumentException($"Effect descriptor contains duplicate parameter key '{parameter.Key}'.", nameof(parameters));
        }

        TypeId = typeId;
        DisplayName = displayName;
        _parameters = new ReadOnlyDictionary<string, EffectParameterDescriptor>(values);
    }

    public string TypeId { get; }
    public string DisplayName { get; }
    public IReadOnlyDictionary<string, EffectParameterDescriptor> Parameters => _parameters;

    public EffectParameterDescriptor GetParameter(string key) =>
        _parameters.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"Effect '{TypeId}' has no parameter '{key}'.");

    public void Validate(EffectInstanceSnapshot instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (!string.Equals(instance.TypeId, TypeId, StringComparison.Ordinal))
            throw new ArgumentException($"Effect instance type '{instance.TypeId}' does not match descriptor '{TypeId}'.", nameof(instance));

        foreach (var pair in instance.Parameters)
            GetParameter(pair.Key).Validate(pair.Value);

        foreach (var pair in instance.ParameterTracks)
        {
            var descriptor = GetParameter(pair.Key);
            if (!descriptor.Animatable)
                throw new ArgumentException($"Effect parameter '{pair.Key}' is not animatable.", nameof(instance));
            foreach (var value in pair.Value.Values.Values)
                descriptor.Validate(value);
        }
    }
}

public sealed class EffectInstance
{
    private readonly Dictionary<string, EffectValue> _parameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AnimationTrack<EffectValue>> _parameterTracks = new(StringComparer.Ordinal);

    public EffectInstance(EffectInstanceId id, string typeId, bool enabled = true)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("EffectInstanceId cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(typeId)) throw new ArgumentException("Effect type id cannot be empty.", nameof(typeId));
        Id = id;
        TypeId = typeId;
        Enabled = enabled;
    }

    public EffectInstanceId Id { get; }
    public string TypeId { get; }
    public bool Enabled { get; internal set; }
    public long Revision { get; private set; }
    public IReadOnlyDictionary<string, EffectValue> Parameters =>
        new ReadOnlyDictionary<string, EffectValue>(_parameters);
    public IReadOnlyDictionary<string, AnimationTrack<EffectValue>> ParameterTracks =>
        new ReadOnlyDictionary<string, AnimationTrack<EffectValue>>(_parameterTracks);

    public EffectValue ResolveParameter(string key, FrameId frameId, EffectDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var parameter = descriptor.GetParameter(key);
        if (_parameterTracks.TryGetValue(key, out var track) && track.TryGetValue(frameId, out var animated))
        {
            parameter.Validate(animated);
            return animated;
        }
        if (_parameters.TryGetValue(key, out var value))
        {
            parameter.Validate(value);
            return value;
        }
        return parameter.DefaultValue;
    }

    internal void SetEnabled(bool enabled)
    {
        if (Enabled == enabled) return;
        var nextRevision = checked(Revision + 1);
        Enabled = enabled;
        Revision = nextRevision;
    }

    internal bool SetParameter(string key, EffectValue value, out EffectValue? previous)
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(value);
        _parameters.TryGetValue(key, out previous);
        if (Equals(previous, value)) return false;
        var nextRevision = checked(Revision + 1);
        _parameters[key] = value;
        Revision = nextRevision;
        return true;
    }

    internal bool RemoveParameter(string key, out EffectValue value)
    {
        ValidateKey(key);
        if (!_parameters.Remove(key, out value!)) return false;
        Revision = checked(Revision + 1);
        return true;
    }

    internal AnimationTrack<EffectValue> GetOrCreateParameterTrack(
        string key,
        AnimationTrackId trackId,
        out bool created)
    {
        ValidateKey(key);
        if (_parameterTracks.TryGetValue(key, out var existing))
        {
            created = false;
            return existing;
        }
        var track = new AnimationTrack<EffectValue>(trackId, key);
        _parameterTracks.Add(key, track);
        Revision = checked(Revision + 1);
        created = true;
        return track;
    }

    internal bool RemoveParameterTrack(string key, out AnimationTrack<EffectValue> track)
    {
        ValidateKey(key);
        if (!_parameterTracks.Remove(key, out track!)) return false;
        Revision = checked(Revision + 1);
        return true;
    }

    internal void RestoreParameterTrack(string key, AnimationTrack<EffectValue> track)
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(track);
        if (_parameterTracks.ContainsKey(key))
            throw new InvalidOperationException($"Effect parameter track '{key}' already exists.");
        _parameterTracks.Add(key, track);
        Revision = checked(Revision + 1);
    }

    internal bool SetKeyframe(string key, FrameId frameId, EffectValue value, AnimationTrackId newTrackId, out EffectValue? previous, out bool trackCreated)
    {
        ArgumentNullException.ThrowIfNull(value);
        var track = GetOrCreateParameterTrack(key, newTrackId, out trackCreated);
        track.TryGetValue(frameId, out previous);
        if (Equals(previous, value)) return false;
        track.Set(frameId, value);
        Revision = checked(Revision + 1);
        return true;
    }

    internal bool RemoveKeyframe(string key, FrameId frameId, out EffectValue value)
    {
        ValidateKey(key);
        if (!_parameterTracks.TryGetValue(key, out var track) || !track.Remove(frameId, out value!))
            return false;
        Revision = checked(Revision + 1);
        return true;
    }

    internal EffectInstanceSnapshot Snapshot()
    {
        var parameters = new ReadOnlyDictionary<string, EffectValue>(
            new Dictionary<string, EffectValue>(_parameters, StringComparer.Ordinal));
        var tracks = _parameterTracks.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Snapshot(),
            StringComparer.Ordinal);
        return new EffectInstanceSnapshot(
            Id,
            TypeId,
            Enabled,
            Revision,
            parameters,
            new ReadOnlyDictionary<string, AnimationTrackSnapshot<EffectValue>>(tracks));
    }

    internal static EffectInstance FromSnapshot(EffectInstanceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var instance = new EffectInstance(snapshot.Id, snapshot.TypeId, snapshot.Enabled);
        foreach (var pair in snapshot.Parameters)
            instance._parameters.Add(pair.Key, pair.Value);
        foreach (var pair in snapshot.ParameterTracks)
        {
            var track = new AnimationTrack<EffectValue>(pair.Value.Id, pair.Value.Name);
            foreach (var value in pair.Value.Values)
                track.Restore(value.Key, value.Value);
            instance._parameterTracks.Add(pair.Key, track);
        }
        instance.Revision = snapshot.Revision;
        return instance;
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Effect parameter key cannot be empty.", nameof(key));
    }
}

public sealed record EffectInstanceSnapshot(
    EffectInstanceId Id,
    string TypeId,
    bool Enabled,
    long Revision,
    IReadOnlyDictionary<string, EffectValue> Parameters,
    IReadOnlyDictionary<string, AnimationTrackSnapshot<EffectValue>> ParameterTracks)
{
    public bool TryResolveParameter(string key, FrameId frameId, EffectDescriptor descriptor, out EffectValue value)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var parameter = descriptor.GetParameter(key);
        if (ParameterTracks.TryGetValue(key, out var track) && track.Values.TryGetValue(frameId, out var animated))
        {
            parameter.Validate(animated);
            value = animated;
            return true;
        }
        if (Parameters.TryGetValue(key, out value!))
        {
            parameter.Validate(value);
            return true;
        }
        value = parameter.DefaultValue;
        return true;
    }
}

public sealed class EffectGraph
{
    private readonly Dictionary<EffectInstanceId, EffectInstance> _effects = [];
    private readonly List<EffectInstanceId> _order = [];

    public long Revision { get; private set; }
    public IReadOnlyList<EffectInstanceId> EffectOrder => _order;

    public EffectInstance GetEffect(EffectInstanceId id) =>
        _effects.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Effect instance '{id}' does not exist.");

    internal void Add(EffectInstance effect) => Insert(_order.Count, effect);

    internal void Insert(int index, EffectInstance effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if ((uint)index > (uint)_order.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (_effects.ContainsKey(effect.Id)) throw new InvalidOperationException($"Effect instance '{effect.Id}' already exists.");
        var nextRevision = checked(Revision + 1);
        _effects.Add(effect.Id, effect);
        _order.Insert(index, effect.Id);
        Revision = nextRevision;
    }

    internal EffectInstance Remove(EffectInstanceId id, out int index)
    {
        if (!_effects.Remove(id, out var effect))
            throw new KeyNotFoundException($"Effect instance '{id}' does not exist.");
        index = _order.IndexOf(id);
        _order.RemoveAt(index);
        Revision = checked(Revision + 1);
        return effect;
    }

    internal void Move(EffectInstanceId id, int newIndex)
    {
        var oldIndex = _order.IndexOf(id);
        if (oldIndex < 0) throw new KeyNotFoundException($"Effect instance '{id}' does not exist.");
        if ((uint)newIndex >= (uint)_order.Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
        if (oldIndex == newIndex) return;
        var nextRevision = checked(Revision + 1);
        _order.RemoveAt(oldIndex);
        _order.Insert(newIndex, id);
        Revision = nextRevision;
    }

    internal EffectGraphSnapshot Snapshot()
    {
        var order = _order.ToArray();
        var effects = order.ToDictionary(id => id, id => GetEffect(id).Snapshot());
        return new EffectGraphSnapshot(
            Revision,
            Array.AsReadOnly(order),
            new ReadOnlyDictionary<EffectInstanceId, EffectInstanceSnapshot>(effects));
    }

    internal static EffectGraph FromSnapshot(EffectGraphSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var graph = new EffectGraph();
        foreach (var id in snapshot.EffectOrder)
        {
            if (!snapshot.Effects.TryGetValue(id, out var effect))
                throw new InvalidOperationException($"Effect graph snapshot order references missing effect '{id}'.");
            graph._effects.Add(id, EffectInstance.FromSnapshot(effect));
            graph._order.Add(id);
        }
        graph.Revision = snapshot.Revision;
        return graph;
    }
}

public sealed record EffectGraphSnapshot(
    long Revision,
    IReadOnlyList<EffectInstanceId> EffectOrder,
    IReadOnlyDictionary<EffectInstanceId, EffectInstanceSnapshot> Effects)
{
    public static EffectGraphSnapshot Empty { get; } = new(
        0,
        Array.Empty<EffectInstanceId>(),
        new ReadOnlyDictionary<EffectInstanceId, EffectInstanceSnapshot>(
            new Dictionary<EffectInstanceId, EffectInstanceSnapshot>()));

    public EffectInstanceSnapshot GetEffect(EffectInstanceId id) =>
        Effects.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Effect instance snapshot '{id}' does not exist.");
}
