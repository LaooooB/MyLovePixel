using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyLovePixel.Application;
using MyLovePixel.Desktop;

namespace MyLovePixel.PerformanceSmoke;

public sealed class ProbeApp : Avalonia.Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
        EditorStyles.Apply(this);
    }
}

internal static class Program
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static MainWindow _window = null!;
    private static readonly List<object> Checks = [];
    private static readonly List<object> Metrics = [];
    private static int _failed;
    private static T Field<T>(string name) => (T)typeof(MainWindow).GetField(name, Flags)!.GetValue(_window)!;
    private static void Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name, Flags)!.Invoke(_window, args);
    private static void Flush() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
    private static void Check(bool ok, string name)
    {
        Checks.Add(new { name, passed = ok });
        if (!ok) _failed++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
    }

    [STAThread]
    public static int Main(string[] args)
    {
        var output = args.FirstOrDefault() ?? "artifacts/performance";
        Directory.CreateDirectory(output);
        try
        {
            AppBuilder.Configure<ProbeApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
            _window = new MainWindow { Width = 1480, Height = 920 };
            _window.Show(); Flush();
            var workspace = Field<EditorWorkspace>("_workspace");
            foreach (var size in new[] { 1024, 2048 })
            {
                var session = workspace.NewDocument(size, size); Flush();
                Call("FitCanvas"); Flush();
                var canvas = Field<PixelCanvasView>("_canvas");
                var presentation = canvas.Presentation;
                var tool = Field<StackPanel>("_toolsPanel").Children[0];
                var allocation = GC.GetAllocatedBytesForCurrentThread();
                var watch = Stopwatch.StartNew();
                var samples = new List<double>();
                for (var i = 0; i < 24; i++)
                {
                    var start = Stopwatch.GetTimestamp();
                    Call("SetZoom", (i % 2 == 0 ? 0.3 : 0.31)); Flush();
                    samples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                }
                watch.Stop();
                allocation = GC.GetAllocatedBytesForCurrentThread() - allocation;
                samples.Sort();
                Metrics.Add(new { canvasSize = size, zoomOperations = 24, totalMilliseconds = watch.Elapsed.TotalMilliseconds, medianMilliseconds = samples[12], p95Milliseconds = samples[22], allocatedBytes = allocation });
                Console.WriteLine($"METRIC {size}px zoom: {watch.Elapsed.TotalMilliseconds:F1}ms, {allocation} bytes / 24 updates");
                Check(ReferenceEquals(presentation, canvas.Presentation), $"{size}px zoom reuses the canvas presentation");
                Check(ReferenceEquals(tool, Field<StackPanel>("_toolsPanel").Children[0]), $"{size}px zoom retains inspector controls and hover state");
                Check(allocation < 24L * 1024 * 1024, $"{size}px zoom avoids full image allocations");
                Call("SetZoom", 1d); Flush();
                var host = canvas.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
                var anchor = host is not null
                    ? host.TranslatePoint(new Point(host.Bounds.Width * 0.5, host.Bounds.Height * 0.5), _window)!.Value
                    : new Point(400, 320);
                var origin = canvas.TranslatePoint(default, _window)!.Value;
                var history = session.Commands.HistoryDiagnostics.EstimatedHistoryBytes;
                _window.MouseDown(anchor, MouseButton.Middle);
                _window.MouseMove(anchor + new Vector(-45, -32), RawInputModifiers.MiddleMouseButton);
                _window.MouseUp(anchor + new Vector(-45, -32), MouseButton.Middle); Flush();
                var moved = canvas.TranslatePoint(default, _window)!.Value - origin;
                Check(Math.Abs(moved.X + 45) < 2 && Math.Abs(moved.Y + 32) < 2, $"{size}px middle-drag pans in both axes");
                Check(session.Commands.HistoryDiagnostics.EstimatedHistoryBytes == history, $"{size}px pan does not paint or add undo entries");
                using var image = _window.CaptureRenderedFrame();
                image?.Save(Path.Combine(output, $"canvas-{size}.png"), new PngBitmapEncoderOptions());
            }
            var button = Field<StackPanel>("_toolsPanel").Children.OfType<Button>().First();
            var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
            Check(presenter.Transitions?.OfType<BrushTransition>().Any(t => t.Property == ContentPresenter.BackgroundProperty) == true,
                "Hover animates the actual template background, not only its parent button");
            NavigationChecks.Run(_window, Check, output);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            File.WriteAllText(Path.Combine(output, "error.txt"), error.ToString());
            _failed++;
        }
        finally
        {
            _window?.Close();
            File.WriteAllText(Path.Combine(output, "performance.json"), JsonSerializer.Serialize(new { failed = _failed, checks = Checks, metrics = Metrics }, new JsonSerializerOptions { WriteIndented = true }));
        }
        return _failed == 0 ? 0 : 1;
    }
}
