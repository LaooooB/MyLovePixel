using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Tools;

public enum PointerDeviceKind
{
    Unknown = 0,
    Mouse = 1,
    Pen = 2,
    Touch = 3,
}

public enum PointerEventKind
{
    Pressed = 1,
    Moved = 2,
    Released = 3,
    Cancelled = 4,
}

[Flags]
public enum PointerButtons
{
    None = 0,
    Primary = 1 << 0,
    Secondary = 1 << 1,
    Middle = 1 << 2,
    Barrel = 1 << 3,
    Eraser = 1 << 4,
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2,
    Meta = 1 << 3,
}

public readonly record struct PointerEvent(
    long PointerId,
    PointerDeviceKind DeviceKind,
    PointerEventKind Kind,
    IntPoint CanvasPixel,
    double Pressure,
    PointerButtons Buttons,
    KeyModifiers Modifiers,
    long TimestampTicks)
{
    public PointerEvent WithModifiers(KeyModifiers modifiers) =>
        this with { Modifiers = modifiers };
}
