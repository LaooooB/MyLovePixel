using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Desktop;

namespace MyLovePixel.UxSmoke;

public sealed class SmokeApplication : Avalonia.Application
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
    private static readonly List<string> Checks = [];
    private static string _output = string.Empty;
    private static MainWindow? _window;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    public static int Main(string[] args)
    {
        _output = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "MyLovePixel-ux-evidence");
        Directory.CreateDirectory(_output);
        try
        {
            // Never overwrite an existing developer/user palette while testing.
            foreach (var path in new[] { UserPaletteStore.DefaultFilePath, ColorLibraryStore.DefaultFilePath, CanvasDisplaySettingsStore.DefaultFilePath })
                if (File.Exists(path)) throw new InvalidOperationException("UX tests require an isolated user profile; existing data at " + path);
            var legacy = new UserPaletteStore();
            for (var i = 0; i < 48; i++) legacy.Add(new Rgba32((byte)(i * 5), (byte)(i * 3), (byte)(i * 2)));
            var legacyBytes = File.ReadAllBytes(legacy.FilePath);
            AppBuilder.Configure<SmokeApplication>().UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
            _window = new MainWindow();
            _window.Show(); Flush();
            var window = _window;
            var tabs = Find<TabControl>(window, "inspector-tabs");
            tabs.SelectedIndex = 1; Flush();
            Check(((TabItem)tabs.Items[1]!).Header?.ToString() == "Colors", "Colors has its own inspector tab");
            Check(tabs.Items.OfType<TabItem>().All(t => t.Header?.ToString() != "Photo"), "Redundant Photo tab is removed");
            var store = Field<ColorLibraryStore>("_userPaletteStore");
            Check(store.Colors.Count == 48 && File.ReadAllBytes(legacy.FilePath).SequenceEqual(legacyBytes), "All legacy colors migrate without changing the old file");
            var picker = Find<Button>(window, "studio-color-picker");
            Check(ToolTip.GetShowDelay(picker) == 750, "Picker tooltip waits 750 ms");
            Check(ToolTip.GetBetweenShowDelay(picker) == 0, "Adjacent controls do not bypass tooltip delay");
            Check(ToolTip.GetShowDelay(Find<TextBox>(window, "color-library-search")) == 750, "Tooltip timing applies to TextBox as well as Button");
            foreach (var size in new[] { new Size(1080, 700), new Size(1480, 920) })
            {
                window.Width = size.Width; window.Height = size.Height; Flush();
                Within(window, picker, "Picker at " + size);
                Within(window, Find<Button>(window, "color-library-save-current"), "Save current at " + size);
                Within(window, Find<TextBox>(window, "color-library-search"), "Search at " + size);
                var panel = Field<WrapPanel>("_userPaletteSwatches");
                var scroll = panel.GetVisualAncestors().OfType<ScrollViewer>().First();
                Check(scroll.Bounds.Height >= 110, "Saved swatches have a usable viewport at " + size + " (" + scroll.Bounds.Height + ")");
                Capture("colors-" + (int)size.Width);
            }
            var search = Field<TextBox>("_librarySearch");
            search.Focus(); window.KeyTextInput("not-a-color"); Flush();
            Check(Field<WrapPanel>("_userPaletteSwatches").Children.Count == 0, "Search filters immediately and shows an empty state");
            window.KeyPressQwerty(PhysicalKey.I, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.I, RawInputModifiers.None);
            Check(!Field<bool>("_eyedropperMode"), "Typing I in search does not activate eyedropper");
            search.Text = string.Empty; Flush();
            var folderId = store.CreateFolder("Characters");
            Call("RefreshUserPalette", new object?[] { null }); Flush();
            var folders = Field<ComboBox>("_libraryFolder");
            folders.SelectedIndex = 2; Flush();
            var hex = Field<TextBox>("_studioHex");
            hex.Text = "#65432180";
            Click(Find<Button>(window, "color-library-save-current"));
            store = Field<ColorLibraryStore>("_userPaletteStore");
            var saved = store.Colors.Single(c => c.Hex == "#65432180");
            Check(saved.FolderId == folderId && folders.SelectedIndex == 2, "Saving keeps the selected folder and reveals the saved color");
            var swatch = Field<WrapPanel>("_userPaletteSwatches").Children.OfType<Button>().Single();
            swatch.Focus(); Click(swatch);
            Check(swatch.IsFocused && ReferenceEquals(swatch, Field<WrapPanel>("_userPaletteSwatches").Children[0]), "Selecting a swatch preserves its control and keyboard focus");
            var libraryTabs = Field<TabControl>("_colorLibraryTabs");
            Click(Find<Button>(window, "color-library-pin-temporary"));
            hex.Text = "#112233"; Click(Find<Button>(window, "color-library-pin-temporary"));
            Check(libraryTabs.SelectedIndex == 0 && store.TemporaryColors.Count == 2, "Repeated temporary saves retain colors without switching tabs");
            Check(new ColorLibraryStore().TemporaryColors.Count == 2, "Temporary colors are persisted immediately");
            libraryTabs.SelectedIndex = 1; Flush(); Capture("temporary");
            libraryTabs.SelectedIndex = 0; Flush();
            hex.Text = "#654321"; Click(Find<Button>(window, "studio-hex-apply"));
            var activeBeforePicker = (Rgba32)Call("ActiveLibraryColor")!;
            Click(picker);
            var flyout = Field<Flyout>("_colorPickerFlyout");
            Check(flyout.IsOpen, "Picker opens on click");
            var body = (Control)flyout.Content!;
            var spectrum = body.GetVisualDescendants().OfType<Control>().Single(c => AutomationProperties.GetAutomationId(c) == "color-picker-spectrum");
            spectrum.GetType().GetMethod("SetHue")!.Invoke(spectrum, new object[] { 120d }); Flush();
            Check((Rgba32)Call("ActiveLibraryColor")! != activeBeforePicker, "Picker drag/hue preview changes active color");
            CaptureControl(body, "picker");
            var cancel = body.GetVisualDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "Cancel");
            Click(cancel);
            Check((Rgba32)Call("ActiveLibraryColor")! == activeBeforePicker && !flyout.IsOpen, "Cancel restores the original color and closes the picker");
            var workspace = Field<EditorWorkspace>("_workspace");
            workspace.NewDocument(1024, 1024); Flush();
            var session = (DocumentSession)Call("Current")!;
            var pixels = new byte[1024 * 1024 * 4];
            for (var y = 256; y < 768; y++)
            for (var x = 256; x < 768; x++)
            {
                var i = (y * 1024 + x) * 4;
                pixels[i] = 101; pixels[i + 1] = 67; pixels[i + 2] = 33; pixels[i + 3] = 128;
            }
            session.ReplaceCurrentCanvasWithRgba(pixels, "UX fixture"); Flush();
            Call("FitCanvas"); Flush();
            var canvas = Field<PixelCanvasView>("_canvas");
            var original = canvas.Presentation!.Rgba.ToArray();
            var history = session.Commands.HistoryDiagnostics.EstimatedHistoryBytes;
            var brightness = Find<Slider>(window, "canvas-backdrop-brightness");
            brightness.Value = 0; Flush(); Capture("canvas-1024-soft");
            brightness.Value = 100; Flush(); Capture("canvas-1024-bright");
            Check(canvas.Presentation!.Rgba.Span.SequenceEqual(original), "Backdrop brightness never modifies rendered artwork RGBA");
            Check(history == session.Commands.HistoryDiagnostics.EstimatedHistoryBytes, "Backdrop slider does not enter artwork undo history");
            canvas.SetInvert(true); Call("PickCanvasColor", 300, 300); Flush();
            Check((Rgba32)Call("ActiveLibraryColor")! == new Rgba32(101, 67, 33, 128), "Eyedropper reads original RGBA despite brightness and display inversion");
            canvas.SetInvert(false);
            brightness.Value = 37; Call("SaveCanvasDisplaySettings");
            Check(new CanvasDisplaySettingsStore().Current.BackgroundBrightness == 37, "Brightness setting is durably saved");
            window.Close(); _window = null; Flush();
            _window = new MainWindow(); _window.Show(); Flush();
            Check(Find<Slider>(_window, "canvas-backdrop-brightness").Value == 37, "Restart restores the brightness slider");
            Check(Field<ColorLibraryStore>("_userPaletteStore").TemporaryColors.Count == 2, "Restart retains temporary colors");
            Check(File.ReadAllBytes(legacy.FilePath).SequenceEqual(legacyBytes), "Legacy palette remains byte-identical after all UI operations");
            File.WriteAllText(Path.Combine(_output, "checks.json"), JsonSerializer.Serialize(new { passed = Checks.Count, checks = Checks }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"UX SMOKE PASSED: {Checks.Count} checks");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            File.WriteAllText(Path.Combine(_output, "failure.txt"), error.ToString());
            if (_window is not null) try { Capture("failure"); } catch { }
            return 1;
        }
        finally { _window?.Close(); }
    }

    private static T Field<T>(string name) => (T)(typeof(MainWindow).GetField(name, Flags)!.GetValue(_window) ?? throw new InvalidOperationException(name + " is null"));
    private static object? Call(string name, params object?[] args) => typeof(MainWindow).GetMethod(name, Flags)!.Invoke(_window, args);
    private static T Find<T>(Control root, string id) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => AutomationProperties.GetAutomationId(c) == id);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("UX CHECK FAILED: " + message);
        Checks.Add(message); Console.WriteLine("PASS " + message);
    }
    private static void Flush() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(); }
    private static void Within(Window window, Control control, string label)
    {
        var point = control.TranslatePoint(default, window) ?? throw new InvalidOperationException("Detached " + label);
        Check(point.X >= 0 && point.Y >= 0 && point.X + control.Bounds.Width <= window.ClientSize.Width + 1 && point.Y + control.Bounds.Height <= window.ClientSize.Height + 1, label + " is visible without clipping");
    }
    private static void Capture(string name)
    {
        Flush();
        using var frame = _window!.CaptureRenderedFrame() ?? throw new InvalidOperationException("No screenshot");
        frame.Save(Path.Combine(_output, name + ".png"));
    }
    private static void CaptureControl(Control control, string name)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)), new Vector(96, 96));
        bitmap.Render(control); bitmap.Save(Path.Combine(_output, name + ".png"));
    }
}
