using System.Collections.ObjectModel;

namespace MyLovePixel.PluginSdk;

public enum PluginPointerKind
{
    Pressed = 1,
    Moved = 2,
    Released = 3,
    Cancelled = 4,
}

[Flags]
public enum PluginPointerButtons
{
    None = 0,
    Primary = 1,
    Secondary = 2,
    Middle = 4,
    Barrel = 8,
    Eraser = 16,
}

public sealed record PluginPointerEvent(
    long PointerId,
    PluginPointerKind Kind,
    PluginIntPoint Position,
    double Pressure,
    PluginPointerButtons Buttons,
    long Timestamp);

public sealed class PluginRasterTarget
{
    private readonly byte[] _rgba;

    public PluginRasterTarget(Guid surfaceId, long revision, PluginIntSize size, ReadOnlyMemory<byte> rgba)
    {
        if (surfaceId == Guid.Empty) throw new ArgumentException("Surface id cannot be empty.", nameof(surfaceId));
        if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        var expected = checked(size.Width * size.Height * 4);
        if (rgba.Length != expected) throw new ArgumentException("RGBA payload length does not match target size.", nameof(rgba));
        SurfaceId = surfaceId;
        Revision = revision;
        Size = size;
        _rgba = rgba.ToArray();
    }

    public Guid SurfaceId { get; }
    public long Revision { get; }
    public PluginIntSize Size { get; }
    public ReadOnlyMemory<byte> Rgba => _rgba;
}

public sealed record PluginPixelWrite(int X, int Y, PluginRgba32 Color);

public sealed class PluginPixelPatch
{
    private readonly PluginPixelWrite[] _writes;

    public PluginPixelPatch(Guid surfaceId, long expectedRevision, IEnumerable<PluginPixelWrite> writes, string name = "Plugin Pixel Patch")
    {
        if (surfaceId == Guid.Empty) throw new ArgumentException("Surface id cannot be empty.", nameof(surfaceId));
        if (expectedRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
        ArgumentNullException.ThrowIfNull(writes);
        _writes = writes.GroupBy(value => (value.X, value.Y)).Select(group => group.Last()).ToArray();
        if (_writes.Length == 0) throw new ArgumentException("Plugin pixel patch cannot be empty.", nameof(writes));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Patch name cannot be empty.", nameof(name));
        SurfaceId = surfaceId;
        ExpectedRevision = expectedRevision;
        Name = name;
    }

    public Guid SurfaceId { get; }
    public long ExpectedRevision { get; }
    public string Name { get; }
    public IReadOnlyList<PluginPixelWrite> Writes => Array.AsReadOnly(_writes);
}

public sealed record PluginToolResult(
    bool Consumed,
    IReadOnlyList<PluginPixelWrite> PreviewWrites,
    PluginPixelPatch? Commit)
{
    public static PluginToolResult Ignored { get; } = new(false, Array.Empty<PluginPixelWrite>(), null);
}

public interface IPluginTool : IPluginExtension
{
    PluginToolResult Handle(PluginPointerEvent pointerEvent, PluginRasterTarget target);
}

public sealed record PluginCommandRequest(string CommandId, PluginRasterTarget? Target, IReadOnlyDictionary<string, PluginValue> Arguments);
public sealed record PluginCommandResult(PluginPixelPatch? Mutation, string? Message = null);

public interface IPluginCommand : IPluginExtension
{
    PluginCommandResult Execute(PluginCommandRequest request);
}

public enum PluginEffectParameterKind
{
    Integer = 1,
    Number = 2,
    Boolean = 3,
    Color = 4,
    Point = 5,
    PaletteReference = 6,
    Text = 7,
}

public sealed record PluginEffectParameterDescriptor(
    string Key,
    string DisplayName,
    PluginEffectParameterKind Kind,
    PluginValue DefaultValue,
    bool Animatable = true,
    double? Minimum = null,
    double? Maximum = null);

public sealed class PluginEffectDescriptor
{
    private readonly PluginEffectParameterDescriptor[] _parameters;

    public PluginEffectDescriptor(string typeId, string displayName, IEnumerable<PluginEffectParameterDescriptor>? parameters = null)
    {
        ValidateExtensionId(typeId, nameof(typeId));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Effect display name cannot be empty.", nameof(displayName));
        _parameters = (parameters ?? Array.Empty<PluginEffectParameterDescriptor>()).ToArray();
        if (_parameters.Select(value => value.Key).Distinct(StringComparer.Ordinal).Count() != _parameters.Length)
            throw new ArgumentException("Effect parameter keys must be unique.", nameof(parameters));
        TypeId = typeId;
        DisplayName = displayName;
    }

    public string TypeId { get; }
    public string DisplayName { get; }
    public IReadOnlyList<PluginEffectParameterDescriptor> Parameters => Array.AsReadOnly(_parameters);

    private static void ValidateExtensionId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Extension id cannot be empty.", parameterName);
        if (!value.Contains('.', StringComparison.Ordinal)) throw new ArgumentException("Extension id must be namespaced.", parameterName);
    }
}

