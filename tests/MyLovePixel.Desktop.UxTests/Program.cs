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
            window.KeyPress(Key.G, RawInputModifiers.None);
            window.KeyRelease(Key.G, RawInputModifiers.None);
            Pump();
            Check(Session(window).ActiveToolId == "core.fill", "G must select Fill, not toggle the grid.");
            window.KeyPress(Key.D1, RawInputModifiers.None);
            window.KeyRelease(Key.D1, RawInputModifiers.None);
            Pump();
            Check(Session(window).Zoom == 1d, "1 must select 100% zoom, not a tool.");
        });
        Run("HEX validation is local and non-destructive", window =>
        {
            var before = Session(window).GetToolColors();
            var hex = Find<TextBox>(window, "color.hex");
            hex.Focus();
            hex.Text = "#wrong";
            window.KeyPress(Key.Enter, RawInputModifiers.None);
            window.KeyRelease(Key.Enter, RawInputModifiers.None);
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
            image!.Save("release/ui/compact-960.png");
        });

        Console.WriteLine($"Desktop UX tests: {9 - _failures}/9 passed.");
        return _failures == 0 ? 0 : 1;
    }

    private static void Run(string name, Action<MainWindow> test)
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            Field<DispatcherTimer>(window, "_autosaveTimer").Stop();
            Field<DispatcherTimer>(window, "_playbackTimer").Stop();
            Pump();
            test(window);
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex)
        {
            _failures++;
            Console.WriteLine("FAIL " + name + ": " + ex);
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
    private static T Find<T>(MainWindow window, string id) where T : Control =>
        window.GetVisualDescendants().OfType<T>().FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == id)
        ?? throw new InvalidOperationException("Missing accessible control: " + id);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
