namespace MyLovePixel.Tools;

public enum ToolOptionKind
{
    Boolean = 1,
    Integer = 2,
    Enum = 3,
}

public sealed class ToolOptionDefinition
{
    private ToolOptionDefinition(
        string id,
        string displayName,
        ToolOptionKind kind,
        object defaultValue,
        int? minimum,
        int? maximum,
        IReadOnlyList<string> allowedValues)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Tool option id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Tool option display name cannot be empty.", nameof(displayName));
        Id = id;
        DisplayName = displayName;
        Kind = kind;
        Minimum = minimum;
        Maximum = maximum;
        AllowedValues = allowedValues;
        DefaultValue = Validate(defaultValue);
    }

    public string Id { get; }
    public string DisplayName { get; }
    public ToolOptionKind Kind { get; }
    public object DefaultValue { get; }
    public int? Minimum { get; }
    public int? Maximum { get; }
    public IReadOnlyList<string> AllowedValues { get; }

    public static ToolOptionDefinition Boolean(string id, string displayName, bool defaultValue) =>
        new(id, displayName, ToolOptionKind.Boolean, defaultValue, null, null, Array.Empty<string>());

    public static ToolOptionDefinition Integer(
        string id,
        string displayName,
        int defaultValue,
        int minimum,
        int maximum)
    {
        if (minimum > maximum) throw new ArgumentException("Minimum cannot exceed maximum.", nameof(minimum));
        return new ToolOptionDefinition(id, displayName, ToolOptionKind.Integer, defaultValue, minimum, maximum, Array.Empty<string>());
    }

    public static ToolOptionDefinition Enum(
        string id,
        string displayName,
        string defaultValue,
        params string[] allowedValues)
    {
        ArgumentNullException.ThrowIfNull(allowedValues);
        if (allowedValues.Length == 0) throw new ArgumentException("Enum options require at least one value.", nameof(allowedValues));
        if (allowedValues.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Enum option values cannot be empty.", nameof(allowedValues));
        var distinct = allowedValues.Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length != allowedValues.Length) throw new ArgumentException("Enum option values must be unique.", nameof(allowedValues));
        return new ToolOptionDefinition(id, displayName, ToolOptionKind.Enum, defaultValue, null, null, Array.AsReadOnly(distinct));
    }

    internal object Validate(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Kind switch
        {
            ToolOptionKind.Boolean when value is bool boolean => boolean,
            ToolOptionKind.Integer when value is int integer => ValidateInteger(integer),
            ToolOptionKind.Enum when value is string enumValue => ValidateEnum(enumValue),
            ToolOptionKind.Boolean => throw new ArgumentException($"Option '{Id}' requires a Boolean value.", nameof(value)),
            ToolOptionKind.Integer => throw new ArgumentException($"Option '{Id}' requires an Int32 value.", nameof(value)),
            ToolOptionKind.Enum => throw new ArgumentException($"Option '{Id}' requires a string enum value.", nameof(value)),
            _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
        };
    }

    private int ValidateInteger(int value)
    {
        if (Minimum is int minimum && value < minimum)
            throw new ArgumentOutOfRangeException(nameof(value), $"Option '{Id}' must be at least {minimum}.");
        if (Maximum is int maximum && value > maximum)
            throw new ArgumentOutOfRangeException(nameof(value), $"Option '{Id}' must be at most {maximum}.");
        return value;
    }

    private string ValidateEnum(string value)
    {
        if (!AllowedValues.Contains(value, StringComparer.Ordinal))
            throw new ArgumentException($"Option '{Id}' does not allow value '{value}'.", nameof(value));
        return value;
    }
}

public sealed class ToolOptionSchema
{
    private readonly ToolOptionDefinition[] _definitions;
    private readonly Dictionary<string, ToolOptionDefinition> _byId;

    public ToolOptionSchema(IEnumerable<ToolOptionDefinition>? definitions = null)
    {
        _definitions = (definitions ?? Array.Empty<ToolOptionDefinition>()).ToArray();
        if (_definitions.Any(definition => definition is null))
            throw new ArgumentException("Tool option definitions cannot contain null values.", nameof(definitions));

        _byId = new Dictionary<string, ToolOptionDefinition>(StringComparer.Ordinal);
        foreach (var definition in _definitions)
        {
            if (!_byId.TryAdd(definition.Id, definition))
                throw new ArgumentException($"Tool option '{definition.Id}' is defined more than once.", nameof(definitions));
        }
    }

    public IReadOnlyList<ToolOptionDefinition> Definitions => Array.AsReadOnly(_definitions);

    public ToolOptionDefinition GetDefinition(string id) =>
        _byId.TryGetValue(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Tool option '{id}' is not defined.");

    public ToolOptions CreateDefaults()
    {
        var values = _definitions.ToDictionary(
            definition => definition.Id,
            definition => definition.DefaultValue,
            StringComparer.Ordinal);
        return new ToolOptions(this, values);
    }
}

public sealed class ToolOptions
{
    private readonly Dictionary<string, object> _values;

    internal ToolOptions(ToolOptionSchema schema, Dictionary<string, object> values)
    {
        Schema = schema;
        _values = values;
    }

    public ToolOptionSchema Schema { get; }

    public bool GetBoolean(string id) => Get<bool>(id);
    public int GetInteger(string id) => Get<int>(id);
    public string GetEnum(string id) => Get<string>(id);

    public ToolOptions With(string id, object value)
    {
        var definition = Schema.GetDefinition(id);
        var validated = definition.Validate(value);
        var copy = new Dictionary<string, object>(_values, StringComparer.Ordinal)
        {
            [id] = validated,
        };
        return new ToolOptions(Schema, copy);
    }

    private T Get<T>(string id)
    {
        Schema.GetDefinition(id);
        if (!_values.TryGetValue(id, out var value))
            throw new InvalidOperationException($"Tool option '{id}' has no value.");
        if (value is not T typed)
            throw new InvalidOperationException($"Tool option '{id}' has an unexpected runtime type.");
        return typed;
    }
}
