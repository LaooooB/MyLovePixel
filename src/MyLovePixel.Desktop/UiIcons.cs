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
    SelectionRectangle,
    SelectionEllipse,
    SelectionLasso,
    Edit,
    Eraser,
    Line,
    Arc,
    Fill,
    Blur,
    Fade,
    Shadow,
    Highlight,
    Layer,
    Tileset,
    Tilemap,
    Keyframe,
    Flip,
    Rotate,
    Scale,
    ColorSwatch,
    ColorFilter,
    Swap,
    Onion,
    Effects,
    Plugin,
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
            [IconsaxIconKind.SelectionRectangle] = "M9.9 19H19V9.9C19 6 18 5 14.1 5H5V14.1C5 18 6 19 9.9 19Z M5 5V2 M5 5H2 M19 19V22 M19 19H22",
            [IconsaxIconKind.SelectionEllipse] = "M13.43 15H4.4C2.58 15 1.42 13.05 2.3 11.45L4.63 7.20994L6.81 3.23994C7.72 1.58994 10.1 1.58994 11.01 3.23994L13.2 7.20994L14.25 9.11995L15.53 11.45C16.41 13.05 15.25 15 13.43 15Z M22 15.5C22 19.09 19.09 22 15.5 22C11.91 22 9 19.09 9 15.5C9 15.33 9.01 15.17 9.02 15H13.43C15.25 15 16.41 13.05 15.53 11.45L14.25 9.12C14.65 9.04 15.07 9 15.5 9C19.09 9 22 11.91 22 15.5Z",
            [IconsaxIconKind.SelectionLasso] = "M10.75 22.5001H13.27C14.23 22.5001 14.85 21.8201 14.67 20.9901L14.26 19.1802H9.75999L9.35 20.9901C9.17 21.7701 9.85 22.5001 10.75 22.5001Z M14.26 19.1702L15.99 17.6301C16.96 16.7701 17 16.1701 16.23 15.2001L13.18 11.3302C12.54 10.5202 11.49 10.5202 10.85 11.3302L7.8 15.2001C7.03 16.1701 7.02999 16.8001 8.03999 17.6301L9.77 19.1702 M12.01 11.1201V13.6501 M12.52 5H11.52C10.97 5 10.52 4.55 10.52 4V3C10.52 2.45 10.97 2 11.52 2H12.52C13.07 2 13.52 2.45 13.52 3V4C13.52 4.55 13.07 5 12.52 5Z M3.27 14.17H4.27C4.82 14.17 5.27 13.72 5.27 13.17V12.17C5.27 11.62 4.82 11.1699 4.27 11.1699H3.27C2.72 11.1699 2.27 11.62 2.27 12.17V13.17C2.27 13.72 2.72 14.17 3.27 14.17Z M20.73 14.17H19.73C19.18 14.17 18.73 13.72 18.73 13.17V12.17C18.73 11.62 19.18 11.1699 19.73 11.1699H20.73C21.28 11.1699 21.73 11.62 21.73 12.17V13.17C21.73 13.72 21.28 14.17 20.73 14.17Z M10.52 3.56006C6.71 4.01006 3.75 7.24004 3.75 11.17 M20.25 11.17C20.25 7.25004 17.31 4.03006 13.52 3.56006",
            [IconsaxIconKind.Edit] = "M13.26 3.59997L5.04997 12.29C4.73997 12.62 4.43997 13.27 4.37997 13.72L4.00997 16.96C3.87997 18.13 4.71997 18.93 5.87997 18.73L9.09997 18.18C9.54997 18.1 10.18 17.77 10.49 17.43L18.7 8.73997C20.12 7.23997 20.76 5.52997 18.55 3.43997C16.35 1.36997 14.68 2.09997 13.26 3.59997Z M11.89 5.05005C12.32 7.81005 14.56 9.92005 17.34 10.2 M3 22H21",
            [IconsaxIconKind.Eraser] = "M9 22H15C20 22 22 20 22 15V9C22 4 20 2 15 2H9C4 2 2 4 2 9V15C2 20 4 22 9 22Z M6.98994 15.08L8.92993 17.02C9.56993 17.66 10.6299 17.66 11.2699 17.02L17.0199 11.27C17.6599 10.63 17.6599 9.57 17.0199 8.93L15.0799 6.99001C14.4399 6.35001 13.3799 6.35001 12.7399 6.99001L6.98994 12.74C6.33994 13.38 6.33994 14.43 6.98994 15.08Z M9.31006 10.4199L13.5801 14.6899",
            [IconsaxIconKind.Line] = "M6 12H18",
            [IconsaxIconKind.Arc] = "M2.06999 4.59988C2.86999 1.13988 8.07999 1.13988 8.86999 4.59988C9.33999 6.62988 8.04999 8.34988 6.92999 9.41988C6.10999 10.1999 4.81999 10.1899 3.99999 9.41988C2.88999 8.34988 1.59999 6.62988 2.06999 4.59988Z M15.07 16.5999C15.87 13.1399 21.11 13.1399 21.91 16.5999C22.38 18.6299 21.09 20.3499 19.96 21.4199C19.14 22.1999 17.84 22.1899 17.02 21.4199C15.89 20.3499 14.6 18.6299 15.07 16.5999Z M12 5H14.68C16.53 5 17.39 7.29 16 8.51L8.01001 15.5C6.62001 16.71 7.48001 19 9.32001 19H12 M5.48622 5.5H5.49777 M18.4862 17.5H18.4978",
            [IconsaxIconKind.Fill] = "M3.77 15.56L7.23 19.02C9.66 21.45 10.49 21.41 12.89 19.02L18.46 13.45C20.4 11.51 20.89 10.22 18.46 7.78996L15 4.32996C12.41 1.73996 11.28 2.38996 9.34 4.32996L3.77 9.89996C1.38 12.3 1.18 12.97 3.77 15.56Z M19.2 16.79L18.54 17.88C17.61 19.43 18.33 20.7 20.14 20.7C21.95 20.7 22.67 19.43 21.74 17.88L21.08 16.79C20.56 15.93 19.71 15.93 19.2 16.79Z M2 12.2401C7.56 10.7301 13.42 10.6801 19 12.1101L19.5 12.2401",
            [IconsaxIconKind.Blur] = "M12.61 2.21C12.25 1.93 11.75 1.93 11.39 2.21C9.49001 3.66 3.87997 8.39 3.90997 13.9C3.90997 18.36 7.54001 22 12.01 22C16.48 22 20.11 18.37 20.11 13.91C20.12 8.48 14.5 3.67 12.61 2.21Z M12 2V22 M12 18.96L19.7 15.22 M12 13.9599L19.37 10.3799 M12 8.96001L17.03 6.51001",
            [IconsaxIconKind.Fade] = "M12.61 2.21C12.25 1.93 11.75 1.93 11.39 2.21C9.49004 3.66 3.88003 8.39 3.91003 13.9C3.91003 18.36 7.54004 22 12.01 22C16.48 22 20.11 18.37 20.11 13.91C20.12 8.48 14.5 3.67 12.61 2.21Z",
            [IconsaxIconKind.Shadow] = "M2.03009 12.42C2.39009 17.57 6.76009 21.76 11.9901 21.99C15.6801 22.15 18.9801 20.43 20.9601 17.72C21.7801 16.61 21.3401 15.87 19.9701 16.12C19.3001 16.24 18.6101 16.29 17.8901 16.26C13.0001 16.06 9.00009 11.97 8.98009 7.13996C8.97009 5.83996 9.24009 4.60996 9.73009 3.48996C10.2701 2.24996 9.62009 1.65996 8.37009 2.18996C4.41009 3.85996 1.70009 7.84996 2.03009 12.42Z",
            [IconsaxIconKind.Highlight] = "M12 18.5C15.5899 18.5 18.5 15.5899 18.5 12C18.5 8.41015 15.5899 5.5 12 5.5C8.41015 5.5 5.5 8.41015 5.5 12C5.5 15.5899 8.41015 18.5 12 18.5Z M19.14 19.14L19.01 19.01 M19.01 4.99L19.14 4.86 M4.86 19.14L4.99 19.01 M12 2.08V2 M12 22V21.92 M2.08 12H2 M22 12H21.92 M4.99 4.99L4.86 4.86",
            [IconsaxIconKind.Layer] = "M13.01 2.92007L18.91 5.54007C20.61 6.29007 20.61 7.53007 18.91 8.28007L13.01 10.9001C12.34 11.2001 11.24 11.2001 10.57 10.9001L4.67 8.28007C2.97 7.53007 2.97 6.29007 4.67 5.54007L10.57 2.92007C11.24 2.62007 12.34 2.62007 13.01 2.92007Z M3 11C3 11.84 3.63 12.81 4.4 13.15L11.19 16.17C11.71 16.4 12.3 16.4 12.81 16.17L19.6 13.15C20.37 12.81 21 11.84 21 11 M3 16C3 16.93 3.55 17.77 4.4 18.15L11.19 21.17C11.71 21.4 12.3 21.4 12.81 21.17L19.6 18.15C20.45 17.77 21 16.93 21 16",
            [IconsaxIconKind.Tileset] = "M22 10.9V4.1C22 2.6 21.36 2 19.77 2H15.73C14.14 2 13.5 2.6 13.5 4.1V10.9C13.5 12.4 14.14 13 15.73 13H19.77C21.36 13 22 12.4 22 10.9Z M22 19.9V18.1C22 16.6 21.36 16 19.77 16H15.73C14.14 16 13.5 16.6 13.5 18.1V19.9C13.5 21.4 14.14 22 15.73 22H19.77C21.36 22 22 21.4 22 19.9Z M10.5 13.1V19.9C10.5 21.4 9.86 22 8.27 22H4.23C2.64 22 2 21.4 2 19.9V13.1C2 11.6 2.64 11 4.23 11H8.27C9.86 11 10.5 11.6 10.5 13.1Z M10.5 4.1V5.9C10.5 7.4 9.86 8 8.27 8H4.23C2.64 8 2 7.4 2 5.9V4.1C2 2.6 2.64 2 4.23 2H8.27C9.86 2 10.5 2.6 10.5 4.1Z",
            [IconsaxIconKind.Tilemap] = "M22 9.00002V15C22 17.5 21.5 19.25 20.38 20.38L14 14L21.73 6.27002C21.91 7.06002 22 7.96002 22 9.00002Z M21.73 6.27L6.26999 21.73C3.25999 21.04 2 18.96 2 15V9C2 4 4 2 9 2H15C18.96 2 21.04 3.26 21.73 6.27Z M20.38 20.38C19.25 21.5 17.5 22 15 22H9.00003C7.96003 22 7.06002 21.91 6.27002 21.73L14 14L20.38 20.38Z M6.24002 7.97997C6.92002 5.04997 11.32 5.04997 12 7.97997C12.39 9.69997 11.31 11.16 10.36 12.06C9.67001 12.72 8.58003 12.72 7.88003 12.06C6.93003 11.16 5.84002 9.69997 6.24002 7.97997Z M9.0946 8.69995H9.10359",
            [IconsaxIconKind.Keyframe] = "M9 22H15C20 22 22 20 22 15V9C22 4 20 2 15 2H9C4 2 2 4 2 9V15C2 20 4 22 9 22Z M16.28 13.61C15.15 14.74 13.53 15.09 12.1 14.64L9.51001 17.22C9.33001 17.41 8.96001 17.53 8.69001 17.49L7.49001 17.33C7.09001 17.28 6.73001 16.9 6.67001 16.51L6.51001 15.31C6.47001 15.05 6.60001 14.68 6.78001 14.49L9.36001 11.91C8.92001 10.48 9.26001 8.86001 10.39 7.73001C12.01 6.11001 14.65 6.11001 16.28 7.73001C17.9 9.34001 17.9 11.98 16.28 13.61Z M10.45 16.28L9.59998 15.42 M13.3945 10.7H13.4035",
            [IconsaxIconKind.Flip] = "M9 22H15C20 22 22 20 22 15V9C22 4 20 2 15 2H9C4 2 2 4 2 9V15C2 20 4 22 9 22Z M10.18 17.1501L7.14001 14.1101 M10.1801 6.8501V17.1501 M13.8199 6.8501L16.8599 9.8901 M13.8199 17.1501V6.8501",
            [IconsaxIconKind.Rotate] = "M9.11008 5.0799C9.98008 4.8199 10.9401 4.6499 12.0001 4.6499C16.7901 4.6499 20.6701 8.5299 20.6701 13.3199C20.6701 18.1099 16.7901 21.9899 12.0001 21.9899C7.21008 21.9899 3.33008 18.1099 3.33008 13.3199C3.33008 11.5399 3.87008 9.8799 4.79008 8.4999 M7.87012 5.32L10.7601 2 M7.87012 5.32007L11.2401 7.78007",
            [IconsaxIconKind.Scale] = "M21 9V3H15 M3 15V21H9 M21 3L13.5 10.5 M10.5 13.5L3 21",
            [IconsaxIconKind.ColorSwatch] = "M10 4.5V18C10 19.08 9.55999 20.07 8.85999 20.79L8.82001 20.83C8.73001 20.92 8.63001 21.01 8.54001 21.08C8.24001 21.34 7.89999 21.54 7.54999 21.68C7.43999 21.73 7.33 21.77 7.22 21.81C6.83 21.94 6.41 22 6 22C5.73 22 5.46001 21.97 5.20001 21.92C5.07001 21.89 4.94 21.86 4.81 21.82C4.65 21.77 4.50001 21.72 4.35001 21.65C4.06 21.51 3.79001 21.35 3.54001 21.16L3.53 21.15C3.4 21.05 3.28001 20.95 3.17001 20.83C3.06001 20.71 2.95 20.59 2.84 20.46C2.65 20.21 2.49001 19.94 2.35001 19.66C2.28 19.49 2.22999 19.34 2.17999 19.19C2.13999 19.06 2.10999 18.93 2.07999 18.8C2.02999 18.54 2 18.27 2 18V4.5C2 3 3 2 4.5 2H7.5C9 2 10 3 10 4.5Z M22 16.5V19.5C22 21 21 22 19.5 22H6C6.41 22 6.83 21.94 7.22 21.81C7.33 21.77 7.43999 21.73 7.54999 21.68C7.89999 21.54 8.24001 21.34 8.54001 21.08C8.63001 21.01 8.73001 20.92 8.82001 20.83L8.85999 20.79L15.66 14H19.5C21 14 22 15 22 16.5Z M18.37 11.2899L15.66 14L8.85999 20.7899C9.55999 20.0699 10 19.08 10 18V8.33995L12.71 5.62996C13.77 4.56996 15.19 4.56996 16.25 5.62996L18.37 7.74996C19.43 8.80996 19.43 10.2299 18.37 11.2899Z M6 19C6.55228 19 7 18.5523 7 18C7 17.4477 6.55228 17 6 17C5.44772 17 5 17.4477 5 18C5 18.5523 5.44772 19 6 19Z",
            [IconsaxIconKind.ColorFilter] = "M14 16C14 17.77 13.23 19.37 12 20.46C10.94 21.42 9.54 22 8 22C4.69 22 2 19.31 2 16C2 13.24 3.88 10.9 6.42 10.21C7.11 11.95 8.59 13.29 10.42 13.79C10.92 13.93 11.45 14 12 14C12.55 14 13.08 13.93 13.58 13.79C13.85 14.47 14 15.22 14 16Z M18 8C18 8.78 17.85 9.53 17.58 10.21C16.89 11.95 15.41 13.29 13.58 13.79C13.08 13.93 12.55 14 12 14C11.45 14 10.92 13.93 10.42 13.79C8.59 13.29 7.11 11.95 6.42 10.21C6.15 9.53 6 8.78 6 8C6 4.69 8.69 2 12 2C15.31 2 18 4.69 18 8Z M22 16C22 19.31 19.31 22 16 22C14.46 22 13.06 21.42 12 20.46C13.23 19.37 14 17.77 14 16C14 15.22 13.85 14.47 13.58 13.79C15.41 13.29 16.89 11.95 17.58 10.21C20.12 10.9 22 13.24 22 16Z",
            [IconsaxIconKind.Swap] = "M20.5 14.99L15.49 20.01 M3.5 14.99H20.5 M3.5 9.00999L8.51 3.98999 M20.5 9.01001H3.5",
            [IconsaxIconKind.Onion] = "M16 12.9V17.1C16 20.6 14.6 22 11.1 22H6.9C3.4 22 2 20.6 2 17.1V12.9C2 9.4 3.4 8 6.9 8H11.1C14.6 8 16 9.4 16 12.9Z M22 6.9V11.1C22 14.6 20.6 16 17.1 16H16V12.9C16 9.4 14.6 8 11.1 8H8V6.9C8 3.4 9.4 2 12.9 2H17.1C20.6 2 22 3.4 22 6.9Z",
            [IconsaxIconKind.Effects] = "M17.29 4.13999L17.22 7.92997C17.21 8.44997 17.54 9.13999 17.96 9.44999L20.44 11.33C22.03 12.53 21.77 14 19.87 14.6L16.64 15.61C16.1 15.78 15.53 16.37 15.39 16.92L14.62 19.86C14.01 22.18 12.49 22.41 11.23 20.37L9.46999 17.52C9.14999 17 8.39 16.61 7.79 16.64L4.45003 16.81C2.06003 16.93 1.38002 15.55 2.94002 13.73L4.92 11.43C5.29 11 5.46 10.2 5.29 9.65998L4.28005 6.42997C3.69005 4.52997 4.75004 3.47999 6.64004 4.09999L9.59005 5.06999C10.09 5.22999 10.84 5.11998 11.26 4.80998L14.34 2.58998C16 1.38998 17.33 2.08999 17.29 4.13999Z M21.91 22L18.88 18.97",
            [IconsaxIconKind.Plugin] = "M8 10L6 12L8 14 M16 10L18 12L16 14 M12 22C17.5228 22 22 17.5228 22 12C22 6.47715 17.5228 2 12 2C6.47715 2 2 6.47715 2 12C2 17.5228 6.47715 22 12 22Z M13 9.66992L11 14.33",
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
            var v when v.Contains("invert black") => IconsaxIconKind.ColorFilter,
            var v when v == "selection" => IconsaxIconKind.Pointer,
            var v when v == "rectangle" => IconsaxIconKind.SelectionRectangle,
            var v when v == "ellipse" => IconsaxIconKind.SelectionEllipse,
            var v when v == "lasso" => IconsaxIconKind.SelectionLasso,
            var v when v == "by color" => IconsaxIconKind.ColorFilter,
            var v when v.StartsWith("select all") => IconsaxIconKind.SelectionRectangle,
            var v when v.StartsWith("invert selection") => IconsaxIconKind.Pointer,
            var v when v.StartsWith("clear selection") => IconsaxIconKind.Eraser,
            var v when v.StartsWith("move left") => IconsaxIconKind.ArrowLeft,
            var v when v.StartsWith("move right") => IconsaxIconKind.ArrowRight,
            var v when v.StartsWith("move up") || v.StartsWith("move down") => IconsaxIconKind.Layer,
            var v when v.Contains("flip") => IconsaxIconKind.Flip,
            var v when v.Contains("rotate 90") => IconsaxIconKind.Rotate,
            var v when v.Contains("scale selection") => IconsaxIconKind.Scale,
            var v when v == "pencil" => IconsaxIconKind.Edit,
            var v when v == "eraser" => IconsaxIconKind.Eraser,
            var v when v == "line" => IconsaxIconKind.Line,
            var v when v == "arc" || v == "shape" => IconsaxIconKind.Arc,
            var v when v == "fill" => IconsaxIconKind.Fill,
            var v when v == "blur brush" => IconsaxIconKind.Blur,
            var v when v == "fade brush" => IconsaxIconKind.Fade,
            var v when v == "shadow brush" => IconsaxIconKind.Shadow,
            var v when v == "highlight brush" => IconsaxIconKind.Highlight,
            var v when v.StartsWith("swap") => IconsaxIconKind.Swap,
            var v when v.StartsWith("primary") || v.StartsWith("secondary") => IconsaxIconKind.ColorSwatch,
            var v when v.Contains("tileset") => IconsaxIconKind.Tileset,
            var v when v.Contains("tilemap") => IconsaxIconKind.Tilemap,
            var v when v.Contains("layer") => IconsaxIconKind.Layer,
            var v when v == "hide" || v == "show" => IconsaxIconKind.Eye,
            var v when v == "lock" || v == "unlock" => IconsaxIconKind.Lock,
            var v when v.StartsWith("add effect") => IconsaxIconKind.Effects,
            var v when v.Contains("add tile") => IconsaxIconKind.Add,
            var v when v.StartsWith("remove") || v.StartsWith("delete") || v.StartsWith("dismiss") || v.Contains("collect unused") => IconsaxIconKind.Trash,
            var v when (v.StartsWith("enable") || v.StartsWith("disable")) && v.Contains("effect") => IconsaxIconKind.Effects,
            var v when v.Contains("keyframe") => IconsaxIconKind.Keyframe,
            var v when v.StartsWith("enable") || v.StartsWith("disable") => IconsaxIconKind.Layer,
            var v when v.StartsWith("edit") => IconsaxIconKind.Edit,
            var v when v.Contains("erase tile") => IconsaxIconKind.Eraser,
            var v when v.Contains("make selected cell unique") => IconsaxIconKind.Tileset,
            var v when v.StartsWith("play") || v.StartsWith("run ") => IconsaxIconKind.Play,
            var v when v.Contains("onion skin") => IconsaxIconKind.Onion,
            var v when v.Contains("duplicate frame") || v.Contains("linked frame") => IconsaxIconKind.Layer,
            var v when v.Contains("plugin") => IconsaxIconKind.Plugin,
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
            "↻" => IconsaxIconKind.Rotate,
            "✎" or "✐" => IconsaxIconKind.Edit,
            "⌁" => IconsaxIconKind.Arc,
            "⌫" => IconsaxIconKind.Eraser,
            "◌" => IconsaxIconKind.Onion,
            "◐" => IconsaxIconKind.Effects,
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
