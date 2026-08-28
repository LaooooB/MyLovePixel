using System.Collections.ObjectModel;

namespace MyLovePixel.PluginSdk;

public readonly record struct PluginApiVersion : IComparable<PluginApiVersion>
{
    public PluginApiVersion(int major, int minor)
    {
        if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));
        if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor));
        Major = major;
        Minor = minor;
    }

    public int Major { get; }
    public int Minor { get; }

    public int CompareTo(PluginApiVersion other)
    {
        var major = Major.CompareTo(other.Major);
        return major != 0 ? major : Minor.CompareTo(other.Minor);
    }

    public override string ToString() => $"{Major}.{Minor}";

    public static bool operator <(PluginApiVersion left, PluginApiVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(PluginApiVersion left, PluginApiVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(PluginApiVersion left, PluginApiVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PluginApiVersion left, PluginApiVersion right) => left.CompareTo(right) >= 0;
}

public static class PluginApi
{
    public static PluginApiVersion Current { get; } = new(1, 0);
    public static PluginApiVersion MinimumSupported { get; } = new(1, 0);

    public static bool IsCompatible(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return manifest.MinimumApiVersion <= Current &&
               manifest.MaximumApiVersion >= MinimumSupported &&
               manifest.MinimumApiVersion.Major == Current.Major &&
               manifest.MaximumApiVersion.Major == Current.Major;
    }
}

public readonly record struct PluginId
{
    public PluginId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Plugin id cannot be empty.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > 128) throw new ArgumentOutOfRangeException(nameof(value), "Plugin id cannot exceed 128 characters.");
        if (!normalized.Contains('.', StringComparison.Ordinal))
            throw new ArgumentException("Plugin id must be namespaced, for example 'com.example.my-plugin'.", nameof(value));
        if (normalized.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_')))
            throw new ArgumentException("Plugin id contains unsupported characters.", nameof(value));
        Value = normalized;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

[Flags]
public enum PluginCapability
{
    None = 0,
    Tool = 1 << 0,
    Command = 1 << 1,
    Effect = 1 << 2,
    Exporter = 1 << 3,
    Importer = 1 << 4,
    Panel = 1 << 5,
    Palette = 1 << 6,
    Dither = 1 << 7,
    AutoTile = 1 << 8,
    ProjectData = 1 << 9,
    Script = 1 << 10,
}

public sealed record PluginManifest
{
    public PluginManifest(
        PluginId id,
        string name,
        string version,
        PluginApiVersion minimumApiVersion,
        PluginApiVersion maximumApiVersion,
        PluginCapability capabilities)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Plugin name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("Plugin version cannot be empty.", nameof(version));
        if (minimumApiVersion > maximumApiVersion)
            throw new ArgumentException("Minimum plugin API version cannot exceed maximum plugin API version.");
        Id = id;
        Name = name.Trim();
        Version = version.Trim();
        MinimumApiVersion = minimumApiVersion;
        MaximumApiVersion = maximumApiVersion;
        Capabilities = capabilities;
    }

    public PluginId Id { get; }
    public string Name { get; }
    public string Version { get; }
    public PluginApiVersion MinimumApiVersion { get; }
    public PluginApiVersion MaximumApiVersion { get; }
    public PluginCapability Capabilities { get; }
}

public enum PluginExtensionKind
{
    Tool = 1,
    Command = 2,
    Effect = 3,
    Exporter = 4,
    Importer = 5,
    Panel = 6,
    Palette = 7,
    Dither = 8,
    AutoTile = 9,
}

public interface IPluginExtension
{
    string Id { get; }
    string DisplayName { get; }
}

public interface IPluginRegistration : IDisposable
{
    PluginId Owner { get; }
    PluginExtensionKind Kind { get; }
    string ExtensionId { get; }
    bool IsDisposed { get; }
}

public interface IPluginRegistrationContext
{
    PluginManifest Manifest { get; }
    IPluginRegistration RegisterTool(IPluginTool tool);
    IPluginRegistration RegisterCommand(IPluginCommand command);
    IPluginRegistration RegisterEffect(IPluginEffectEvaluator effect);
    IPluginRegistration RegisterExporter(IPluginExporter exporter);
    IPluginRegistration RegisterImporter(IPluginImporter importer);
    IPluginRegistration RegisterPanel(IPluginPanelProvider panel);
    IPluginRegistration RegisterPaletteAlgorithm(IPluginPaletteAlgorithm algorithm);
    IPluginRegistration RegisterDitherAlgorithm(IPluginDitherAlgorithm algorithm);
    IPluginRegistration RegisterAutoTileRule(IPluginAutoTileRule rule);
}

public interface IPlugin
{
    PluginManifest Manifest { get; }
    void Register(IPluginRegistrationContext context);
}

public interface IPluginLifecycle
{
    void OnUnload();
}

public readonly record struct PluginIntPoint(int X, int Y);

public readonly record struct PluginIntSize
{
    public PluginIntSize(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }
}

public readonly record struct PluginIntRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public readonly record struct PluginRgba32(byte R, byte G, byte B, byte A);

public sealed class PluginImage
{
    private readonly byte[] _rgba;

    public PluginImage(PluginIntSize size, ReadOnlyMemory<byte> rgba, PluginIntPoint origin = default)
    {
        var expected = checked(size.Width * size.Height * 4);
        if (rgba.Length != expected) throw new ArgumentException("RGBA payload length does not match image size.", nameof(rgba));
        Size = size;
        Origin = origin;
        _rgba = rgba.ToArray();
    }

    public PluginIntSize Size { get; }
    public PluginIntPoint Origin { get; }
    public ReadOnlyMemory<byte> Rgba => _rgba;

    public PluginRgba32 GetPixel(int x, int y)
    {
        if ((uint)x >= (uint)Size.Width || (uint)y >= (uint)Size.Height) throw new ArgumentOutOfRangeException(nameof(x));
        var offset = checked(((y * Size.Width) + x) * 4);
        return new PluginRgba32(_rgba[offset], _rgba[offset + 1], _rgba[offset + 2], _rgba[offset + 3]);
    }
}

public enum PluginValueKind
{
    Integer = 1,
    Number = 2,
    Boolean = 3,
    Color = 4,
    Point = 5,
    Identifier = 6,
    Text = 7,
}

public sealed record PluginValue
{
    private PluginValue(
        PluginValueKind kind,
        long integerValue = 0,
        double numberValue = 0,
        bool booleanValue = false,
        PluginRgba32 colorValue = default,
        PluginIntPoint pointValue = default,
        Guid identifierValue = default,
        string? textValue = null)
    {
        Kind = kind;
        IntegerValue = integerValue;
        NumberValue = numberValue;
        BooleanValue = booleanValue;
        ColorValue = colorValue;
        PointValue = pointValue;
        IdentifierValue = identifierValue;
        TextValue = textValue;
    }

    public PluginValueKind Kind { get; }
    public long IntegerValue { get; }
    public double NumberValue { get; }
    public bool BooleanValue { get; }
    public PluginRgba32 ColorValue { get; }
    public PluginIntPoint PointValue { get; }
    public Guid IdentifierValue { get; }
    public string? TextValue { get; }

    public static PluginValue Integer(long value) => new(PluginValueKind.Integer, integerValue: value);
    public static PluginValue Number(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return new PluginValue(PluginValueKind.Number, numberValue: value);
    }
    public static PluginValue Boolean(bool value) => new(PluginValueKind.Boolean, booleanValue: value);
    public static PluginValue Color(PluginRgba32 value) => new(PluginValueKind.Color, colorValue: value);
    public static PluginValue Point(PluginIntPoint value) => new(PluginValueKind.Point, pointValue: value);
    public static PluginValue Identifier(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Identifier cannot be empty.", nameof(value));
        return new PluginValue(PluginValueKind.Identifier, identifierValue: value);
    }
    public static PluginValue Text(string value) => new(PluginValueKind.Text, textValue: value ?? throw new ArgumentNullException(nameof(value)));
}

public static class PluginCollections
{
    public static IReadOnlyDictionary<string, string> Freeze(IReadOnlyDictionary<string, string>? values) =>
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(values ?? new Dictionary<string, string>(), StringComparer.Ordinal));
}
