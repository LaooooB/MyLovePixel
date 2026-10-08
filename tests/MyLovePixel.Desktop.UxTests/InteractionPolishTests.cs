using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.VisualTree;
using MyLovePixel.Application;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Desktop;

internal static partial class Program
{
    private static void RunInteractionPolishTests()
    {
        Run("Right drag fills gaps and undoes the entire stroke", w =>
        {
            var s = Session(w); SeedRow(s); Pump();
            var c = Field<PixelCanvasView>(w, "_canvas"); var count = s.Commands.UndoCount;
            w.MouseDown(At(w, 2, 3), MouseButton.Right);
            w.MouseMove(At(w, 9, 3)); w.MouseUp(At(w, 9, 3), MouseButton.Right); Pump();
            Check(Enumerable.Range(2, 8).All(x => s.GetCanvasPixel(x, 3).A == 0), "Fast right drag left gaps.");
            Check(s.Commands.UndoCount == count + 1 && !c.HasActivePointer, "Erase did not complete one transaction.");
            s.Undo(); Pump();
            Check(Enumerable.Range(2, 8).All(x => s.GetCanvasPixel(x, 3).A == 255), "One undo failed to restore the whole path.");
            s.Redo(); Pump(); Check(Enumerable.Range(2, 8).All(x => s.GetCanvasPixel(x, 3).A == 0), "Redo missed erased pixels.");
        });
        Run("Escape rolls back an active right erase", w =>
        {
            var s = Session(w); SeedRow(s); Pump(); var count = s.Commands.UndoCount;
            w.MouseDown(At(w, 2, 3), MouseButton.Right); w.MouseMove(At(w, 9, 3));
            w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            w.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            w.MouseUp(At(w, 9, 3), MouseButton.Right); Pump();
            Check(Enumerable.Range(2, 8).All(x => s.GetCanvasPixel(x, 3).A == 255), "Cancel failed to restore all pixels.");
            Check(s.Commands.UndoCount == count && !Field<PixelCanvasView>(w, "_canvas").HasActivePointer, "Cancel stranded history or capture.");
        });
        Run("Right erase does not join across an excursion outside the canvas", w =>
        {
            var s = Session(w); SeedRow(s); Pump();
            w.MouseDown(At(w, 2, 3), MouseButton.Right); w.MouseMove(At(w, 4, -10));
            w.MouseMove(At(w, 9, 3)); w.MouseUp(At(w, 9, 3), MouseButton.Right); Pump();
            Check(s.GetCanvasPixel(2, 3).A == 0 && s.GetCanvasPixel(9, 3).A == 0, "Re-entry did not continue erasing.");
            Check(Enumerable.Range(3, 6).All(x => s.GetCanvasPixel(x, 3).A == 255), "Off-canvas movement drew an unintended connecting path.");
        });
        Run("Other mouse buttons cannot interrupt a right erase", w =>
        {
            var s = Session(w); SeedRow(s); Pump();
            w.MouseDown(At(w, 2, 3), MouseButton.Right);
            w.MouseDown(At(w, 2, 3), MouseButton.Left, RawInputModifiers.RightMouseButton); w.MouseUp(At(w, 2, 3), MouseButton.Left, RawInputModifiers.RightMouseButton);
            Check(Field<PixelCanvasView>(w, "_canvas").HasActivePointer, "Releasing the other mouse button ended the erase.");
            w.MouseMove(At(w, 9, 3)); w.MouseUp(At(w, 9, 3), MouseButton.Right); Pump();
            Check(Enumerable.Range(2, 8).All(x => s.GetCanvasPixel(x, 3).A == 0), "A second button disrupted the erase path.");
        });
        Run("Erasing blank pixels is a no-op and locks block right drag", w =>
        {
            var s = Session(w); var count = s.Commands.UndoCount;
            w.MouseDown(At(w, 2, 3), MouseButton.Right); w.MouseMove(At(w, 9, 3)); w.MouseUp(At(w, 9, 3), MouseButton.Right); Pump();
            Check(s.Commands.UndoCount == count && !s.IsDirty, "An empty erase dirtied the document.");
            SeedRow(s); s.SetLayerLocked(s.CurrentLayerId, true); Pump(); count = s.Commands.UndoCount;
            w.MouseDown(At(w, 2, 3), MouseButton.Right); w.MouseMove(At(w, 9, 3)); w.MouseUp(At(w, 9, 3), MouseButton.Right); Pump();
            Check(s.Commands.UndoCount == count && Enumerable.Range(2, 8).All(x => s.GetCanvasPixel(x, 3).A == 255), "Right erase changed a locked layer.");
        });
        Run("Canvas hover outline fades without changing artwork", w =>
        {
            var c = Field<PixelCanvasView>(w, "_canvas"); var version = Session(w).DocumentVersion;
            var property = typeof(PixelCanvasView).GetProperty("HoverOpacity");
            Check(property is not null, "The canvas cursor has no animated opacity.");
            double Opacity() => (double)property!.GetValue(c)!;
            MoveWithoutSettling(w, At(w, 10, 10)); PumpFor(45);
            Check(Opacity() > 0 && Opacity() < 1, "Cursor fade-in skipped intermediate opacity.");
            PumpFor(170); Check(Opacity() > .99, "Cursor failed to finish appearing.");
            MoveWithoutSettling(w, new Point(180, 15)); PumpFor(45);
            Check(Opacity() > 0 && Opacity() < 1, "Cursor disappeared abruptly.");
            PumpFor(170); Check(Opacity() < .01 && Session(w).DocumentVersion == version, "Hover altered artwork or stayed visible.");
        });
        Run("Hover colors actually interpolate on enter and exit", w =>
        {
            var button = Find<Button>(w, "tool.core.fill");
            var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First();
            var p = button.TranslatePoint(new Point(20, 18), w)!.Value;
            MoveWithoutSettling(w, new Point(w.Bounds.Width / 2, 15)); PumpFor(220);
            var resting = BrushColor(presenter.Background);
            MoveWithoutSettling(w, p); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); var start = BrushColor(presenter.Background);
            PumpFor(65); var mid = BrushColor(presenter.Background);
            PumpFor(180); var full = BrushColor(presenter.Background);
            using (var capture = w.CaptureRenderedFrame()) capture?.Save("release/ui/hover-full.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            Check(resting != full, "Hover has no visible effect.");
            Check(start != full && mid != start && mid != full, "Hover enter snapped instead of fading.");
            MoveWithoutSettling(w, new Point(w.Bounds.Width / 2, 15)); PumpFor(65); var leaving = BrushColor(presenter.Background);
            PumpFor(180); var end = BrushColor(presenter.Background);
            Check(leaving != full && leaving != resting && end == resting, "Hover exit did not fade back to rest.");
        });
        Run("Tooltips fade both ways and disappear completely", w =>
        {
            var button = Find<Button>(w, "project.save"); ToolTip.SetShowDelay(button, 20);
            MoveWithoutSettling(w, new Point(w.Bounds.Width / 2, 15)); PumpFor(30);
            MoveWithoutSettling(w, button.TranslatePoint(new Point(15, 15), w)!.Value);
            ToolTip? tip = null;
            // Observe actual rendered frames: a loaded Windows worker may spend
            // the old fixed 70 ms creating the popup, before its animation starts.
            Check(ObserveAnimation(() =>
            {
                tip = w.GetVisualDescendants().OfType<ToolTip>().FirstOrDefault(t => t.IsEffectivelyVisible);
                return tip is { Opacity: > 0 and < 1 };
            }), "Tooltip fade-in did not produce an intermediate opacity.");
            PumpFor(210); Check(tip!.Opacity > .99, "Tooltip never reached full opacity.");
            MoveWithoutSettling(w, new Point(w.Bounds.Width / 2, 15));
            Check(ObserveAnimation(() => tip!.IsAttachedToVisualTree() && tip!.Opacity > 0 && tip!.Opacity < 1),
                "Tooltip vanished before fade-out completed.");
            PumpFor(200); Check(!tip!.IsAttachedToVisualTree(), "Faded tooltip remained attached.");
        });
        Run("Tooltip fade reverses on reentry and Escape dismisses it", w =>
        {
            var button = Find<Button>(w, "project.save"); ToolTip.SetShowDelay(button, 20);
            var p = button.TranslatePoint(new Point(15, 15), w)!.Value;
            MoveWithoutSettling(w, p); PumpFor(250);
            var tip = w.GetVisualDescendants().OfType<ToolTip>().FirstOrDefault(t => t.IsEffectivelyVisible);
            Check(tip is not null, "Tooltip did not appear.");
            MoveWithoutSettling(w, new Point(w.Bounds.Width / 2, 15)); PumpFor(45); MoveWithoutSettling(w, p); PumpFor(230);
            Check(tip!.IsAttachedToVisualTree() && tip!.Opacity > .99, "Reentry closed or recreated the fading tooltip.");
            Field<PixelCanvasView>(w, "_canvas").Focus();
            w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            w.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); PumpFor(220);
            Check(!tip!.IsAttachedToVisualTree(), "Escape left a hover popup behind.");
        });
    }

    private static Point At(MainWindow w, int x, int y)
    {
        var c = Field<PixelCanvasView>(w, "_canvas");
        return c.TranslatePoint(new Point((x + .5) * c.Zoom, (y + .5) * c.Zoom), w)!.Value;
    }
    private static void SeedRow(DocumentSession s) => s.Execute(new PixelPatchCommand(s.CaptureSnapshot().Cels.First().SurfaceId,
        Enumerable.Range(2, 8).Select(x => new PixelWrite(x, 3, new Rgba32(180, 70, 30, 255))), "Seed erase path"));
    private static void MoveWithoutSettling(MainWindow window, Point position)
    {
        // The Headless extension drains up to ten animation frames per MouseMove.
        // Inject directly so intermediate frames can be asserted rather than skipped.
        var impl = window.PlatformImpl!;
        impl.GetType().GetInterfaces().Single(t => t.Name == "IHeadlessWindow").GetMethod("MouseMove")!.Invoke(impl, new object[] { position, RawInputModifiers.None });
    }
    private static Color BrushColor(IBrush? brush) => (brush as ISolidColorBrush)?.Color ?? Colors.Transparent;
    private static bool ObserveAnimation(Func<bool> condition)
    {
        var observed = false;
        var frame = new Avalonia.Threading.DispatcherFrame();
        var render = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
        render.Tick += (_, _) =>
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            if (condition()) { observed = true; frame.Continue = false; }
        };
        using var end = Avalonia.Threading.DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromSeconds(2));
        render.Start();
        try { Avalonia.Threading.Dispatcher.UIThread.PushFrame(frame); }
        finally { render.Stop(); }
        return observed;
    }

    private static void PumpFor(int milliseconds)
    {
        var frame = new Avalonia.Threading.DispatcherFrame();
        var render = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        render.Tick += (_, _) => AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var end = Avalonia.Threading.DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(milliseconds));
        render.Start();
        try { Avalonia.Threading.Dispatcher.UIThread.PushFrame(frame); }
        finally { render.Stop(); }
    }
}
