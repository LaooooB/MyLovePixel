from pathlib import Path
p = Path('src/MyLovePixel.Desktop/MainWindow.InteractionUx.cs')
s = p.read_text(encoding='utf-8')
old = '            row.Children.Add(content);\n'
assert old in s
s = s.replace(old, '            button.Content = null;\n            row.Children.Add(content);\n', 1)
p.write_text(s, encoding='utf-8')
p = Path('tests/MyLovePixel.Desktop.UxTests/Program.cs')
s = p.read_text(encoding='utf-8')
s = s.replace('private static int _failures;', 'private static int _failures;\n    private static int _tests;')
s = s.replace('var window = new MainWindow();', 'var window = new MainWindow();\n        _tests++;')
s = s.replace('Find<T>(MainWindow window, string id)', 'Find<T>(Window window, string id)')
anchor = '        Console.WriteLine($"Desktop UX tests: {9 - _failures}/9 passed.");'
assert anchor in s
extra = r'''
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
        Console.WriteLine($"Desktop UX tests: {_tests - _failures}/{_tests} passed.");'''
s = s.replace(anchor, extra)
s = s.replace('            Console.WriteLine("PASS " + name);', '''            Directory.CreateDirectory("release/ui");
            using (var frame = window.CaptureRenderedFrame())
                frame?.Save($"release/ui/{_tests:00}.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            Console.WriteLine("PASS " + name);''')
s = s.replace('            Console.WriteLine("FAIL " + name + ": " + ex);', '''            Console.WriteLine("FAIL " + name + ": " + ex);
            Console.WriteLine("Notice: " + Field<TextBlock>(window, "_notice").Text);
            Directory.CreateDirectory("release/ui");
            using (var frame = window.CaptureRenderedFrame())
                frame?.Save($"release/ui/{_tests:00}-failed.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);''')
p.write_text(s, encoding='utf-8')
