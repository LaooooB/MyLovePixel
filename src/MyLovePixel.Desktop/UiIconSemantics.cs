using Avalonia.Controls;
using Avalonia.Media;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace MyLovePixel.Desktop;

internal static class UiIconSemantics
{
    // Iconsax V1 Free / Linear geometry from Vuesax/iconsax.
    // This layer handles application-specific semantics that should not collapse
    // onto generic Edit/Layer glyphs in the base UiIcons catalog.
    private const string ArrowUp = "M18.0701 9.57L12.0001 3.5L5.93005 9.57 M12 20.4999V3.66992";
    private const string ArrowDown = "M19.9201 8.94995L13.4001 15.47C12.6301 16.24 11.3701 16.24 10.6001 15.47L4.08008 8.94995";
    private const string Link = "M13.5 12C13.5 15.18 10.93 17.75 7.75 17.75C4.57 17.75 2 15.18 2 12C2 8.82 4.57 6.25 7.75 6.25 M10 12C10 8.69 12.69 6 16 6C19.31 6 22 8.69 22 12C22 15.31 19.31 18 16 18";
    private const string MagicPen = "M3.5 20.4999C4.33 21.3299 5.67 21.3299 6.5 20.4999L19.5 7.49994C20.33 6.66994 20.33 5.32994 19.5 4.49994C18.67 3.66994 17.33 3.66994 16.5 4.49994L3.5 17.4999C2.67 18.3299 2.67 19.6699 3.5 20.4999Z M18.01 8.98999L15.01 5.98999 M8.5 2.44L10 2L9.56 3.5L10 5L8.5 4.56L7 5L7.44 3.5L7 2L8.5 2.44Z M4.5 8.44L6 8L5.56 9.5L6 11L4.5 10.56L3 11L3.44 9.5L3 8L4.5 8.44Z M19.5 13.44L21 13L20.56 14.5L21 16L19.5 15.56L18 16L18.44 14.5L18 13L19.5 13.44Z";
    private const string GridEdit = "M22 11V9C22 4 20 2 15 2H9C4 2 2 4 2 9V15C2 20 4 22 9 22H10 M2.03003 8.5H22 M2.03003 15.5H12 M8.51001 21.99V2.01001 M15.51 11.99V2.01001 M18.73 14.6701L14.58 18.82C14.42 18.98 14.27 19.29 14.23 19.51L14 21.1C13.92 21.67 14.32 22.08 14.89 21.99L16.48 21.76C16.7 21.73 17.01 21.5701 17.17 21.4101L21.32 17.26C22.03 16.55 22.37 15.7101 21.32 14.6601C20.28 13.6201 19.45 13.9501 18.73 14.6701Z M18.14 15.26C18.49 16.52 19.48 17.5 20.74 17.86";
    private const string Location = "M12 13.4299C13.7231 13.4299 15.12 12.0331 15.12 10.3099C15.12 8.58681 13.7231 7.18994 12 7.18994C10.2769 7.18994 8.88 8.58681 8.88 10.3099C8.88 12.0331 10.2769 13.4299 12 13.4299Z M3.62001 8.49C5.59001 -0.169998 18.42 -0.159997 20.38 8.5C21.53 13.58 18.37 17.88 15.6 20.54C13.59 22.48 10.41 22.48 8.39001 20.54C5.63001 17.88 2.47001 13.57 3.62001 8.49Z";
    private const string Scan = "M2 9V6.5C2 4.01 4.01 2 6.5 2H9 M15 2H17.5C19.99 2 22 4.01 22 6.5V9 M22 16V17.5C22 19.99 19.99 22 17.5 22H16 M9 22H6.5C4.01 22 2 19.99 2 17.5V15 M17 9.5V14.5C17 16.5 16 17.5 14 17.5H10C8 17.5 7 16.5 7 14.5V9.5C7 7.5 8 6.5 10 6.5H14C16 6.5 17 7.5 17 9.5Z M19 12H5";
    private const string Shield = "M10.49 2.23006L5.50003 4.11006C4.35003 4.54006 3.41003 5.90006 3.41003 7.12006V14.5501C3.41003 15.7301 4.19003 17.2801 5.14003 17.9901L9.44003 21.2001C10.85 22.2601 13.17 22.2601 14.58 21.2001L18.88 17.9901C19.83 17.2801 20.61 15.7301 20.61 14.5501V7.12006C20.61 5.89006 19.67 4.53006 18.52 4.10006L13.53 2.23006C12.68 1.92006 11.32 1.92006 10.49 2.23006Z";
    private const string Flash = "M9.31993 13.28H12.4099V20.48C12.4099 21.54 13.7299 22.04 14.4299 21.24L21.9999 12.64C22.6599 11.89 22.1299 10.72 21.1299 10.72H18.0399V3.51997C18.0399 2.45997 16.7199 1.95997 16.0199 2.75997L8.44994 11.36C7.79994 12.11 8.32993 13.28 9.31993 13.28Z M8.5 4H1.5 M7.5 20H1.5 M4.5 12H1.5";
    private const string Gallery = "M9 22H15C20 22 22 20 22 15V9C22 4 20 2 15 2H9C4 2 2 4 2 9V15C2 20 4 22 9 22Z M9 10C10.1046 10 11 9.10457 11 8C11 6.89543 10.1046 6 9 6C7.89543 6 7 6.89543 7 8C7 9.10457 7.89543 10 9 10Z M2.67004 18.9501L7.60004 15.6401C8.39004 15.1101 9.53004 15.1701 10.24 15.7801L10.57 16.0701C11.35 16.7401 12.61 16.7401 13.39 16.0701L17.55 12.5001C18.33 11.8301 19.59 11.8301 20.37 12.5001L22 13.9001";
    private const string RefreshCircle = "M12 22C17.5228 22 22 17.5228 22 12C22 6.47715 17.5228 2 12 2C6.47715 2 2 6.47715 2 12C2 17.5228 6.47715 22 12 22Z M8.01001 14.5101C8.19001 14.8101 8.41 15.0901 8.66 15.3401C10.5 17.1801 13.49 17.1801 15.34 15.3401C16.09 14.5901 16.52 13.64 16.66 12.67 M7.33997 11.3301C7.47997 10.3501 7.90997 9.41003 8.65997 8.66003C10.5 6.82003 13.49 6.82003 15.34 8.66003C15.6 8.92003 15.81 9.20005 15.99 9.49005 M7.81995 17.18V14.51H10.4899 M16.18 6.82007V9.49005H13.51";
    private const string TickCircle = "M12 22C17.5 22 22 17.5 22 12C22 6.5 17.5 2 12 2C6.5 2 2 6.5 2 12C2 17.5 6.5 22 12 22Z M7.75 12L10.58 14.83L16.25 9.17004";
    private const string CloseCircle = "M12 22C17.5 22 22 17.5 22 12C22 6.5 17.5 2 12 2C6.5 2 2 6.5 2 12C2 17.5 6.5 22 12 22Z M9.16998 14.83L14.83 9.17004 M14.83 14.83L9.16998 9.17004";

