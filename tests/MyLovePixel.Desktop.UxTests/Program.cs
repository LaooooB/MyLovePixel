using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
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
            .With(new Avalonia.Media.FontManagerOptions { DefaultFamilyName = "avares://Avalonia.Fonts.Inter/Assets#Inter" })
            .WithInterFont()
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
        Run("Canvas participates in native accessibility with a stable identity", window =>
        {
            var canvas = Field<PixelCanvasView>(window, "_canvas");
            var peer = ControlAutomationPeer.CreatePeerForElement(canvas);
            Check(peer is not null, "The custom canvas has no automation peer.");
            Check(peer!.IsControlElement() && peer.IsContentElement(), "Canvas is missing from the accessibility tree.");
            Check(peer.GetAutomationId() == "workspace.canvas" && peer.GetName() == "Pixel canvas", "Canvas accessibility identity is not stable.");
            Check(peer.IsKeyboardFocusable(), "The accessible canvas cannot receive keyboard focus.");
            Check(peer.GetBoundingRectangle().Width > 0, "Canvas automation bounds are empty.");
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
        Run("Right drag erases every crossed pixel as one undoable gesture", window =>
        {
            var session = Session(window);
            var cel = session.CaptureSnapshot().Cels.First();
            var color = new Rgba32(180, 70, 30, 255);
            session.Execute(new PixelPatchCommand(cel.SurfaceId,
            [
                new PixelWrite(2, 3, color),
                new PixelWrite(3, 3, color),
                new PixelWrite(4, 3, color),
            ], "Seed erase drag"));
            Pump();
            Click(Find<Button>(window, "tool.core.pencil"));
            var canvas = Field<PixelCanvasView>(window, "_canvas");
            Point At(int x) => canvas.TranslatePoint(new Point((x + .5) * canvas.Zoom, 3.5 * canvas.Zoom), window)!.Value;
            var before = session.Commands.UndoCount;
            window.MouseDown(At(2), MouseButton.Right);
            window.MouseMove(At(3));
            window.MouseMove(At(4));
            window.MouseUp(At(4), MouseButton.Right);
            Pump();
            Check(session.GetCanvasPixel(2, 3).A == 0 && session.GetCanvasPixel(3, 3).A == 0 && session.GetCanvasPixel(4, 3).A == 0,
                "Holding right mouse and dragging did not erase every crossed pixel.");
            Check(session.Commands.UndoCount == before + 1, "A continuous right-button erase drag must be one undo step.");
            session.Undo(); Pump();
            Check(session.GetCanvasPixel(2, 3) == color && session.GetCanvasPixel(3, 3) == color && session.GetCanvasPixel(4, 3) == color,
                "Undo did not restore the full right-button erase gesture.");
        });
        Run("Hover visuals fade in and out instead of snapping", window =>
        {
            var button = Find<Button>(window, "project.save");
            var transitions = button.Transitions;
            Check(transitions is not null && transitions.OfType<Avalonia.Animation.BrushTransition>().Any(t => t.Property == Button.BackgroundProperty),
                "Button hover background has no fade transition.");
            Check(transitions!.OfType<Avalonia.Animation.BrushTransition>().Any(t => t.Property == Button.BorderBrushProperty),
                "Button hover border has no fade transition.");
            Check(transitions.All(t => t.Duration >= TimeSpan.FromMilliseconds(90) && t.Duration <= TimeSpan.FromMilliseconds(240)),
                "Hover fade timing is outside the short interaction range.");
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

        foreach (var scale in new[] { 1.25, 1.5, 2.0 })
        {
            Run($"DPI {scale * 100:0}% retains readable values and canvas", window =>
            {
                window.SetRenderScaling(scale); window.Width = 1280 / scale; window.Height = 800 / scale; Pump();
                Call(window, "FitCanvas"); Pump();
                var input = Find<NumericUpDown>(window, "color.alpha");
                Check(input.GetVisualDescendants().OfType<TextBox>().Any(t => t.Bounds.Width >= 25 && t.Text == "255"), "Alpha value is clipped at this DPI.");
                Check(Find<Button>(window, "tool.core.eyedropper").Bounds.Width >= 120, "Named tool row became too narrow.");
                var viewport = Field<ScrollViewer>(window, "_canvasScroll");
                Check(viewport.Bounds.Width >= 150 && viewport.Bounds.Height >= 90, "Dock panels consumed the canvas work area.");
                using var frame = window.CaptureRenderedFrame();
                Check(frame is not null && Math.Abs(frame.PixelSize.Width - window.Bounds.Width * scale) <= 2, "DPI test did not change physical render scaling.");
                frame!.Save($"release/ui/dpi-{scale * 100:0}.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            });
        }
        Run("Canvas-edge transform handles remain reachable", window =>
        {
            var session = Session(window); Call(window, "SelectQuickTool", "workspace.selection");
            Field<SelectionWorkspaceRuntime>(window, "_selection").SelectAll(session); Call(window, "RefreshCanvas", true); Pump();
            var canvas = Field<PixelCanvasView>(window, "_canvas");
            var method = typeof(PixelCanvasView).GetMethod("HandlePoint", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(method is not null, "Transform handles at canvas edges need a visible hit area.");
            var point = (Point)method!.Invoke(canvas, new object[] { new Point(0, 0) })!;
            Check(point.X >= 7 && point.Y >= 7, "Edge handle is clipped.");
        });
        Run("Export fields survive retry and invalid filenames stay in dialog", window =>
        {
            var previous = new MyLovePixel.Export.ExportPreset { Scale = 3, ImageBaseName = "retry", MetadataFileName = "retry.json" };
            var constructor = typeof(ExportDialog).GetConstructor([typeof(MyLovePixel.Export.ExportPreset)]);
            Check(constructor is not null, "Export options are lost when retrying.");
            var dialog = (ExportDialog)constructor!.Invoke([previous]); dialog.ShowDialog<object?>(window); Pump();
            Check(Field<NumericUpDown>(dialog, "_scale").Value == 3, "Export scale was reset.");
            Field<TextBox>(dialog, "_fileName").Text = "../bad";
            var accept = dialog.GetVisualDescendants().OfType<Button>().Single(b => b.IsDefault); Click(accept); Pump();
            Check(dialog.IsVisible, "Invalid filename closed the export dialog."); dialog.Close(null); Pump();
        });
        Run("Empty workspace disables document-only controls", window =>
        {
            var workspace = Field<EditorWorkspace>(window, "_workspace");
            foreach (var session in workspace.Sessions.ToArray()) workspace.Close(session);
            Call(window, "RefreshAll", false); Pump();
            Check(!Find<Button>(window, "project.save").IsEffectivelyEnabled, "Save is enabled with no document.");
            Check(!Find<Button>(window, "tool.workspace.selection").IsEffectivelyEnabled, "Selection is enabled with no document.");
        });
        Run("Effect adjustments form one undoable gesture", window =>
        {
            var session = Session(window);
            var plugins = Field<PluginWorkspaceRuntime>(window, "_plugins");
            var id = plugins.AddEffect(session, "core.blur");
            Pump();
            Field<TabControl>(window, "_sideTabs").SelectedIndex = 3; Pump(); Call(window, "RefreshEffects"); Pump();
            var radius = Field<StackPanel>(window, "_effectsPanel").GetVisualDescendants().OfType<NumericUpDown>().First();
            radius.Focus(); var before = session.Commands.UndoCount;
            radius.Value = 2; radius.Value = 3; radius.Value = 4;
            Call(window, "FinishParameterEdit", true); Pump();
            Check(session.Commands.UndoCount == before + 1, "Continuous effect adjustment flooded undo history.");
            session.Undo(); Pump();
            Check(plugins.GetEffectParameters(session, id).First(p => p.Key == "radius").Value.IntegerValue == 1, "Undo did not restore the original radius.");
        });
        Run("Canceling a focused parameter restores its initial value", window =>
        {
            var session = Session(window); Field<TabControl>(window, "_sideTabs").SelectedIndex = 1; Pump();
            var opacity = Field<StackPanel>(window, "_layersPanel").GetVisualDescendants().OfType<NumericUpDown>().First();
            opacity.Focus(); opacity.Value = 40;
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Pump();
            Check(session.GetLayers().Single().Opacity == 255 && !session.IsDirty && session.Commands.UndoCount == 0, "Escape committed or stranded a parameter gesture.");
        });
        Run("Toggle labels follow changes from another panel", window =>
        {
            var toggle = window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>()
                .First(b => (b.Content as string)?.StartsWith("Onion Skin:") == true);
            typeof(MainWindow).GetField("_onionSkin", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
            Call(window, "RefreshAll", false); Pump();
            Check(toggle.IsChecked == true && (toggle.Content as string)?.EndsWith(": On") == true, "Onion skin label disagrees with actual state.");
        });
        Run("Primary action label has readable contrast", window =>
        {
            var save = Find<Button>(window, "project.save");
            var text = save.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Save");
            var fg = ((Avalonia.Media.ISolidColorBrush)text.Foreground!).Color;
            var bg = ((Avalonia.Media.ISolidColorBrush)save.Background!).Color;
            Check(Contrast(fg, bg) >= 4.5, "Save label has insufficient contrast against its highlighted background.");
        });
        Run("Actual canvas-edge resize can be canceled without mutation", window =>
        {
            var session = Session(window); Call(window, "SelectQuickTool", "workspace.selection");
            Field<SelectionWorkspaceRuntime>(window, "_selection").SelectAll(session); Call(window, "RefreshCanvas", true); Pump();
            var canvas = Field<PixelCanvasView>(window, "_canvas"); var revision = session.DocumentVersion;
            var p = canvas.TranslatePoint(new Point(9, 9), window)!.Value;
            window.MouseDown(p, MouseButton.Left); window.MouseMove(p + new Vector(12, 12)); Pump();
            Check(Field<object?>(canvas, "_activeSelectionTransform")?.ToString() == "ScaleTopLeft", "Visible corner did not start a resize.");
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); window.MouseUp(p, MouseButton.Left); Pump();
            Check(!canvas.HasActivePointer && session.DocumentVersion == revision, "Cancel changed artwork or stranded capture.");
        });
        Run("Export validates Windows names and retains unrelated options", window =>
        {
            var previous = new MyLovePixel.Export.ExportPreset { Scale = 2, Padding = 3, MaxAtlasWidth = 1024, ImageBaseName = "art", MetadataFileName = "meta/art.json" };
            foreach (var name in new[] { "CON", "bad?name", "trailing.", "folder\art" })
            {
                var dialog = new ExportDialog(previous); dialog.ShowDialog<object?>(window); Pump();
                Field<TextBox>(dialog, "_fileName").Text = name; Click(dialog.GetVisualDescendants().OfType<Button>().Single(b => b.IsDefault));
                Check(dialog.IsVisible, "An invalid Windows name closed the dialog: " + name); dialog.Close(null); Pump();
            }
            var valid = new ExportDialog(previous); var result = valid.ShowDialog<MyLovePixel.Export.ExportPreset?>(window); Pump();
            Click(valid.GetVisualDescendants().OfType<Button>().Single(b => b.IsDefault)); Pump();
            Check(result.IsCompletedSuccessfully && result.Result?.MaxAtlasWidth == 1024 && result.Result?.MetadataFileName == "meta/art.json", "Retry overwrote options outside this dialog.");
        });
        Run("Sidebar tabs stay usable after document edits", window =>
        {
            var tabs = Field<TabControl>(window, "_sideTabs");
            foreach (var tab in new[] { 1, 2, 3, 0 })
            {
                tabs.SelectedIndex = tab; Pump();
                Session(window).SetToolColors(new Rgba32(12, 34, 56, 255), Rgba32.Transparent); Pump();
                Check(tabs.SelectedIndex == tab, "An edit reset the active sidebar tab.");
                Check(!Field<Border>(window, "_noticeHost").IsVisible, "Opening a sidebar caused an error.");
                using var image = window.CaptureRenderedFrame();
                image?.Save($"release/ui/sidebar-{tab}.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
        });
        Run("Repeated painting, zoom and undo retain a consistent editor", window =>
        {
            var session = Field<EditorWorkspace>(window, "_workspace").NewDocument(256, 256);
            Pump(); Call(window, "FitCanvas"); Pump();
            var canvas = Field<PixelCanvasView>(window, "_canvas");
            session.SetToolColors(new Rgba32(60, 160, 210, 255), Rgba32.Transparent);
            var timings = new List<double>(); var before = session.Commands.UndoCount;
            var memory = GC.GetTotalMemory(true);
            for (var i = 0; i < 96; i++)
            {
                var point = canvas.TranslatePoint(new Point((40.5 + i % 24 * 4) * canvas.Zoom, (60.5 + i / 24 * 8) * canvas.Zoom), window)!.Value;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Pump();
                timings.Add(watch.Elapsed.TotalMilliseconds);
            }
            Check(session.Commands.UndoCount == before + 96, "Repeated painting lost or duplicated an edit.");
            var retained = GC.GetTotalMemory(true) - memory;
            var ordered = timings.Order().ToArray();
            File.WriteAllText("release/performance.json", System.Text.Json.JsonSerializer.Serialize(new
            {
                environment = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                processors = Environment.ProcessorCount,
                scenario = "256x256 canvas; 96 real pointer strokes and UI/render queue drains; headless Skia, not physical input latency",
                p50Milliseconds = ordered[ordered.Length / 2], p95Milliseconds = ordered[(int)(ordered.Length * .95)],
                maxMilliseconds = ordered[^1], retainedManagedBytes = retained,
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            for (var i = 0; i < 96; i++) session.Undo(); Pump();
            Check(session.Commands.UndoCount == before && !session.IsDirty, "Repeated undo did not return to the initial document.");
            Check(Find<Button>(window, "tool.core.eyedropper").IsEffectivelyEnabled, "Repeated editing left controls disabled.");
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
    private static double Contrast(Avalonia.Media.Color a, Avalonia.Media.Color b)
    {
        static double Channel(byte c) { var v = c / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
        static double Light(Avalonia.Media.Color c) => .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
        var x = Light(a); var y = Light(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}