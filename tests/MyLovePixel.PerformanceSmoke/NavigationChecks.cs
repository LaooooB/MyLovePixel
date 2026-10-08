using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Desktop;

namespace MyLovePixel.PerformanceSmoke;

internal static class NavigationChecks
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run(MainWindow window, Action<bool, string> check, string output)
    {
        T Field<T>(string name) => (T)typeof(MainWindow).GetField(name, Flags)!.GetValue(window)!;
        void Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name, Flags)!.Invoke(window, args);
        var workspace = Field<EditorWorkspace>("_workspace");
        var canvas = Field<PixelCanvasView>("_canvas");
        var host = canvas.GetVisualAncestors().OfType<ScrollViewer>().First();
        Point Origin() => canvas.TranslatePoint(default, window)!.Value;
        Point Anchor() => host.TranslatePoint(new Point(host.Viewport.Width * 0.5, host.Viewport.Height * 0.5), window)!.Value;
        var session = workspace.NewDocument(64, 64); Flush(); Call("FitCanvas"); Flush();
        var origin = Origin(); var anchor = Anchor();
        var history = session.Commands.HistoryDiagnostics.EstimatedHistoryBytes;
        window.MouseDown(anchor, MouseButton.Middle);
        window.MouseMove(anchor + new Vector(62, -33), RawInputModifiers.MiddleMouseButton);
        window.MouseUp(anchor + new Vector(62, -33), MouseButton.Middle); Flush();
        var displacement = Origin() - origin;
        check(Math.Abs(displacement.X - 62) < 2 && Math.Abs(displacement.Y + 33) < 2, "Middle pan works for a fitted small canvas, not only overflowing images");
        check(Field<IPointer?>("_panPointer") is null, "Middle release clears capture state");
        window.MouseDown(anchor, MouseButton.Middle);
        window.MouseMove(anchor + new Vector(-20, -12), RawInputModifiers.MiddleMouseButton);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        var canceled = host.Offset;
        window.MouseMove(anchor + new Vector(-50, -42), RawInputModifiers.MiddleMouseButton);
        window.MouseUp(anchor + new Vector(-50, -42), MouseButton.Middle); Flush();
        check(Field<IPointer?>("_panPointer") is null && host.Offset == canceled, "Escape ends middle pan without a stuck hand cursor");
        check(session.Commands.HistoryDiagnostics.EstimatedHistoryBytes == history, "Fitted pan and cancellation leave artwork history unchanged");
        Call("FitCanvas"); session.SelectTool("core.pencil"); Flush();
        var pixel = canvas.TranslatePoint(new Point(canvas.Zoom * 32.5, canvas.Zoom * 32.5), window)!.Value;
        window.MouseDown(pixel, MouseButton.Left); window.MouseUp(pixel, MouseButton.Left); Flush();
        var surface = session.CaptureSnapshot().GetSurface(session.CaptureSnapshot().Cels.Single().SurfaceId);
        var painted = 0;
        for (var y = 0; y < 64; y++) for (var x = 0; x < 64; x++) if (surface.GetPixel(x, y).A != 0) painted++;
        check(painted == 1 && surface.GetPixel(32, 32).A != 0, "Pencil still paints exactly one pixel after middle pan");
        session.Undo(); Flush();
        check(session.CaptureSnapshot().GetSurface(session.CaptureSnapshot().Cels.Single().SurfaceId).GetPixel(32, 32).A == 0, "Undo works after navigating and resuming drawing");
        session = workspace.NewDocument(1024, 1024); Flush(); Call("FitCanvas"); Flush();
        anchor = Anchor() + new Vector(42, -18);
        var beforeZoom = canvas.Zoom;
        var beforePosition = (anchor - Origin()) / beforeZoom;
        var uploads = canvas.DisplayFullUploadCount;
        window.MouseWheel(anchor, new Vector(0, 0.5)); Pump(230);
        var afterPosition = (anchor - Origin()) / canvas.Zoom;
        check(Math.Abs(canvas.Zoom - beforeZoom * Math.Sqrt(1.2)) < 0.00001, "Fractional wheel deltas produce the exact zoom target");
        check((Math.Abs(afterPosition.X - beforePosition.X) + Math.Abs(afterPosition.Y - beforePosition.Y)) * canvas.Zoom < 1.5, "Animated zoom keeps the pixel under the cursor anchored");
        check(canvas.DisplayFullUploadCount == uploads, "Animated wheel zoom never reuploads the full bitmap");
        Call("SetZoom", 0.5d); Flush();
        for (var i = 0; i < 3; i++) window.MouseWheel(anchor, new Vector(0, 1));
        for (var i = 0; i < 2; i++) window.MouseWheel(anchor, new Vector(0, -1));
        Pump(240);
        check(Math.Abs(canvas.Zoom - 0.6) < 0.00001, "Rapid wheel reversal coalesces without dropping or duplicating notches");
        window.MouseWheel(anchor, new Vector(0, 1));
        window.MouseDown(anchor, MouseButton.Left);
        var drawingZoom = canvas.Zoom; Pump(180);
        check(Math.Abs(canvas.Zoom - drawingZoom) < 0.00001, "Starting a stroke stops camera animation immediately");
        window.MouseUp(anchor, MouseButton.Left); Flush();
        var saveOffset = host.Offset; var saveZoom = canvas.Zoom;
        workspace.Save(session, Path.Combine(output, "navigation-save.pixelproj")); Flush();
        check(host.Offset == saveOffset && canvas.Zoom == saveZoom, "Saving the document does not reset the camera");
        Call("SetZoom", 0.3d); Flush();
        var cacheProperty = typeof(PixelCanvasView).GetProperty("DisplayResampleCount");
        var resamples = cacheProperty?.GetValue(canvas) as long?;
        for (var i = 0; i < 8; i++) { Call("SetZoom", i % 2 == 0 ? 0.31d : 0.3d); Flush(); }
        check(resamples is > 0 && Equals(resamples, cacheProperty?.GetValue(canvas)), "Minified canvas reuses bounded resolution caches between zoom frames");
        var button = Field<StackPanel>("_toolsPanel").Children.OfType<Button>().First();
        var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
        var hoverPoint = button.TranslatePoint(new Point(button.Bounds.Width * 0.5, button.Bounds.Height * 0.5), window)!.Value;
        window.MouseMove(new Point(500, 65)); Pump(210);
        var beforeBounds = button.Bounds;
        var values = new HashSet<uint>();
        void Sample() { if (presenter.Background is ISolidColorBrush brush) values.Add(brush.Color.ToUInt32()); }
        Sample(); window.MouseMove(hoverPoint); Sample();
        for (var i = 0; i < 12; i++) { Pump(16); Sample(); }
        check(values.Count >= 3, "Hover fade-in renders intermediate colors rather than jumping");
        values.Clear(); Sample(); window.MouseMove(new Point(500, 65));
        for (var i = 0; i < 12; i++) { Pump(16); Sample(); }
        check(values.Count >= 3, "Hover fade-out renders intermediate colors rather than jumping");
        check(button.Bounds == beforeBounds, "Hover and leave do not move or resize the click target");
        using (var image = window.CaptureRenderedFrame()) image?.Save(Path.Combine(output, "navigation-verified.png"), new PngBitmapEncoderOptions());
    }
    private static void Flush() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
    private static void Pump(int milliseconds)
    {
        var start = Stopwatch.GetTimestamp();
        do { Thread.Sleep(5); Flush(); } while (Stopwatch.GetElapsedTime(start).TotalMilliseconds < milliseconds);
    }
}
