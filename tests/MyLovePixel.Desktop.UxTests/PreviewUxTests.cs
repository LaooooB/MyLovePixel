using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.VisualTree;
using MyLovePixel.Application;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Pixel;
using SkiaSharp;

internal static partial class Program
{
    private static void RunPreviewUxTests()
    {
        Run("Preview white background covers transparent pixels and all margins", w =>
        {
            var preview = Field<Control>(w, "_quickPreview");
            using var shot = CapturePreview(w, preview);
            foreach (var fraction in new[] { .1, .3, .5, .7, .9 })
            {
                var pixel = shot.GetPixel((int)(shot.Width * fraction), (int)(shot.Height * fraction));
                Check(pixel == SKColors.White, $"Preview contains a checker or non-white background: {pixel}.");
            }
        });
        Run("Preview has independent zoom controls without editing artwork", w =>
        {
            var session = Session(w); var preview = Field<Control>(w, "_quickPreview");
            var editorZoom = session.Zoom; var revision = session.DocumentVersion; var history = session.Commands.UndoCount;
            var initial = PreviewZoom(preview);
            Click(Find<Button>(w, "preview.zoom.in"));
            Check(PreviewZoom(preview) > initial, "Preview did not enlarge.");
            Click(Find<Button>(w, "preview.zoom.out"));
            Check(Math.Abs(PreviewZoom(preview) - initial) < .001, "Preview zoom out did not restore its scale.");
            Click(Find<Button>(w, "preview.zoom.actual"));
            Check(PreviewZoom(preview) == 1, "100% does not mean one source pixel per display unit.");
            Click(Find<Button>(w, "preview.zoom.fit"));
            Check(Math.Abs(PreviewZoom(preview) - initial) < .001, "Fit did not recover the full preview.");
            Check(session.Zoom == editorZoom && session.DocumentVersion == revision && session.Commands.UndoCount == history,
                "Preview controls changed the drawing canvas or undo history.");
        });
        Run("Preview opens a resizable live window while drawing remains usable", w =>
        {
            Click(Find<Button>(w, "preview.enlarge"));
            var large = w.OwnedWindows.Single();
            Check(large.CanResize && large.Bounds.Width >= 600, "Enlarged preview is not a resizable window.");
            Check(w.IsEffectivelyEnabled, "Enlarged preview blocks the editor.");
            Click(Find<Button>(w, "preview.enlarge"));
            Check(w.OwnedWindows.Count() == 1, "Opening preview twice creates duplicate windows.");
            var view = Find<Control>(large, "preview.large.viewport");
            var session = Session(w);
            var color = new Rgba32(230, 40, 20, 255);
            session.Execute(new PixelPatchCommand(session.CaptureSnapshot().Cels.First().SurfaceId,
                [new PixelWrite(32, 32, color)], "Preview live seed"));
            Pump();
            var presentation = (CanvasPresentation)view.GetType().GetProperty("Presentation")!.GetValue(view)!;
            var offset = (32 * presentation.Size.Width + 32) * 4;
            Check(presentation.Rgba.Span[offset] == 230 && presentation.Rgba.Span[offset + 3] == 255, "Floating preview did not follow edits.");
            large.Width = 940; large.Height = 730; Pump();
            Directory.CreateDirectory("release/ui");
            using var shot = large.CaptureRenderedFrame();
            shot!.Save("release/ui/preview-large.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            large.Close(); Pump();
            Click(Find<Button>(w, "preview.enlarge"));
            Check(w.OwnedWindows.Count() == 1, "Floating preview cannot be reopened.");
            w.OwnedWindows.Single().Close();
        });
        Run("Preview white composite preserves alpha and excludes onion skin", w =>
        {
            var s = Session(w); var cel = s.CaptureSnapshot().Cels.First();
            s.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(32, 32, new Rgba32(255, 0, 0, 128))]));
            Pump();
            var preview = Field<Control>(w, "_quickPreview");
            using var image = CapturePreview(w, preview);
            var p = image.GetPixel(image.Width / 2 + 1, image.Height / 2 + 1);
            Check(p.Red == 255 && p.Green is >= 126 and <= 128 && p.Blue is >= 126 and <= 128 && p.Alpha == 255,
                $"Semi-transparent red was not composited over white: {p}.");
            Check(s.GetCanvasPixel(32, 32).A == 128, "White preview changed the artwork's transparency.");
            s.DuplicateCurrentFrame(false); s.EraseCanvasPixel(32, 32); Pump();
            typeof(MyLovePixel.Desktop.MainWindow).GetField("_onionSkin", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(w, true);
            Call(w, "RefreshCanvas", true); Pump();
            using var empty = CapturePreview(w, preview);
            Check(empty.GetPixel(empty.Width / 2 + 1, empty.Height / 2 + 1) == SKColors.White, "Previous-frame onion skin leaked into final preview.");
        });
        Run("Preview zoomed view supports drag pan and focus-safe keyboard navigation", w =>
        {
            Click(Find<Button>(w, "preview.enlarge")); var large = w.OwnedWindows.Single();
            var view = Find<Control>(large, "preview.large.viewport");
            var s = Session(w); var revision = s.DocumentVersion;
            Click(Find<Button>(large, "preview.large.zoom.in"));
            var before = (Vector)view.GetType().GetProperty("PanOffset")!.GetValue(view)!;
            var center = view.TranslatePoint(new Point(view.Bounds.Width / 2, view.Bounds.Height / 2), large)!.Value;
            large.MouseDown(center, MouseButton.Left); large.MouseMove(center + new Vector(25, 20)); large.MouseUp(center + new Vector(25, 20), MouseButton.Left); Pump();
            Check((Vector)view.GetType().GetProperty("PanOffset")!.GetValue(view)! != before, "Enlarged preview cannot be panned.");
            view.Focus(); var initial = PreviewZoom(view);
            large.KeyPress(Key.OemMinus, RawInputModifiers.None, PhysicalKey.Minus, null);
            large.KeyRelease(Key.OemMinus, RawInputModifiers.None, PhysicalKey.Minus, null); Pump();
            Check(PreviewZoom(view) < initial, "Minus key did not zoom the focused preview.");
            Check(s.DocumentVersion == revision, "Panning preview created a drawing edit.");
            large.Close();
        });
        Run("Preview controls remain reachable at high DPI beside named palettes", w =>
        {
            foreach (var scale in new[] { 1d, 1.25, 1.5, 2d })
            {
                w.SetRenderScaling(scale); w.Width = 1280 / scale; w.Height = 800 / scale; Pump();
                var open = Find<Button>(w, "preview.enlarge");
                Check(open.IsEffectivelyVisible && open.Bounds.Width >= 40, "Enlarge preview is clipped at high DPI.");
                var p = open.TranslatePoint(new Point(open.Bounds.Width / 2, open.Bounds.Height / 2), w)!.Value;
                Check(p.Y >= 0 && p.Y < w.Bounds.Height, "Preview controls are outside the window.");
                var hit = w.InputHitTest(p) as Visual;
                Check(hit is not null && (ReferenceEquals(hit, open) || hit.GetVisualAncestors().Contains(open)),
                    "Enlarge preview cannot actually be clicked at this scale.");
                var canvas = Field<ScrollViewer>(w, "_canvasScroll");
                Check(canvas.Bounds.Width >= 150 && canvas.Bounds.Height >= 90, "Preview consumed the work area.");
                using var shot = w.CaptureRenderedFrame();
                shot!.Save($"release/ui/preview-dpi-{scale * 100:0}.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
        });
    }

    private static double PreviewZoom(Control view) =>
        (double)(view.GetType().GetProperty("EffectiveZoom")?.GetValue(view)
            ?? throw new InvalidOperationException("Preview has no independent zoom."));

    private static SKBitmap CapturePreview(Window window, Control view)
    {
        Pump(); using var frame = window.CaptureRenderedFrame();
        using var stream = new MemoryStream();
        frame!.Save(stream, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default); stream.Position = 0;
        using var image = SKBitmap.Decode(stream);
        var topLeft = view.TranslatePoint(new Point(), window)!.Value;
        var scale = window.RenderScaling;
        var rect = SKRectI.Create((int)Math.Round(topLeft.X * scale), (int)Math.Round(topLeft.Y * scale),
            (int)Math.Round(view.Bounds.Width * scale), (int)Math.Round(view.Bounds.Height * scale));
        var result = new SKBitmap();
        Check(image.ExtractSubset(result, rect), "Preview screenshot is empty.");
        return result;
    }
}
