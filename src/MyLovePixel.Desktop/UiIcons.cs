using Avalonia.Controls;
using Avalonia.Media;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace MyLovePixel.Desktop;

internal enum IconsaxIconKind
{
    None = 0,
    Add,
    FolderOpen,
    Save,
    Import,
    Export,
    Undo,
    Redo,
    ZoomIn,
    ZoomOut,
    Grid,
    Pointer,
    Edit,
    Eraser,
    Line,
    Layer,
    Eye,
    Lock,
    Play,
    Trash,
    ArrowLeft,
    ArrowRight,
}

internal static class UiIcons
{
    // Selected Iconsax V1 Free / Linear vector geometry from Vuesax/iconsax.
    // Only the symbols actually used by MyLovePixel are embedded; the source icon
    // package is not redistributed. All geometry keeps the original 24x24 viewbox
    // and is rendered with the Iconsax Linear 1.5px rounded-stroke language.
    private static readonly IReadOnlyDictionary<IconsaxIconKind, string> GeometryData =
        new Dictionary<IconsaxIconKind, string>
        {
            [IconsaxIconKind.Add] = "M6 12H18 M12 18V6",
            [IconsaxIconKind.FolderOpen] = "M21.67 14.3L21.27 19.3C21.12 20.83 21 22 18.29 22H5.71001C3.00001 22 2.88001 20.83 2.73001 19.3L2.33001 14.3C2.25001 13.47 2.51001 12.7 2.98001 12.11C2.99001 12.1 2.99001 12.1 3.00001 12.09C3.55001 11.42 4.38001 11 5.31001 11H18.69C19.62 11 20.44 11.42 20.98 12.07C20.99 12.08 21 12.09 21 12.1C21.49 12.69 21.76 13.46 21.67 14.3Z M3.5 11.43V6.28003C3.5 2.88003 4.35 2.03003 7.75 2.03003H9.02C10.29 2.03003 10.58 2.41003 11.06 3.05003L12.33 4.75003C12.65 5.17003 12.84 5.43003 13.69 5.43003H16.24C19.64 5.43003 20.49 6.28003 20.49 9.68003V11.47 M9.42993 17H14.5699",
            [IconsaxIconKind.Save] = "M12.89 5.87988H5.10999C3.39999 5.87988 2 7.27987 2 8.98987V20.3499C2 21.7999 3.04 22.4199 4.31 21.7099L8.23999 19.5199C8.65999 19.2899 9.34 19.2899 9.75 19.5199L13.68 21.7099C14.95 22.4199 15.99 21.7999 15.99 20.3499V8.98987C16 7.27987 14.6 5.87988 12.89 5.87988Z M22 5.10999V16.47C22 17.92 20.96 18.53 19.69 17.83L16 15.77V8.98999C16 7.27999 14.6 5.88 12.89 5.88H8V5.10999C8 3.39999 9.39999 2 11.11 2H18.89C20.6 2 22 3.39999 22 5.10999Z",
            [IconsaxIconKind.Import] = "M12 2C6.48 2 2 6.48 2 12C2 17.52 6.48 22 12 22C17.52 22 22 17.52 22 12 M22 2L13.8 10.2 M13 6.17004V11H17.83",
            [IconsaxIconKind.Export] = "M16.44 8.8999C20.04 9.2099 21.51 11.0599 21.51 15.1099V15.2399C21.51 19.7099 19.72 21.4999 15.25 21.4999H8.73998C4.26998 21.4999 2.47998 19.7099 2.47998 15.2399V15.1099C2.47998 11.0899 3.92998 9.2399 7.46998 8.9099 M12 15.0001V3.62012 M15.35 5.85L12 2.5L8.65002 5.85",
            [IconsaxIconKind.Undo] = "M7.12988 18.3101H15.1299C17.8899 18.3101 20.1299 16.0701 20.1299 13.3101C20.1299 10.5501 17.8899 8.31006 15.1299 8.31006H4.12988 M6.43012 10.8099L3.87012 8.24994L6.43012 5.68994",
            [IconsaxIconKind.Redo] = "M16.8701 18.3101H8.87012C6.11012 18.3101 3.87012 16.0701 3.87012 13.3101C3.87012 10.5501 6.11012 8.31006 8.87012 8.31006H19.8701 M17.5701 10.8099L20.1301 8.24994L17.5701 5.68994",
            [IconsaxIconKind.ZoomIn] = "M9.19995 11.7H14.2 M11.7 14.2V9.19995 M11.5 21C16.7467 21 21 16.7467 21 11.5C21 6.25329 16.7467 2 11.5 2C6.25329 2 2 6.25329 2 11.5C2 16.7467 6.25329 21 11.5 21Z M22 22L20 20",
            [IconsaxIconKind.ZoomOut] = "M9.19995 11.7H14.2 M11.5 21C16.7467 21 21 16.7467 21 11.5C21 6.25329 16.7467 2 11.5 2C6.25329 2 2 6.25329 2 11.5C2 16.7467 6.25329 21 11.5 21Z M22 22L20 20",
            [IconsaxIconKind.Grid] = "M9 22H15C20 22 22 20 22 15V9C22 4 20 2 15 2H9C4 2 2 4 2 9V15C2 20 4 22 9 22Z M12 2V22 M2 9.5H12 M12 14.5H22",
            [IconsaxIconKind.Pointer] = "M12 22C16.13 22 19.5 18.63 19.5 14.5V9.5C19.5 5.37 16.13 2 12 2C7.87 2 4.5 5.37 4.5 9.5V14.5C4.5 18.63 7.87 22 12 22Z M12 11C11.17 11 10.5 10.33 10.5 9.5V7.5C10.5 6.67 11.17 6 12 6C12.82 6 13.5 6.67 13.5 7.5V9.5C13.5 10.33 12.82 11 12 11Z M12 6V2",
            [IconsaxIconKind.Edit] = "M13.26 3.59997L5.04997 12.29C4.73997 12.62 4.43997 13.27 4.37997 13.72L4.00997 16.96C3.87997 18.13 4.71997 18.93 5.87997 18.73L9.09997 18.18C9.54997 18.1 10.18 17.77 10.49 17.43L18.7 8.73997C20.12 7.23997 20.76 5.52997 18.55 3.43997C16.35 1.36997 14.68 2.09997 13.26 3.59997Z M11.89 5.05005C12.32 7.81005 14.56 9.92005 17.34 10.2 M3 22H21",
            [IconsaxIconKind.Eraser] = "M9 22H15C20 22 22 20 22 15V9C22 4 20 2 15 2H9C4 2 2 4 2 9V15C2 20 4 22 9 22Z M6.98994 15.08L8.92993 17.02C9.56993 17.66 10.6299 17.66 11.2699 17.02L17.0199 11.27C17.6599 10.63 17.6599 9.57 17.0199 8.93L15.0799 6.99001C14.4399 6.35001 13.3799 6.35001 12.7399 6.99001L6.98994 12.74C6.33994 13.38 6.33994 14.43 6.98994 15.08Z M9.31006 10.4199L13.5801 14.6899",
            [IconsaxIconKind.Line] = "M6 12H18",
            [IconsaxIconKind.Layer] = "M13.01 2.92007L18.91 5.54007C20.61 6.29007 20.61 7.53007 18.91 8.28007L13.01 10.9001C12.34 11.2001 11.24 11.2001 10.57 10.9001L4.67 8.28007C2.97 7.53007 2.97 6.29007 4.67 5.54007L10.57 2.92007C11.24 2.62007 12.34 2.62007 13.01 2.92007Z M3 11C3 11.84 3.63 12.81 4.4 13.15L11.19 16.17C11.71 16.4 12.3 16.4 12.81 16.17L19.6 13.15C20.37 12.81 21 11.84 21 11 M3 16C3 16.93 3.55 17.77 4.4 18.15L11.19 21.17C11.71 21.4 12.3 21.4 12.81 21.17L19.6 18.15C20.45 17.77 21 16.93 21 16",
            [IconsaxIconKind.Eye] = "M15.58 12C15.58 13.98 13.98 15.58 12 15.58C10.02 15.58 8.42004 13.98 8.42004 12C8.42004 10.02 10.02 8.42004 12 8.42004C13.98 8.42004 15.58 10.02 15.58 12Z M12 20.27C15.53 20.27 18.82 18.19 21.11 14.59C22.01 13.18 22.01 10.81 21.11 9.39997C18.82 5.79997 15.53 3.71997 12 3.71997C8.46997 3.71997 5.17997 5.79997 2.88997 9.39997C1.98997 10.81 1.98997 13.18 2.88997 14.59C5.17997 18.19 8.46997 20.27 12 20.27Z",
            [IconsaxIconKind.Lock] = "M6 10V8C6 4.69 7 2 12 2C17 2 18 4.69 18 8V10 M17 22H7C3 22 2 21 2 17V15C2 11 3 10 7 10H17C21 10 22 11 22 15V17C22 21 21 22 17 22Z M15.9965 16H16.0054 M11.9955 16H12.0045 M7.99451 16H8.00349",
            [IconsaxIconKind.Play] = "M4 11.9999V8.43989C4 4.01989 7.13 2.2099 10.96 4.4199L14.05 6.1999L17.14 7.9799C20.97 10.1899 20.97 13.8099 17.14 16.0199L14.05 17.7999L10.96 19.5799C7.13 21.7899 4 19.9799 4 15.5599V11.9999Z",
            [IconsaxIconKind.Trash] = "M21 5.97998C17.67 5.64998 14.32 5.47998 10.98 5.47998C9 5.47998 7.02 5.57998 5.04 5.77998L3 5.97998 M8.5 4.97L8.72 3.66C8.88 2.71 9 2 10.69 2H13.31C15 2 15.13 2.75 15.28 3.67L15.5 4.97 M18.85 9.14001L18.2 19.21C18.09 20.78 18 22 15.21 22H8.79002C6.00002 22 5.91002 20.78 5.80002 19.21L5.15002 9.14001 M10.33 16.5H13.66 M9.5 12.5H14.5",
            [IconsaxIconKind.ArrowLeft] = "M9.57 5.92993L3.5 11.9999L9.57 18.0699 M20.5 12H3.67004",
            [IconsaxIconKind.ArrowRight] = "M14.4301 5.92993L20.5001 11.9999L14.4301 18.0699 M3.5 12H20.33",
        };

