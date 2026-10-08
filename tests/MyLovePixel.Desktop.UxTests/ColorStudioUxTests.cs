using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.VisualTree;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Desktop;

internal static partial class Program
{
    private static void RunColorStudioTests()
    {
        Run("Color studio replaces the swatch wall and Photo with dedicated tabs", w =>
        {
            var tabs = Field<TabControl>(w, "_sideTabs");
            var labels = tabs.Items.Cast<TabItem>().Select(t => t.Header?.ToString()).ToArray();
            Check(labels.Contains("Colors") && labels.Contains("Tools") && labels.Length >= 6, "Dedicated Colors and Tools tabs are missing.");
            Check(!labels.Contains("Photo"), "Redundant Photo tab still exists.");
            Check(!w.GetVisualDescendants().OfType<Expander>().Any(e => e.Header?.ToString() == "Color library"), "Huge swatch wall still occupies the sidebar.");
            Check(Find<Button>(w, "color.picker").IsEffectivelyVisible, "Picker is not next to the main HEX input.");
        });
        Run("Color studio keeps a useful palette viewport alongside the timeline", w =>
        {
            w.Width = 1280; w.Height = 820; Pump();
            var store = Field<UserPaletteStore>(w, "_userPaletteStore");
            for (var i = 0; i < 12; i++) store.Add(new Rgba32((byte)(30 + i), 90, 150), "Saved color " + i);
            Session(w).SetToolColors(new Rgba32(120, 70, 40), Rgba32.Transparent); Pump();
            Click(Find<Button>(w, "color.keep"));
            Call(w, "RefreshPersonalRows"); Pump();
            var list = Find<ListBox>(w, "palette.results");
            Check(list.Bounds.Height >= 240, $"The palette is squeezed by the timeline: {list.Bounds.Height:0} pixels.");
            Check(Field<Expander>(w, "_timelineExpander").IsExpanded, "Making room for colors hid the timeline.");
            Check(Find<Button>(w, "preview.enlarge").IsEffectivelyVisible, "The fixed Preview shortcut is missing.");
            using var frame = w.CaptureRenderedFrame();
            frame!.Save("release/ui/color-studio-layout.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        });
        Run("Color studio keeps temporary eyedrop colors without editing artwork", w =>
        {
            var s = Session(w); var revision = s.DocumentVersion; var history = s.Commands.UndoCount;
            s.SetToolColors(new Rgba32(101, 67, 33), Rgba32.Transparent); Pump();
            Click(Find<Button>(w, "color.keep"));
            s.SetToolColors(new Rgba32(0, 128, 255), Rgba32.Transparent); Pump();
            Click(Find<Button>(w, "color.keep"));
            Click(Find<Button>(w, "quick.swatch.654321"));
            Check(s.GetToolColors().Primary == new Rgba32(101, 67, 33), "Temporary color cannot be reused.");
            var store = Field<UserPaletteStore>(w, "_userPaletteStore");
            Check(new UserPaletteStore(store.FilePath).QuickColors.Count == 2, "Temporary slots were not saved across restart.");
            Check(s.DocumentVersion == revision && s.Commands.UndoCount == history, "Temporary colors mutated artwork history.");
        });
        Run("Color studio searches names and HEX and clears the filter", w =>
        {
            SaveNamed(w, "#654321", "木头阴影"); SaveNamed(w, "#0080FF", "海水");
            Click(Find<Button>(w, "palette.editor.close"));
            var search = Find<TextBox>(w, "palette.search"); search.Text = "木头"; Pump();
            Check(Find<Button>(w, "palette.swatch.654321").IsEffectivelyVisible, "Name match missing.");
            Check(!w.GetVisualDescendants().OfType<Button>().Any(b => Avalonia.Automation.AutomationProperties.GetAutomationId(b) == "palette.swatch.0080FF"), "Unmatched color remains realized.");
            search.Text = "0080ff"; Pump(); Check(Find<Button>(w, "palette.swatch.0080FF").IsEffectivelyVisible, "HEX search failed.");
            Click(Find<Button>(w, "palette.search.clear")); Check(search.Text == "", "Clear did not reset search.");
        });

        Run("Color studio folders create rename move and remove without losing colors", w =>
        {
            SaveNamed(w, "#654321", "木头阴影");
            Click(Find<Button>(w, "palette.editor.close"));
            Call(w, "EditColorFolderAsync", new object[] { null! }); Pump();
            var dialog = w.OwnedWindows.Single();
            Find<TextBox>(dialog, "folder.name").Text = "角色";
            Click(Find<Button>(dialog, "folder.save")); Pump();
            var store = Field<UserPaletteStore>(w, "_userPaletteStore");
            var folder = store.Folders.Single();
            Check(folder.Name == "角色", "New folder was not saved.");
            var filter = Find<ComboBox>(w, "palette.folder.filter"); filter.SelectedIndex = 0; Pump();
            Click(Find<Button>(w, "palette.swatch.654321")); Click(Find<Button>(w, "palette.move"));
            dialog = w.OwnedWindows.Single(); Find<ComboBox>(dialog, "folder.move.target").SelectedIndex = 1;
            Click(Find<Button>(dialog, "folder.move.save")); Pump();
            Check(store.Swatches.Single().FolderId == folder.Id, "Color was not moved to its folder.");
            filter.SelectedIndex = 2; Pump();
            Check(Find<Button>(w, "palette.swatch.654321").IsEffectivelyVisible, "Folder does not show its color.");
            Call(w, "EditColorFolderAsync", folder); Pump(); dialog = w.OwnedWindows.Single();
            Find<TextBox>(dialog, "folder.name").Text = "木材"; Click(Find<Button>(dialog, "folder.save")); Pump();
            Check(store.Folders.Single().Name == "木材", "Folder rename not durable.");
            filter.SelectedIndex = 0; Find<TextBox>(w, "palette.search").Text = "木材"; Pump();
            Check(Find<Button>(w, "palette.swatch.654321").IsEffectivelyVisible, "Search does not include folder names.");
            Find<TextBox>(w, "palette.search").Text = ""; filter.SelectedIndex = 2; Pump();
            Click(Find<Button>(w, "palette.folder.manage")); Pump();
            var remove = w.GetVisualDescendants().OfType<MenuItem>().FirstOrDefault(m => m.Header?.ToString() == "Remove folder · keep colors");
            Check(remove is not null, "Folder actions cannot be found in the open menu.");
            remove!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Pump();
            Check(store.Folders.Count == 0 && store.Swatches.Single().FolderId is null, "Folder removal deleted the color.");
            Check(new UserPaletteStore(store.FilePath).Swatches.Single().Name == "木头阴影", "Folder changes lost the existing saved name.");
        });
        Run("Color studio draggable spectrum confirms exact color and cancel restores original", w =>
        {
            var session = Session(w); var initial = new Rgba32(101, 67, 33, 128);
            session.SetToolColors(initial, Rgba32.Transparent); Pump();
            var revision = session.DocumentVersion; var undo = session.Commands.UndoCount;
            Click(Find<Button>(w, "color.picker")); var dialog = w.OwnedWindows.Single();
            var spectrum = Find<Control>(dialog, "color.picker.spectrum");
            var hue = Find<Control>(dialog, "color.picker.hue");
            Point In(Control control, double x, double y) => control.TranslatePoint(new Point(8 + (control.Bounds.Width - 16) * x, 8 + (control.Bounds.Height - 16) * y), dialog)!.Value;
            dialog.MouseDown(In(hue, .5, .5), MouseButton.Left); dialog.MouseUp(In(hue, .5, .5), MouseButton.Left);
            dialog.MouseDown(In(spectrum, .2, .2), MouseButton.Left);
            dialog.MouseMove(In(spectrum, .75, .25)); dialog.MouseUp(In(spectrum, .75, .25), MouseButton.Left); Pump();
            var text = Find<TextBox>(dialog, "color.picker.hex").Text;
            Check(HexColor.TryParse(text, out var picked), "Picker did not display a valid resulting HEX.");
            var expected = new MyLovePixel.Application.HsvColor(179.9995, .75, .75).ToColor(128);
            Check(picked == expected && picked != initial, $"Drag coordinates do not match HSV color: {text}, expected {HexColor.Format(expected)}.");
            Check(session.GetToolColors().Primary == initial, "Unconfirmed color changed the drawing color.");
            using (var frame = dialog.CaptureRenderedFrame()) frame!.Save("release/ui/color-picker.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            Click(Find<Button>(dialog, "color.picker.cancel"));
            Check(session.GetToolColors().Primary == initial, "Cancel failed to preserve drawing color.");
            Click(Find<Button>(w, "color.picker")); dialog = w.OwnedWindows.Single();
            Find<TextBox>(dialog, "color.picker.hex").Text = "#1289AB40";
            Click(Find<Button>(dialog, "color.picker.apply")); Pump();
            Check(session.GetToolColors().Primary == new Rgba32(18,137,171,64), "Picker confirm lost exact HEX or transparency.");
            Check(session.DocumentVersion == revision && session.Commands.UndoCount == undo, "Color picker changed document data.");
        });
        Run("Color studio large saved libraries virtualize and search after scrolling", w =>
        {
            var store = Field<UserPaletteStore>(w, "_userPaletteStore");
            var colors = Enumerable.Range(0, 3000).Select(i => new { hex = $"#{i:X6}", name = $"场景颜色 {i:0000}" }).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
            File.WriteAllText(store.FilePath, System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = 2, colors }));
            store.Reload(); Call(w, "RefreshPersonalRows"); Pump();
            var list = Find<ListBox>(w, "palette.results");
            Check(list.ItemCount == 3000, "Existing palette did not load every color.");
            Check(list.GetVisualDescendants().OfType<Button>().Count() < 40, "Entire palette was realized instead of virtualizing visible rows.");
            list.ScrollIntoView(store.Swatches[^1]); Pump();
            Check(Find<Button>(w, "palette.swatch.000BB7").IsEffectivelyVisible, "Last saved color cannot be reached.");
            var search = Find<TextBox>(w, "palette.search"); search.Text = "场景 0001"; Pump();
            Check(Find<Button>(w, "palette.swatch.000001").IsEffectivelyVisible, "A narrowed multi-result search retained the old scroll position.");
            search.Text = "#000001"; Pump();
            Check(list.ItemCount == 1 && Find<Button>(w, "palette.swatch.000001").IsEffectivelyVisible, "Search from the bottom stranded the matching color offscreen.");
            search.Text = "场景"; Pump();
            Check(Find<Button>(w, "palette.swatch.000000").IsEffectivelyVisible, "A new search kept the old scroll position.");
            search.Text = ""; Pump(); list.ScrollIntoView(store.Swatches[0]); Pump();
            var folder = store.CreateFolder("场景"); store.Move(store.Swatches[0].Color, folder); Call(w, "RefreshPersonalRows");
            Session(w).SetToolColors(new Rgba32(210,80,55), Rgba32.Transparent); Pump(); Click(Find<Button>(w,"color.keep"));
            Session(w).SetToolColors(new Rgba32(25,125,170), Rgba32.Transparent); Pump(); Click(Find<Button>(w,"color.keep"));
            using (var frame = w.CaptureRenderedFrame()) frame!.Save("release/ui/color-studio-library.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            Check(File.Exists(store.FilePath + ".v2.bak"), "Large library upgrade has no original backup.");
        });
        Run("Color studio quick slots reuse actual eyedrop samples and promote to named colors", w =>
        {
            var session = Session(w); var surface = session.CaptureSnapshot().Cels.First().SurfaceId;
            var brown = new Rgba32(101,67,33); var blue = new Rgba32(20,90,220,128);
            session.Execute(new MyLovePixel.Commands.Pixel.PixelPatchCommand(surface, [new MyLovePixel.Core.Pixel.PixelWrite(10,10,brown), new MyLovePixel.Core.Pixel.PixelWrite(20,10,blue)])); Pump();
            var revision = session.DocumentVersion; var undo = session.Commands.UndoCount;
            Click(Find<Button>(w,"tool.core.eyedropper")); var canvas = Field<PixelCanvasView>(w,"_canvas");
            foreach (var x in new[]{10,20})
            {
                var point = canvas.TranslatePoint(new Point((x+.5)*canvas.Zoom,10.5*canvas.Zoom),w)!.Value;
                w.MouseDown(point,MouseButton.Left);w.MouseUp(point,MouseButton.Left);Pump();Click(Find<Button>(w,"color.keep"));
            }
            Click(Find<Button>(w,"quick.swatch.654321")); Check(session.GetToolColors().Primary == brown,"Eyedrop color was not retained in its slot.");
            Click(Find<Button>(w,"quick.save")); Find<TextBox>(w,"palette.name").Text = "临时木色"; Click(Find<Button>(w,"palette.save"));
            var store = Field<UserPaletteStore>(w,"_userPaletteStore");
            Check(store.Swatches.Single().Name == "临时木色" && store.QuickColors.Count == 2,"Promoting a quick color lost slots or its name.");
            Check(session.DocumentVersion == revision && session.Commands.UndoCount == undo,"Sampling or keeping colors changed artwork.");
        });
        Run("Color studio saves return to the list with the saved name clickable", w =>
        {
            SaveNamed(w, "#654321", "Wood shadow");
            Check(!Field<Border>(w, "_personalEditorHost").IsVisible, "Saving leaves the large form obscuring the colors.");
            var row = Find<Button>(w, "palette.swatch.654321");
            var point = row.TranslatePoint(new Point(row.Bounds.Width/2,row.Bounds.Height/2), w)!.Value;
            var hit = w.InputHitTest(point) as Visual;
            Check(hit is not null && (hit == row || hit.GetVisualAncestors().Contains(row)), "The saved color cannot be clicked after saving.");
        });
        Run("Color studio compact layouts keep search and picker reachable", w =>
        {
            SaveNamed(w, "#654321", "中文颜色长名称"); Click(Find<Button>(w,"palette.editor.close"));
            foreach(var scale in new[]{1d,1.25,1.5,2d})
            {
                w.SetRenderScaling(scale); w.Width=1280/scale;w.Height=800/scale;Pump();
                var picker = Find<Button>(w,"color.picker");var search = Find<TextBox>(w,"palette.search");
                foreach(var control in new Control[]{picker,search,Find<Button>(w,"preview.enlarge")})
                {
                    control.BringIntoView();Pump();
                    var point=control.TranslatePoint(new Point(control.Bounds.Width/2,control.Bounds.Height/2),w)!.Value;
                    var hit=w.InputHitTest(point) as Visual;
                    Check(hit is not null && (hit==control || hit.GetVisualAncestors().Contains(control)),"Core color control cannot be clicked at scale "+scale);
                }
                Find<Button>(w,"color.picker").BringIntoView();Pump();
                using(var frame=w.CaptureRenderedFrame())frame!.Save($"release/ui/color-studio-dpi-{scale*100:0}.png",Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
        });
    }
}