public sealed record PluginEffectRequest(
    Guid DocumentId,
    Guid FrameId,
    Guid CelId,
    PluginImage Source,
    IReadOnlyDictionary<string, PluginValue> Parameters);

public interface IPluginEffectEvaluator : IPluginExtension
{
    PluginEffectDescriptor Descriptor { get; }
    PluginImage Evaluate(PluginEffectRequest request);
}

public sealed record PluginExportFrame(Guid FrameId, long DurationTicks, PluginImage Image);

public sealed class PluginExportRequest
{
    private readonly PluginExportFrame[] _frames;

    public PluginExportRequest(Guid documentId, string presetName, IEnumerable<PluginExportFrame> frames, IReadOnlyDictionary<string, string>? options = null)
    {
        if (documentId == Guid.Empty) throw new ArgumentException("Document id cannot be empty.", nameof(documentId));
        if (string.IsNullOrWhiteSpace(presetName)) throw new ArgumentException("Preset name cannot be empty.", nameof(presetName));
        ArgumentNullException.ThrowIfNull(frames);
        _frames = frames.ToArray();
        if (_frames.Length == 0) throw new ArgumentException("Plugin export requires at least one frame.", nameof(frames));
        DocumentId = documentId;
        PresetName = presetName;
        Options = PluginCollections.Freeze(options);
    }

    public Guid DocumentId { get; }
    public string PresetName { get; }
    public IReadOnlyList<PluginExportFrame> Frames => Array.AsReadOnly(_frames);
    public IReadOnlyDictionary<string, string> Options { get; }
}

public sealed record PluginExportArtifact
{
    private readonly byte[] _content;

    public PluginExportArtifact(string relativePath, string mediaType, ReadOnlyMemory<byte> content)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("Artifact path cannot be empty.", nameof(relativePath));
        if (Path.IsPathRooted(relativePath) || relativePath.Split('/', '\\').Any(segment => segment == ".."))
            throw new ArgumentException("Artifact path must be safe and relative.", nameof(relativePath));
        if (string.IsNullOrWhiteSpace(mediaType)) throw new ArgumentException("Media type cannot be empty.", nameof(mediaType));
        RelativePath = relativePath.Replace('\\', '/');
        MediaType = mediaType;
        _content = content.ToArray();
    }

    public string RelativePath { get; }
    public string MediaType { get; }
    public ReadOnlyMemory<byte> Content => _content;
}

public sealed class PluginExportBundle
{
    private readonly PluginExportArtifact[] _artifacts;

    public PluginExportBundle(IEnumerable<PluginExportArtifact> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        _artifacts = artifacts.ToArray();
        if (_artifacts.Length == 0) throw new ArgumentException("Plugin export bundle cannot be empty.", nameof(artifacts));
        if (_artifacts.Select(value => value.RelativePath).Distinct(StringComparer.Ordinal).Count() != _artifacts.Length)
            throw new ArgumentException("Plugin export artifact paths must be unique.", nameof(artifacts));
    }

    public IReadOnlyList<PluginExportArtifact> Artifacts => Array.AsReadOnly(_artifacts);
}

public interface IPluginExporter : IPluginExtension
{
    PluginExportBundle Export(PluginExportRequest request);
}

public sealed record PluginImportRequest(string Name, ReadOnlyMemory<byte> Content);
public sealed record PluginImportResult(PluginImage Image, IReadOnlyDictionary<string, string>? Metadata = null);

public interface IPluginImporter : IPluginExtension
{
    bool CanImport(string name, ReadOnlySpan<byte> header);
    PluginImportResult Import(PluginImportRequest request);
}

public sealed record PluginPanelAction(string Id, string Label, bool Enabled = true);
public sealed record PluginPanelField(string Id, string Label, string Value, bool ReadOnly = true);
public sealed record PluginPanelSection(string Title, IReadOnlyList<PluginPanelField> Fields, IReadOnlyList<PluginPanelAction> Actions);
public sealed record PluginPanelModel(string Title, IReadOnlyList<PluginPanelSection> Sections);
public sealed record PluginPanelContext(Guid? DocumentId, Guid? FrameId, Guid? LayerId);

public interface IPluginPanelProvider : IPluginExtension
{
    PluginPanelModel Build(PluginPanelContext context);
    PluginPixelPatch? Invoke(string actionId, PluginPanelContext context, PluginRasterTarget? target);
}

public interface IPluginPaletteAlgorithm : IPluginExtension
{
    IReadOnlyList<PluginRgba32> Process(IReadOnlyList<PluginRgba32> colors);
}

public interface IPluginDitherAlgorithm : IPluginExtension
{
    PluginImage Process(PluginImage image, IReadOnlyList<PluginRgba32> palette);
}

public interface IPluginAutoTileRule : IPluginExtension
{
    int ResolveVariant(long documentSeed, PluginIntPoint coordinate, int neighborMask);
}
