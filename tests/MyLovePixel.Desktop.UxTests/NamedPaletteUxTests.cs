using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Desktop;

internal static partial class Program
{
    private static void RunNamedPaletteTests()
    {
        Run("Fixed color editor opens the personal palette name field", w =>
        {
            Find<TextBox>(w, "color.hex").Text = "#654321";
            Click(Find<Button>(w, "palette.open")); Pump();
            Check(Find<TextBox>(w, "palette.name").IsKeyboardFocusWithin && Find<TextBox>(w, "palette.hex").Text == "#654321", "Save shortcut did not reveal the matching name field.");
        });
        Run("Named palette saves exact HEX, visible names and durable colors without editing artwork", w =>
        {
            var s = Session(w); var version = s.DocumentVersion; var undo = s.Commands.UndoCount;
            SaveNamed(w, "#654321", "木头阴影");
            var row = Find<Button>(w, "palette.swatch.654321");
            Check(row.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "木头阴影" && t.TextWrapping == TextWrapping.Wrap), "Custom name is missing or truncated.");
            Check(row.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "#654321"), "HEX is not permanently visible.");
            var store = Field<UserPaletteStore>(w, "_userPaletteStore");
            Check(new UserPaletteStore(store.FilePath).Swatches.Single().Name == "木头阴影", "Name was not persisted.");
            Check(s.DocumentVersion == version && s.Commands.UndoCount == undo && !s.IsDirty, "Palette editing changed artwork history.");
        });
        Run("Saved colors can be renamed in place without duplicating the color", w =>
        {
            SaveNamed(w, "654321", "Wood");
            Click(Find<Button>(w, "palette.swatch.654321")); Click(Find<Button>(w, "palette.rename"));
            var name = Find<TextBox>(w, "palette.name"); Check(name.IsKeyboardFocusWithin, "Rename did not focus the name.");
            name.Text = "深色木头"; Click(Find<Button>(w, "palette.save"));
            var store = Field<UserPaletteStore>(w, "_userPaletteStore");
            Check(store.Swatches.Count == 1 && new UserPaletteStore(store.FilePath).Swatches.Single().Name == "深色木头", "Rename duplicated or lost the color.");
            Check(Find<Button>(w, "palette.swatch.654321").GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "深色木头"), "Renamed label did not update.");
        });
        Run("Saved palette applies to the chosen foreground or background and removal preserves artwork", w =>
        {
            SaveNamed(w, "#1E64C880", "Water"); var s = Session(w);
            Call(w, "SetStudioColorTarget", true); Click(Find<Button>(w, "palette.swatch.1E64C880")); Pump();
            Check(s.GetToolColors().Secondary == new Rgba32(30, 100, 200, 128), "Saved alpha or background target was lost.");
            var colors = s.GetToolColors(); var version = s.DocumentVersion;
            Click(Find<Button>(w, "palette.remove"));
            Check(Field<UserPaletteStore>(w, "_userPaletteStore").Swatches.Count == 0, "Remove did not persist.");
            Check(s.GetToolColors() == colors && s.DocumentVersion == version, "Remove changed paint or artwork.");
        });
        Run("Invalid palette HEX cannot save and typing names cannot switch tools", w =>
        {
            var hex = Find<TextBox>(w, "palette.hex"); var name = Find<TextBox>(w, "palette.name");
            hex.Text = "#invalid"; name.Text = "Pencil";
            Check(!Find<Button>(w, "palette.save").IsEnabled, "Invalid HEX is saveable.");
            name.Focus(); var tool = Session(w).ActiveToolId;
            w.KeyPress(Key.E, RawInputModifiers.None, PhysicalKey.E, null); w.KeyRelease(Key.E, RawInputModifiers.None, PhysicalKey.E, null); Pump();
            Check(Session(w).ActiveToolId == tool, "Typing a name switched the drawing tool.");
            w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); w.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Pump();
            Check(Find<TextBlock>(w, "palette.validation").IsVisible && Field<UserPaletteStore>(w, "_userPaletteStore").Swatches.Count == 0, "Invalid input silently persisted.");
        });
        Run("Main HEX feeds the personal palette until its draft is edited", w =>
        {
            var main = Find<TextBox>(w, "color.hex"); main.Focus(); main.Text = "#654321";
            w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); w.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Pump();
            Check(Find<TextBox>(w, "palette.hex").Text == "#654321", "Programmatic initialization marked the draft as user-edited.");
        });
        Run("Palette drafts survive ordinary editing and main HEX can seed a new named color", w =>
        {
            var hex = Find<TextBox>(w, "palette.hex"); var name = Find<TextBox>(w, "palette.name");
            hex.Text = "#654321"; name.Text = "Draft name";
            Session(w).SetToolColors(new Rgba32(4, 5, 6), Rgba32.Transparent); Pump();
            Check(hex.Text == "#654321" && name.Text == "Draft name", "Color refresh overwrote a palette draft.");
            hex.Focus(); w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); w.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Pump();
            Check(hex.Text == "#040506", "Escape did not restore the current paint color.");
        });
        Run("Preview remains visible on launch and when the window becomes compact", w =>
        {
            var preview = Field<Expander>(w, "_previewExpander"); Check(preview.IsExpanded, "Preview starts hidden.");
            w.Width = 960; w.Height = 640; Pump(); Check(preview.IsExpanded, "Compact sizing hid Preview.");
        });
        Run("Palette is below Color library and named rows remain usable at high DPI", w =>
        {
            SaveNamed(w, "#654321", "Wood shadow"); SaveNamed(w, "#FFB76B", "Warm light"); SaveNamed(w, "#248FC8", "Ocean blue");
            foreach (var scale in new[] { 1d, 1.5d, 2d })
            {
                w.SetRenderScaling(scale); w.Width = 1280 / scale; w.Height = 820 / scale; Pump();
                var row = Find<Button>(w, "palette.swatch.248FC8"); row.BringIntoView(); Pump();
                Check(row.Bounds.Width >= 200 && row.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Ocean blue").Bounds.Width > 80, "Name is clipped.");
                var personal = Find<StackPanel>(w, "palette.personal");
                var library = w.GetVisualDescendants().OfType<Expander>().First(e => e.Header?.ToString() == "Color library");
                Check(library.TranslatePoint(new Point(0, 0), personal)!.Value.Y < 0, "My palette is not under Color library.");
                using var frame = w.CaptureRenderedFrame(); frame!.Save($"release/ui/named-palette-{scale * 100:0}.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
        });
    }

    private static void SaveNamed(MainWindow w, string hex, string name)
    {
        Find<TextBox>(w, "palette.hex").Text = hex;
        Find<TextBox>(w, "palette.name").Text = name;
        Click(Find<Button>(w, "palette.save")); Pump();
    }
}
