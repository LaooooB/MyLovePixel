using Avalonia.Controls;
using Lucide.Avalonia;

namespace MyLovePixel.Desktop;

internal static class UiIcons
{
    public static bool TryResolve(string label, string legacyGlyph, out LucideIconKind kind)
    {
        var value = label.Trim().ToLowerInvariant();

        kind = value switch
        {
            var v when v.StartsWith("new") => LucideIconKind.FilePlus,
            var v when v.StartsWith("open") => LucideIconKind.FolderOpen,
            var v when v.StartsWith("save as") => LucideIconKind.FileDown,
            var v when v.StartsWith("save") => LucideIconKind.Save,
            var v when v.StartsWith("import") => LucideIconKind.FileInput,
            var v when v.StartsWith("export") => LucideIconKind.Download,
            var v when v.StartsWith("undo") => LucideIconKind.Undo2,
            var v when v.StartsWith("redo") => LucideIconKind.Redo2,
            var v when v.StartsWith("zoom in") => LucideIconKind.ZoomIn,
            var v when v.StartsWith("zoom out") => LucideIconKind.ZoomOut,
            var v when v.Contains("pixel grid") => LucideIconKind.Grid3x3,
            var v when v.Contains("invert black") => LucideIconKind.Contrast,

            var v when v == "selection" => LucideIconKind.MousePointer2,
            var v when v == "rectangle" => LucideIconKind.SquareDashedMousePointer,
            var v when v == "ellipse" => LucideIconKind.CircleDashed,
            var v when v == "lasso" => LucideIconKind.Lasso,
            var v when v == "by color" => LucideIconKind.Pipette,
            var v when v.StartsWith("select all") => LucideIconKind.Scan,
            var v when v.StartsWith("invert selection") => LucideIconKind.Combine,
            var v when v.StartsWith("clear selection") => LucideIconKind.X,
            var v when v.StartsWith("move left") => LucideIconKind.ArrowLeft,
            var v when v.StartsWith("move right") => LucideIconKind.ArrowRight,
            var v when v.StartsWith("move up") => LucideIconKind.ArrowUp,
            var v when v.StartsWith("move down") => LucideIconKind.ArrowDown,
            var v when v.Contains("flip horizontal") => LucideIconKind.FlipHorizontal2,
            var v when v.Contains("flip vertical") => LucideIconKind.FlipVertical2,
            var v when v.Contains("rotate 90") => LucideIconKind.RotateCw,
            var v when v.Contains("scale selection") => LucideIconKind.Expand,

            var v when v == "pencil" => LucideIconKind.Pencil,
            var v when v == "eraser" => LucideIconKind.Eraser,
            var v when v == "line" => LucideIconKind.Minus,
            var v when v == "arc" => LucideIconKind.RotateCcwClock,
            var v when v == "shape" => LucideIconKind.Square,
            var v when v == "fill" => LucideIconKind.PaintBucket,
            var v when v == "blur brush" => LucideIconKind.CircleDashed,
            var v when v == "fade brush" => LucideIconKind.Contrast,
            var v when v == "shadow brush" => LucideIconKind.Layers2,
            var v when v == "highlight brush" => LucideIconKind.Palette,
            var v when v.StartsWith("swap") => LucideIconKind.ArrowLeftRight,
            var v when v.StartsWith("primary") || v.StartsWith("secondary") => LucideIconKind.Palette,

            var v when v.StartsWith("add layer") => LucideIconKind.LayersPlus,
            var v when v.StartsWith("delete layer") => LucideIconKind.LayersMinus,
            var v when v.StartsWith("move layer up") => LucideIconKind.LayerArrowUp,
            var v when v.StartsWith("move layer down") => LucideIconKind.LayerArrowDown,
            var v when v == "hide" || v == "show" => v == "hide" ? LucideIconKind.EyeOff : LucideIconKind.Eye,
            var v when v == "lock" || v == "unlock" => v == "lock" ? LucideIconKind.Lock : LucideIconKind.LockOpen,

            var v when v.StartsWith("add effect") => LucideIconKind.Plus,
            var v when v.StartsWith("remove") || v.StartsWith("delete") || v.StartsWith("dismiss") => LucideIconKind.Trash2,
            var v when v.StartsWith("enable") || v.StartsWith("disable") => LucideIconKind.Power,
            var v when v.Contains("keyframe") => LucideIconKind.Diamond,
            var v when v.StartsWith("edit") => LucideIconKind.PencilLine,

            var v when v.Contains("erase tile") => LucideIconKind.Eraser,
            var v when v.Contains("add tile") => LucideIconKind.Plus,
            var v when v.Contains("add tileset") => LucideIconKind.Plus,
            var v when v.Contains("add tilemap") => LucideIconKind.Plus,
            var v when v.Contains("collect unused") => LucideIconKind.Trash2,
            var v when v.Contains("make selected cell unique") => LucideIconKind.CopyPlus,

            var v when v.StartsWith("play") || v.StartsWith("run ") => LucideIconKind.Play,
            var v when v.Contains("duplicate frame") => LucideIconKind.Copy,
            var v when v.Contains("linked frame") => LucideIconKind.Link2,
            var v when v.Contains("onion skin") => LucideIconKind.Layers2,

            var v when v.StartsWith("load plugin") => LucideIconKind.PlugZap,
            var v when v.StartsWith("unload") => LucideIconKind.Unplug,
            var v when v.StartsWith("recover") => LucideIconKind.RotateCcwClock,

            _ => default,
        };

        if (!EqualityComparer<LucideIconKind>.Default.Equals(kind, default)) return true;

        kind = legacyGlyph switch
        {
            "＋" => LucideIconKind.Plus,
            "×" => LucideIconKind.X,
            "←" => LucideIconKind.ArrowLeft,
            "→" => LucideIconKind.ArrowRight,
            "↑" => LucideIconKind.ArrowUp,
            "↓" => LucideIconKind.ArrowDown,
            "↔" => LucideIconKind.FlipHorizontal2,
            "↕" => LucideIconKind.FlipVertical2,
            "↻" => LucideIconKind.RotateCw,
            "✎" => LucideIconKind.Pencil,
            "⌫" => LucideIconKind.Eraser,
            "▶" => LucideIconKind.Play,
            "◇" => LucideIconKind.Diamond,
            "◆" => LucideIconKind.Puzzle,
            _ => default,
        };
        return !EqualityComparer<LucideIconKind>.Default.Equals(kind, default);
    }

    public static Control Create(LucideIconKind kind, double size = 18) =>
        new LucideIcon { Kind = kind, Size = size, StrokeWidth = 1.8 };

    public static string TextFallback(string label)
    {
        var clean = label.Split('·')[0].Trim();
        if (clean.Length <= 12) return clean;
        var first = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? clean[..12] : first;
    }
}