    public static bool TryCreate(string label, string legacyGlyph, double size, out Control icon)
    {
        var value = (label ?? string.Empty).Trim().ToLowerInvariant();

        if ((value.StartsWith("move ") && value.EndsWith(" up")) || legacyGlyph == "↑")
            return GeometryIcon(ArrowUp, size, out icon);
        if ((value.StartsWith("move ") && value.EndsWith(" down")) || legacyGlyph == "↓")
            return GeometryIcon(ArrowDown, size, out icon);

        if (value == "flipx" || value == "flipy")
            return Existing(IconsaxIconKind.Flip, size, out icon);
        if (value == "rotate90")
            return Existing(IconsaxIconKind.Rotate, size, out icon);

        if (value.StartsWith("duplicate frame"))
            return Existing(IconsaxIconKind.Onion, size, out icon);
        if (value.StartsWith("linked frame") || value.StartsWith("linked copy"))
            return GeometryIcon(Link, size, out icon);

        if (value.StartsWith("enable effect"))
            return GeometryIcon(TickCircle, size, out icon);
        if (value.StartsWith("disable effect"))
            return GeometryIcon(CloseCircle, size, out icon);
        if (value.StartsWith("bake effect"))
            return GeometryIcon(MagicPen, size, out icon);

        if (value == "autotile" || value.Contains("visible 8×8 tile viewport"))
            return GeometryIcon(GridEdit, size, out icon);
        if (value.Contains("make selected cell unique"))
            return Existing(IconsaxIconKind.Onion, size, out icon);
        if (value.Contains("collect unused tiles"))
            return Existing(IconsaxIconKind.Trash, size, out icon);

        if (value.StartsWith("set pivot"))
            return GeometryIcon(Location, size, out icon);
        if (value.StartsWith("edit hitbox"))
            return GeometryIcon(Scan, size, out icon);
        if (value.StartsWith("edit hurtbox"))
            return GeometryIcon(Shield, size, out icon);
        if (value.StartsWith("edit socket"))
            return GeometryIcon(Link, size, out icon);
        if (value.StartsWith("edit event"))
            return GeometryIcon(Flash, size, out icon);
        if (value.StartsWith("edit color cycle"))
            return Existing(IconsaxIconKind.ColorFilter, size, out icon);

        if (value.StartsWith("choose an image") || value.StartsWith("choose photo"))
            return GeometryIcon(Gallery, size, out icon);
        if (value.StartsWith("recover ") || value == "recover")
            return GeometryIcon(RefreshCircle, size, out icon);

        if (value.StartsWith("apply ") && value.Contains("using selected palette"))
            return Existing(IconsaxIconKind.Effects, size, out icon);
        if (value.StartsWith("apply ") && value.Contains("selected palette"))
            return Existing(IconsaxIconKind.ColorSwatch, size, out icon);

        if (value.StartsWith("select all"))
            return Existing(IconsaxIconKind.SelectionRectangle, size, out icon);
        if (value.StartsWith("invert selection"))
            return Existing(IconsaxIconKind.ColorFilter, size, out icon);

        if (value.StartsWith("unload ") || value.StartsWith("dismiss recovery"))
            return Existing(IconsaxIconKind.Trash, size, out icon);

        icon = null!;
        return false;
    }

    private static bool Existing(IconsaxIconKind kind, double size, out Control icon)
    {
        icon = UiIcons.Create(kind, size);
        return true;
    }

    private static bool GeometryIcon(string data, double size, out Control icon)
    {
        icon = new ShapePath
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
        return true;
    }
}