    public static bool TryResolve(string label, string legacyGlyph, out IconsaxIconKind kind)
    {
        var value = label.Trim().ToLowerInvariant();
        kind = value switch
        {
            var v when v.StartsWith("new") => IconsaxIconKind.Add,
            var v when v.StartsWith("open") => IconsaxIconKind.FolderOpen,
            var v when v.StartsWith("save") => IconsaxIconKind.Save,
            var v when v.StartsWith("import") => IconsaxIconKind.Import,
            var v when v.StartsWith("export") => IconsaxIconKind.Export,
            var v when v.StartsWith("undo") => IconsaxIconKind.Undo,
            var v when v.StartsWith("redo") => IconsaxIconKind.Redo,
            var v when v.StartsWith("zoom in") => IconsaxIconKind.ZoomIn,
            var v when v.StartsWith("zoom out") => IconsaxIconKind.ZoomOut,
            var v when v.Contains("pixel grid") => IconsaxIconKind.Grid,
            var v when v.Contains("invert black") => IconsaxIconKind.Layer,
            var v when v == "selection" || v == "rectangle" || v == "ellipse" || v == "lasso" || v == "by color" => IconsaxIconKind.Pointer,
            var v when v.StartsWith("select all") || v.StartsWith("invert selection") => IconsaxIconKind.Pointer,
            var v when v.StartsWith("clear selection") => IconsaxIconKind.Eraser,
            var v when v.StartsWith("move left") => IconsaxIconKind.ArrowLeft,
            var v when v.StartsWith("move right") => IconsaxIconKind.ArrowRight,
            var v when v.StartsWith("move up") || v.StartsWith("move down") => IconsaxIconKind.Layer,
            var v when v.Contains("flip") || v.Contains("rotate 90") || v.Contains("scale selection") => IconsaxIconKind.Layer,
            var v when v == "pencil" => IconsaxIconKind.Edit,
            var v when v == "eraser" => IconsaxIconKind.Eraser,
            var v when v == "line" => IconsaxIconKind.Line,
            var v when v == "arc" || v == "shape" => IconsaxIconKind.Edit,
            var v when v == "fill" => IconsaxIconKind.Layer,
            var v when v is "blur brush" or "fade brush" or "shadow brush" or "highlight brush" => IconsaxIconKind.Edit,
            var v when v.StartsWith("swap") => IconsaxIconKind.Redo,
            var v when v.StartsWith("primary") || v.StartsWith("secondary") => IconsaxIconKind.Edit,
            var v when v.Contains("layer") || v.Contains("tileset") || v.Contains("tilemap") => IconsaxIconKind.Layer,
            var v when v == "hide" || v == "show" => IconsaxIconKind.Eye,
            var v when v == "lock" || v == "unlock" => IconsaxIconKind.Lock,
            var v when v.StartsWith("add effect") || v.Contains("add tile") => IconsaxIconKind.Add,
            var v when v.StartsWith("remove") || v.StartsWith("delete") || v.StartsWith("dismiss") || v.Contains("collect unused") => IconsaxIconKind.Trash,
            var v when v.StartsWith("enable") || v.StartsWith("disable") || v.Contains("keyframe") => IconsaxIconKind.Layer,
            var v when v.StartsWith("edit") => IconsaxIconKind.Edit,
            var v when v.Contains("erase tile") => IconsaxIconKind.Eraser,
            var v when v.Contains("make selected cell unique") => IconsaxIconKind.Layer,
            var v when v.StartsWith("play") || v.StartsWith("run ") => IconsaxIconKind.Play,
            var v when v.Contains("duplicate frame") || v.Contains("linked frame") || v.Contains("onion skin") => IconsaxIconKind.Layer,
            var v when v.StartsWith("load plugin") || v.StartsWith("unload") => IconsaxIconKind.Layer,
            var v when v.StartsWith("recover") => IconsaxIconKind.Undo,
            _ => IconsaxIconKind.None,
        };

        if (kind != IconsaxIconKind.None) return true;

        kind = legacyGlyph switch
        {
            "＋" => IconsaxIconKind.Add,
            "×" => IconsaxIconKind.Trash,
            "←" => IconsaxIconKind.ArrowLeft,
            "→" => IconsaxIconKind.ArrowRight,
            "↑" or "↓" or "↔" or "↕" => IconsaxIconKind.Layer,
            "↻" => IconsaxIconKind.Redo,
            "✎" => IconsaxIconKind.Edit,
            "⌫" => IconsaxIconKind.Eraser,
            "▶" => IconsaxIconKind.Play,
            "◇" or "◆" => IconsaxIconKind.Layer,
            _ => IconsaxIconKind.None,
        };
        return kind != IconsaxIconKind.None;
    }

    public static Control Create(IconsaxIconKind kind, double size = 18)
    {
        if (!GeometryData.TryGetValue(kind, out var data))
            return new TextBlock { Text = "·", FontSize = size };

        return new ShapePath
        {
            Data = Geometry.Parse(data),
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Fill = Brushes.Transparent,
            Stroke = EditorThemeTokens.TextPrimary,
            StrokeThickness = 1.5,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
    }

    public static string TextFallback(string label)
    {
        var clean = label.Split('·')[0].Trim();
        if (clean.Length <= 12) return clean;
        var first = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? clean[..12] : first;
    }
}
