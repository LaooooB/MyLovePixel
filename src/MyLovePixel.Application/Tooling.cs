using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Tools;

namespace MyLovePixel.Application;

public enum EditorPointerDevice
{
    Unknown = 0,
    Mouse = 1,
    Pen = 2,
    Touch = 3,
}

public enum EditorPointerKind
{
    Pressed = 1,
    Moved = 2,
    Released = 3,
    Cancelled = 4,
}

[Flags]
public enum EditorPointerButtons
{
    None = 0,
    Primary = 1,
    Secondary = 2,
    Middle = 4,
    Barrel = 8,
    Eraser = 16,
}

[Flags]
public enum EditorInputModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
    Meta = 8,
}

public readonly record struct EditorPointerEvent(
    long PointerId,
    EditorPointerDevice Device,
    EditorPointerKind Kind,
    IntPoint CanvasPixel,
    double Pressure,
    EditorPointerButtons Buttons,
    EditorInputModifiers Modifiers,
    long TimestampTicks);

public sealed record ToolPaletteItem(string Id, string DisplayName, bool IsActive);

public enum ToolOptionPresentationKind
{
    Boolean = 1,
    Integer = 2,
    Enum = 3,
}

public sealed record ToolOptionPresentation(
    string Id,
    string DisplayName,
    ToolOptionPresentationKind Kind,
    object Value,
    int? Minimum,
    int? Maximum,
    IReadOnlyList<string> AllowedValues);

public sealed record ToolDispatchPresentation(bool Consumed, bool Committed, bool HasPreview);
public sealed record ToolColorState(Rgba32 Primary, Rgba32 Secondary);
public sealed record CanvasPreviewPixel(IntPoint Point, Rgba32 Color);

internal static class BuiltinToolCatalog
{
    private static readonly IReadOnlyDictionary<string, Func<ITool>> Factories =
        new Dictionary<string, Func<ITool>>(StringComparer.Ordinal)
        {
            [ToolDescriptors.Pencil.Id] = static () => new PencilTool(),
            [ToolDescriptors.Eraser.Id] = static () => new EraserTool(),
            [ToolDescriptors.Line.Id] = static () => new LineTool(),
            [ToolDescriptors.Shape.Id] = static () => new ShapeTool(),
            [ToolDescriptors.Fill.Id] = static () => new FillTool(),
        };

    public static IReadOnlyList<ToolPaletteItem> Describe(string activeToolId) =>
        Factories
            .Select(pair => pair.Value().Descriptor)
            .OrderBy(descriptor => descriptor.DisplayName, StringComparer.Ordinal)
            .Select(descriptor => new ToolPaletteItem(
                descriptor.Id,
                descriptor.DisplayName,
                string.Equals(descriptor.Id, activeToolId, StringComparison.Ordinal)))
            .ToArray();

    public static ITool Create(string id) =>
        Factories.TryGetValue(id, out var factory)
            ? factory()
            : throw new KeyNotFoundException($"Tool '{id}' is not registered.");

    public static string DefaultToolId => ToolDescriptors.Pencil.Id;
}

internal static class ToolPresentationMapper
{
    public static PointerEvent ToToolEvent(EditorPointerEvent value) => new(
        value.PointerId,
        value.Device switch
        {
            EditorPointerDevice.Mouse => PointerDeviceKind.Mouse,
            EditorPointerDevice.Pen => PointerDeviceKind.Pen,
            EditorPointerDevice.Touch => PointerDeviceKind.Touch,
            _ => PointerDeviceKind.Unknown,
        },
        value.Kind switch
        {
            EditorPointerKind.Pressed => PointerEventKind.Pressed,
            EditorPointerKind.Moved => PointerEventKind.Moved,
            EditorPointerKind.Released => PointerEventKind.Released,
            EditorPointerKind.Cancelled => PointerEventKind.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        },
        value.CanvasPixel,
        Math.Clamp(value.Pressure, 0d, 1d),
        ToButtons(value.Buttons),
        ToModifiers(value.Modifiers),
        value.TimestampTicks);

    public static IReadOnlyList<ToolOptionPresentation> DescribeOptions(ToolHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host.ActiveTool.Descriptor.Options.Definitions
            .Select(definition => new ToolOptionPresentation(
                definition.Id,
                definition.DisplayName,
                definition.Kind switch
                {
                    ToolOptionKind.Boolean => ToolOptionPresentationKind.Boolean,
                    ToolOptionKind.Integer => ToolOptionPresentationKind.Integer,
                    ToolOptionKind.Enum => ToolOptionPresentationKind.Enum,
                    _ => throw new ArgumentOutOfRangeException(),
                },
                definition.Kind switch
                {
                    ToolOptionKind.Boolean => host.Options.GetBoolean(definition.Id),
                    ToolOptionKind.Integer => host.Options.GetInteger(definition.Id),
                    ToolOptionKind.Enum => host.Options.GetEnum(definition.Id),
                    _ => throw new ArgumentOutOfRangeException(),
                },
                definition.Minimum,
                definition.Maximum,
                definition.AllowedValues))
            .ToArray();
    }

    private static PointerButtons ToButtons(EditorPointerButtons buttons)
    {
        var result = PointerButtons.None;
        if ((buttons & EditorPointerButtons.Primary) != 0) result |= PointerButtons.Primary;
        if ((buttons & EditorPointerButtons.Secondary) != 0) result |= PointerButtons.Secondary;
        if ((buttons & EditorPointerButtons.Middle) != 0) result |= PointerButtons.Middle;
        if ((buttons & EditorPointerButtons.Barrel) != 0) result |= PointerButtons.Barrel;
        if ((buttons & EditorPointerButtons.Eraser) != 0) result |= PointerButtons.Eraser;
        return result;
    }

    private static KeyModifiers ToModifiers(EditorInputModifiers modifiers)
    {
        var result = KeyModifiers.None;
        if ((modifiers & EditorInputModifiers.Shift) != 0) result |= KeyModifiers.Shift;
        if ((modifiers & EditorInputModifiers.Control) != 0) result |= KeyModifiers.Control;
        if ((modifiers & EditorInputModifiers.Alt) != 0) result |= KeyModifiers.Alt;
        if ((modifiers & EditorInputModifiers.Meta) != 0) result |= KeyModifiers.Meta;
        return result;
    }
}
