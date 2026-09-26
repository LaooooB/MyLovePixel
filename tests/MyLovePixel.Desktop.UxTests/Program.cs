using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyLovePixel.Application;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Desktop;

internal static class Program
{
    private static int _failures;
    private static int _tests;

    [STAThread]
    public static int Main()
    {
        AppBuilder.Configure<EditorApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        Run("Tool rail has persistent names and an eyedropper", window =>
        {
            var button = Find<Button>(window, "tool.core.eyedropper");
            Check(button.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Eyedropper"), "Eyedropper needs visible text.");
            var rail = Field<StackPanel>(window, "_toolsPanel");
            foreach (var tool in rail.Children.OfType<Button>())
                Check(tool.GetVisualDescendants().OfType<TextBlock>().Any(t => !string.IsNullOrWhiteSpace(t.Text)), "Tool has no permanent name.");
        });
        Run("A sticky error survives coordinate updates", window =>
        {
            Call(window, "SetError", "Save failed — test");
            Call(window, "RefreshStatus");
            Check(Find<TextBlock>(window, "workspace.notice").Text == "Save failed — test", "Error disappeared during a coordinate update.");
        });
        Run("Eyedropper samples RGBA without editing", window =>
        {
            var session = Session(window);
            var cel = session.CaptureSnapshot().Cels.First();
            var expected = new Rgba32(23, 67, 189, 128);
            session.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(2, 3, expected)], "Seed color"));
            Pump();
            Click(Find<Button>(window, "tool.core.eyedropper"));
            var before = session.DocumentVersion;
            var canvas = Field<PixelCanvasView>(window, "_canvas");
            var point = canvas.TranslatePoint(new Point(2.5 * canvas.Zoom, 3.5 * canvas.Zoom), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Pump();
            Check(session.GetToolColors().Primary == expected, "Sample differs from the source RGBA pixel.");
            Check(session.DocumentVersion == before, "Picking created document history.");
        });
        Run("Color edits preserve tool controls", window =>
        {
            var rail = Field<StackPanel>(window, "_toolsPanel");
            var first = rail.Children.OfType<Button>().First();
            Session(window).SetToolColors(new Rgba32(40, 80, 120, 255), Rgba32.Transparent);
            Pump();
            Check(ReferenceEquals(first, rail.Children.OfType<Button>().First()), "Changing color rebuilt the tool rail.");
        });
        Run("Tool key routing matches visible labels", window =>
        {
            Field<PixelCanvasView>(window, "_canvas").Focus();
            window.KeyPress(Key.G, RawInputModifiers.None, PhysicalKey.G, null);
            window.KeyRelease(Key.G, RawInputModifiers.None, PhysicalKey.G, null);
            Pump();
            Check(Session(window).ActiveToolId == "core.fill", "G must select Fill, not toggle the grid.");
            window.KeyPress(Key.D1, RawInputModifiers.None, PhysicalKey.Digit1, null);
            window.KeyRelease(Key.D1, RawInputModifiers.None, PhysicalKey.Digit1, null);
            Pump();
            Check(Session(window).Zoom == 1d, "1 must select 100% zoom, not a tool.");
        });
        Run("HEX validation is local and non-destructive", window =>
        {
            var before = Session(window).GetToolColors();
            var hex = Find<TextBox>(window, "color.hex");
            hex.Focus();
            hex.Text = "#wrong";
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Pump();
            Check(Session(window).GetToolColors() == before, "Invalid input changed the paint color.");
            Check(Find<TextBlock>(window, "color.validation").IsVisible, "No validation is shown beside HEX input.");
        });
        Run("Alpha is a visible editable channel", window =>
        {
            var alpha = Find<NumericUpDown>(window, "color.alpha");
            alpha.Value = 77;
            Pump();
            Check(Session(window).GetToolColors().Primary.A == 77, "Alpha control is disconnected from the active paint color.");
        });
        Run("Timeline reaches frames after the first page", window =>
        {
            var session = Session(window);
            for (var i = 0; i < 29; i++) session.DuplicateCurrentFrame(false);
            Pump();
            var last = Find<Button>(window, "frame." + session.CurrentFrameId);
            Check(last.IsEffectivelyVisible, "The selected frame after frame 24 is not reachable.");
            Check(last.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "30"), "Timeline does not expose the actual frame number.");
        });
        Run("A small desktop retains named tools", window =>
        {
            window.Width = 960;
            window.Height = 640;
            Pump();
            Check(window.Bounds.Width <= 960, "Window minimum width exceeds the supported compact layout.");
            Check(Find<Button>(window, "tool.core.eyedropper").IsEffectivelyVisible, "Compact layout hides the eyedropper.");
            Directory.CreateDirectory("release/ui");
            using var image = window.CaptureRenderedFrame();
            Check(image is not null, "No actual UI frame was rendered.");
            image!.Save("release/ui/compact-960.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        });


        Run("Alt sampling returns to drawing without extra edits", window =>
        {
            var session = Session(window);
            var cel = session.CaptureSnapshot().Cels.First();
            var color = new Rgba32(35, 160, 80, 255);
            session.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(4, 5, color)], "Seed"));
            Pump();
            Click(Find<Button>(window, "tool.core.pencil"));
            var canvas = Field<PixelCanvasView>(window, "_canvas");
            var revision = session.DocumentVersion;
            window.KeyPress(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft, null);
            var point = canvas.TranslatePoint(new Point(4.5 * canvas.Zoom, 5.5 * canvas.Zoom), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            window.KeyRelease(Key.LeftAlt, RawInputModifiers.None, PhysicalKey.AltLeft, null);
            Pump();
            Check(session.DocumentVersion == revision, "Temporary picker changed the document.");
            Check(session.GetToolColors().Primary == color && session.ActiveToolId == "core.pencil", "Temporary picker lost the previous tool or color.");
            var undo = session.Commands.UndoCount;
            point = canvas.TranslatePoint(new Point(7.5 * canvas.Zoom, 8.5 * canvas.Zoom), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Pump();
            Check(session.GetCanvasPixel(7, 8) == color, "Drawing did not resume after Alt release.");
            Check(session.Commands.UndoCount == undo + 1, "One stroke must add one undo entry.");
        });
        Run("Right click cannot erase while eyedropper is selected", window =>
        {
            var session = Session(window);
            var cel = session.CaptureSnapshot().Cels.First();
            var color = new Rgba32(100, 30, 50, 255);
            session.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(2, 2, color)], "Seed"));
            Pump(); Click(Find<Button>(window, "tool.core.eyedropper"));
            var revision = session.DocumentVersion;
            var canvas = Field<PixelCanvasView>(window, "_canvas");
            var point = canvas.TranslatePoint(new Point(2.5 * canvas.Zoom, 2.5 * canvas.Zoom), window)!.Value;
            window.MouseDown(point, MouseButton.Right); window.MouseUp(point, MouseButton.Right); Pump();
            Check(session.DocumentVersion == revision && session.GetCanvasPixel(2, 2) == color, "Picker right click erased a pixel.");
        });
        Run("Text editing does not activate drawing shortcuts", window =>
        {
            var session = Session(window);
            var before = session.ActiveToolId;
            var hex = Find<TextBox>(window, "color.hex"); hex.Focus();
            window.KeyPress(Key.E, RawInputModifiers.None, PhysicalKey.E, null);
            window.KeyRelease(Key.E, RawInputModifiers.None, PhysicalKey.E, null); Pump();
            Check(session.ActiveToolId == before, "Typing E in a field selected Eraser.");
        });
        Run("Cancel closing preserves unsaved work", window =>
        {
            var session = Session(window);
            session.SetLayerOpacity(session.CurrentLayerId, 130); Pump();
            Call(window, "CloseCurrentDocumentAsync"); Pump();
            var dialog = window.OwnedWindows.Single();
            Click(Find<Button>(dialog, "dialog.cancel")); Pump();
            Check(ReferenceEquals(Session(window), session) && session.IsDirty, "Cancel lost the unsaved document.");
        });
        Run("A failed save leaves document and editor usable", window =>
        {
            var session = Session(window);
            session.SetLayerOpacity(session.CurrentLayerId, 100); Pump();
            var method = typeof(MainWindow).GetMethod("RunBusyAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var task = (Task<bool>)method.Invoke(window, new object[] { "Saving…", (Action)(() => throw new IOException("test write denied")) })!;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!task.IsCompleted && DateTime.UtcNow < deadline) { Thread.Sleep(10); Pump(); }
            Check(task.IsCompleted && !task.GetAwaiter().GetResult(), "Save failure was reported as success.");
            Check(session.IsDirty && ReferenceEquals(Session(window), session), "Save failure discarded edits.");
            Check(!Field<bool>(window, "_busy") && Find<TextBlock>(window, "workspace.notice").Text!.Contains("test write denied"), "Failure did not restore controls and error feedback.");
        });
        Run("Continuous opacity editing is one undo step", window =>
        {
            var session = Session(window);
            Field<TabControl>(window, "_sideTabs").SelectedIndex = 1; Pump();
            var opacity = Field<StackPanel>(window, "_layersPanel").GetVisualDescendants().OfType<NumericUpDown>().First();
            opacity.Focus(); var before = session.Commands.UndoCount;
            opacity.Value = 70; opacity.Value = 40; opacity.Value = 20;
            Call(window, "FinishParameterEdit", true); Pump();
            Check(session.Commands.UndoCount == before + 1, "Opacity changes generated multiple undo entries.");
            session.Undo(); Pump();
            Check(session.GetLayers().Single(l => l.IsCurrent).Opacity == 255, "One undo did not restore the starting opacity.");
        });
        Run("Space pan works at fit zoom without drawing", window =>
        {
            Call(window, "FitCanvas"); Pump();
            var canvas = Field<PixelCanvasView>(window, "_canvas");
            var scroll = Field<ScrollViewer>(window, "_canvasScroll");
            var before = scroll.Offset; var revision = Session(window).DocumentVersion;
            var point = canvas.TranslatePoint(new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2), window)!.Value;
            canvas.Focus(); window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            window.MouseDown(point, MouseButton.Left); window.MouseMove(point + new Vector(25, 20)); window.MouseUp(point + new Vector(25, 20), MouseButton.Left);
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null); Pump();
            Check(scroll.Offset != before, "A fitted canvas cannot be panned.");
            Check(Session(window).DocumentVersion == revision, "Panning created a drawing edit.");
        });
        Run("Zoom keeps the pointed pixel anchored", window =>
        {
            Call(window, "SetZoom", 16d); Pump();
            var canvas = Field<PixelCanvasView>(window, "_canvas"); var scroll = Field<ScrollViewer>(window, "_canvasScroll");
            var point = scroll.TranslatePoint(new Point(scroll.Viewport.Width / 2, scroll.Viewport.Height / 2), canvas)!.Value;
            var before = canvas.TranslatePoint(point, scroll)!.Value; var zoom = canvas.Zoom;
            Call(window, "ChangeZoomAt", 1.25d, point); Pump();
            var after = canvas.TranslatePoint(point * (canvas.Zoom / zoom), scroll)!.Value;
            Check(Math.Abs(after.X - before.X) <= 1.1 && Math.Abs(after.Y - before.Y) <= 1.1, "Zoom moved the anchored pixel.");
        });
        Run("Core dialogs fit compact desktop heights", window =>
        {
            using var snapshot = window.CaptureRenderedFrame();
            var dialog = new ExportDialog();
            var task = dialog.ShowDialog<object?>(window); Pump();
            Check(dialog.Bounds.Height <= 560, "Export dialog exceeds compact desktop height.");
            Check(dialog.GetVisualDescendants().OfType<Button>().Any(b => b.IsCancel), "Export dialog lacks Escape cancellation.");
            dialog.Close(null); Pump();
        });
        Console.WriteLine($"Desktop UX tests: {_tests - _failures}/{_tests} passed.");
        return _failures == 0 ? 0 : 1;
    }

    private static void Run(string name, Action<MainWindow> test)
    {
        var window = new MainWindow();
        _tests++;
        try
        {
            window.Show();
            Field<DispatcherTimer>(window, "_autosaveTimer").Stop();
            Field<DispatcherTimer>(window, "_playbackTimer").Stop();
            Pump();
            test(window);
            Directory.CreateDirectory("release/ui");
            using (var frame = window.CaptureRenderedFrame())
                frame?.Save($"release/ui/{_tests:00}.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex)
        {
            _failures++;
            Console.WriteLine("FAIL " + name + ": " + ex);
            Console.WriteLine("Notice: " + Field<TextBlock>(window, "_notice").Text);
            Directory.CreateDirectory("release/ui");
            using (var frame = window.CaptureRenderedFrame())
                frame?.Save($"release/ui/{_tests:00}-failed.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        }
        finally
        {
            // Explicitly discard test-created sessions so production close protection
            // is not mistaken for a blocked test shutdown.
            var workspace = Field<EditorWorkspace>(window, "_workspace");
            foreach (var session in workspace.Sessions.ToArray()) workspace.Close(session);
            window.Close();
            Pump();
        }
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); }
    private static DocumentSession Session(MainWindow window) => Field<EditorWorkspace>(window, "_workspace").CurrentSession!;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
    private static T Find<T>(Window window, string id) where T : Control =>
        window.GetVisualDescendants().OfType<T>().FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == id)
        ?? throw new InvalidOperationException("Missing accessible control: " + id);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
