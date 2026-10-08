using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyLovePixel.Application;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Desktop;

namespace MyLovePixel.Desktop.Smoke;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => AppBuilder.Configure<SmokeApp>().UsePlatformDetect()
        .With(new FontManagerOptions { DefaultFamilyName = OperatingSystem.IsLinux()
            ? "avares://MyLovePixel.Desktop.Smoke/Assets#Noto Sans" : "Segoe UI" })
        .StartWithClassicDesktopLifetime(args);
}

public sealed class SmokeApp : Avalonia.Application
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MyLovePixel-ui-smoke-" + Guid.NewGuid());
    private readonly string _output = Environment.GetEnvironmentVariable("MLP_SMOKE_OUTPUT") ?? Path.Combine(Environment.CurrentDirectory, "smoke-output");
    private string PalettePath => Path.Combine(_root, "user-palette.json");
    private readonly List<string> _passed = [];
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark; Styles.Add(new FluentTheme()); EditorStyles.Apply(this);
    }
    public override void OnFrameworkInitializationCompleted()
    {
        Directory.CreateDirectory(_root); Directory.CreateDirectory(_output);
        var legacy = Enumerable.Range(0, 600).Select(i => $"#{i:X6}").ToArray();
        File.WriteAllText(PalettePath, JsonSerializer.Serialize(new { schemaVersion = 1, colors = legacy }));
        var window = new MainWindow(new UserPaletteStore(PalettePath));
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = window;
        window.Opened += (_, _) => Dispatcher.UIThread.Post(async () =>
        {
            try { await Run(window); Report(); Environment.Exit(0); }
            catch (Exception e) { Console.Error.WriteLine(e); File.WriteAllText(Path.Combine(_output, "FAILURE.txt"), e.ToString()); try { Shot(window, "failure.png"); } catch { } Environment.Exit(1); }
        });
        base.OnFrameworkInitializationCompleted();
    }
    private void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        _passed.Add(name); Console.WriteLine("PASS " + name);
    }
    private void Report() => File.WriteAllText(Path.Combine(_output, "ui-smoke.txt"), string.Join(Environment.NewLine, _passed.Select(s => "PASS " + s)));
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(obj)!;
    private static object? Invoke(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(obj, args);
    private static T Find<T>(Visual root, string id) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => AutomationProperties.GetAutomationId(c) == id);
    private static void Click(Button button)
    {
        if (!button.IsEnabled) throw new InvalidOperationException("Button disabled: " + AutomationProperties.GetAutomationId(button));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    private static void Click(Visual root, string id) => Click(Find<Button>(root, id));
    private static void ByText(Window root, string text) => Click(root.GetVisualDescendants().OfType<Button>().Single(b => b.Content is string s && s == text));
    private static Task Tick() => Task.Delay(150);
    private void Shot(Window window, string name)
    {
        using var bitmap = new RenderTargetBitmap(PixelSize.FromSize(window.ClientSize, 1), new Vector(96, 96));
        bitmap.Render(window); bitmap.Save(Path.Combine(_output, name), new PngBitmapEncoderOptions());
    }
    private async Task Run(MainWindow window)
    {
        await Tick();
        var store = Field<UserPaletteStore>(window, "_userPaletteStore");
        Check(store.Colors.Count == 600, "600 legacy colors loaded in real window");
        Check(!File.Exists(PalettePath + ".pre-v2.bak"), "Opening the window alone does not rewrite the legacy file");
        var sideTabs = window.GetVisualDescendants().OfType<TabControl>().First(t => t.Items.OfType<TabItem>().Any(i => i.Header is TextBlock { Text: "Colors" }));
        Check(!sideTabs.Items.OfType<TabItem>().Any(i => i.Header is TextBlock { Text: "Photo" }), "Photo/drop-image tab removed");
        Check(Field<WrapPanel>(window, "_userPaletteSwatches").Children.Count == 48, "Large color library renders only 48 swatches per page");
        var search = Find<TextBox>(window, "color-search"); search.Text = "00012A"; await Tick();
        Check(Field<WrapPanel>(window, "_userPaletteSwatches").Children.Count == 1, "HEX search narrows 600 colors to the matching swatch");
        search.Text = "no-match"; await Tick();
        Check(Field<TextBlock>(window, "_userPaletteEmpty").IsVisible, "Empty search state shown");
        Click(window, "color-search-clear"); await Tick(); Click(window, "color-page-next");
        Check(Field<TextBlock>(window, "_colorPageLabel").Text == "2/13", "Next-page button advances the virtualized library");
        Click(window, "color-folder-new"); await Tick();
        var name = window.OwnedWindows.Single(w => w.IsVisible); Find<TextBox>(name, "folder-name-input").Text = "Terrain"; ByText(name, "Save"); await Tick();
        Check(store.Folders.Single().Name == "Terrain", "New-folder dialog saves and selects a folder");
        Check(File.Exists(PalettePath + ".pre-v2.bak"), "First UI mutation creates the lossless pre-upgrade backup");
        Find<TextBox>(window, "studio-hex-input").Text = "#654321"; Click(window, "studio-hex-apply"); Click(window, "user-palette-save"); await Tick();
        Check(store.Entries.Single(e => HexColor.Format(e.Color) == "#654321").FolderId == store.Folders.Single().Id, "Save current places the color in the selected folder");
        Click(window, "user-palette-654321"); Click(window, "user-palette-edit"); await Tick();
        var edit = window.OwnedWindows.Single(w => w.IsVisible); Find<TextBox>(edit, "saved-color-name").Text = "Earth"; ByText(edit, "Save"); await Tick();
        search.Text = "earth"; await Tick();
        Check(Field<WrapPanel>(window, "_userPaletteSwatches").Children.Count == 1, "Saved-color name search works after edit dialog");
        Click(window, "color-search-clear");
        var session = (DocumentSession)Invoke(window, "Current")!;
        var cel = session.CaptureSnapshot().Cels.Single();
        var one = new Rgba32(220, 60, 80); var two = new Rgba32(30, 100, 210);
        session.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, one), new PixelWrite(1, 0, two)]));
        var before = session.RenderCanvas().Rgba.ToArray(); var undo = session.Commands.UndoCount;
        Click(window, "color-eyedropper");
        foreach (var x in new[] { 0, 1 })
        {
            Invoke(window, "DispatchCanvasPointer", new EditorPointerEvent(1, EditorPointerDevice.Mouse, EditorPointerKind.Pressed, new IntPoint(x, 0), 1d, EditorPointerButtons.Primary, EditorInputModifiers.None, x));
            Click(window, "temporary-save-current");
        }
        Check(store.TemporaryColors.SequenceEqual(new[] { one, two }), "Eyedropper to +Temp retains two distinct samples");
        Check(undo == session.Commands.UndoCount && before.SequenceEqual(session.RenderCanvas().Rgba.ToArray()), "UI eyedropper never changes pixels or undo history");
        var libraryTabs = Find<TabControl>(window, "color-library-tabs"); libraryTabs.SelectedIndex = 1; await Tick();
        Click(window, "temporary-DC3C50");
        Check(session.GetToolColors().Primary == one, "Temporary swatch click restores the first sample");
        Click(window, "temporary-promote");
        Check(store.Colors.Contains(one) && store.TemporaryColors.Contains(one), "Promotion copies a temporary color into the library without losing the rack entry");
        Shot(window, "temporary-1480.png");
        libraryTabs.SelectedIndex = 0; await Tick();
        var oldColor = session.GetToolColors().Primary;
        Click(window, "open-color-palette"); await Tick();
        var picker = window.OwnedWindows.Single(w => w.IsVisible);
        picker.Position = new PixelPoint(20, 20); picker.Activate(); await Tick();
        var pad = Find<Control>(picker, "color-spectrum");
        Check(pad.Bounds.Width > 200 && pad.Bounds.Height > 150, "Palette opens with a usable spectrum pad");
        var initialHex = Find<TextBox>(picker, "color-picker-hex").Text;
        await Drag(pad);
        Check(Find<TextBox>(picker, "color-picker-hex").Text != initialHex, "Native mouse drag updates the palette circle and HEX");
        Find<Slider>(picker, "color-alpha").Value = 128; await Tick();
        Shot(picker, "picker.png");
        Find<TextBox>(picker, "color-picker-hex").Text = "#12345680"; Click(picker, "color-picker-apply"); await Tick();
        Check(session.GetToolColors().Primary == new Rgba32(18, 52, 86, 128), "Palette Apply keeps exact RGB and alpha");
        Click(window, "open-color-palette"); await Tick();
        picker = window.OwnedWindows.Single(w => w.IsVisible); Find<TextBox>(picker, "color-picker-hex").Text = "#FFFFFF"; ByText(picker, "Cancel"); await Tick();
        Check(session.GetToolColors().Primary == new Rgba32(18, 52, 86, 128), "Palette Cancel leaves the previous drawing color unchanged");
        Shot(window, "colors-1480.png");
        window.Width = 1080; window.Height = 700; await Tick(); await Tick();
        var save = Find<Button>(window, "user-palette-save"); var next = Find<Button>(window, "color-page-next");
        Check(save.IsEffectivelyVisible && next.IsEffectivelyVisible && next.Bounds.Height > 0, "Save and pagination controls remain visible at 1080 by 700");
        var swatches = Field<WrapPanel>(window, "_userPaletteSwatches");
        var scroll = Field<ScrollViewer>(window, "_colorSwatchScroll");
        Check(scroll.Bounds.Height >= 66, "At least one full swatch row is visible at minimum window height");
        Shot(window, "colors-1080.png");
        var reopened = new UserPaletteStore(PalettePath);
        Check(reopened.Colors.Count == 602 && reopened.Folders.Count == 1 && reopened.TemporaryColors.Count == 2, "Reopened store retains all 600 old colors plus new colors, folder and temporary samples");
        Check(JsonDocument.Parse(File.ReadAllText(PalettePath + ".pre-v2.bak")).RootElement.GetProperty("colors").GetArrayLength() == 600, "Upgrade backup retains all 600 original colors");
    }

    private static async Task Drag(Control pad)
    {
        var start = pad.PointToScreen(new Point(20, 20)); var finish = pad.PointToScreen(new Point(pad.Bounds.Width * .70, pad.Bounds.Height * .45));
        if (OperatingSystem.IsWindows())
        {
            SetCursorPos(start.X, start.Y); await Tick(); MouseEvent(2, 0, 0, 0, 0); await Tick();
            SetCursorPos(finish.X, finish.Y); await Tick(); MouseEvent(4, 0, 0, 0, 0); await Tick();
        }
        else
        {
            var display = XOpenDisplay(null); if (display == 0) throw new InvalidOperationException("Cannot open X display");
            try
            {
                XTestFakeMotionEvent(display, -1, start.X, start.Y, 0); XFlush(display); await Tick();
                XTestFakeButtonEvent(display, 1, true, 0); XFlush(display); await Tick();
                XTestFakeMotionEvent(display, -1, finish.X, finish.Y, 0); XFlush(display); await Tick();
                XTestFakeButtonEvent(display, 1, false, 0); XFlush(display); await Tick();
            }
            finally { XCloseDisplay(display); }
        }
    }
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", EntryPoint = "mouse_event")] private static extern void MouseEvent(uint flags, uint x, uint y, uint data, nuint extra);
    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string? name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
    [DllImport("libX11.so.6")] private static extern int XFlush(nint display);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeMotionEvent(nint display, int screen, int x, int y, ulong delay);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeButtonEvent(nint display, uint button, bool press, ulong delay);
}
