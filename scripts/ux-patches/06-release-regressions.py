from pathlib import Path
p=Path('tests/MyLovePixel.Application.Tests/ReleaseQualityTests.cs')
p.write_text(r'''using System.Reflection;
using MyLovePixel.Application;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Commands.Resources;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Persistence;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class ReleaseQualityTests
{
    [Fact]
    public void Undo_to_saved_content_clears_dirty_state_and_redo_restores_it()
    {
        var workspace = new EditorWorkspace(); var session = workspace.NewDocument(8, 8);
        var directory = Path.Combine(Path.GetTempPath(), "mlp-ux-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            workspace.Save(session, Path.Combine(directory, "sprite.pixelproj"));
            session.SetLayerOpacity(session.CurrentLayerId, 100); Assert.True(session.IsDirty);
            session.Undo(); Assert.False(session.IsDirty);
            session.Redo(); Assert.True(session.IsDirty);
            session.Undo(); session.RenameLayer(session.CurrentLayerId, "Different branch"); Assert.True(session.IsDirty);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public void Canceling_parameter_gesture_restores_clean_state()
    {
        var session = new EditorWorkspace().NewDocument(8, 8);
        using var gesture = session.BeginUserEdit("Opacity");
        session.SetLayerOpacity(session.CurrentLayerId, 80); gesture.Finish(false);
        Assert.Equal((byte)255, session.GetLayers().Single().Opacity);
        Assert.False(session.IsDirty); Assert.Equal(0, session.Commands.UndoCount);
    }
    [Fact]
    public void Current_layer_sampling_preserves_duplicate_palette_index()
    {
        var session = new EditorWorkspace().NewDocument(8, 8);
        var cel = session.CaptureSnapshot().Cels.Single(); var red = new Rgba32(200, 20, 30, 255);
        var palette = new AddPaletteCommand([Rgba32.Transparent, red, red], 0); session.Execute(palette);
        session.Execute(new ReplacePixelSurfaceCommand(cel.SurfaceId, PixelFormat.Indexed8, palette.PaletteId, Enumerable.Repeat((byte)2, 64).ToArray(), "Seed indexed"));
        var revision = session.DocumentVersion; var count = session.Commands.UndoCount;
        var sample = session.SampleCanvasPixel(3, 4, true)!; session.ApplySampledColor(sample);
        Assert.Equal((byte)2, sample.PaletteIndex); Assert.Equal((byte)2, session.PrimarySample!.PaletteIndex);
        Assert.Equal(red, session.GetToolColors().Primary); Assert.Equal(revision, session.DocumentVersion); Assert.Equal(count, session.Commands.UndoCount);
        Assert.Throws<InvalidOperationException>(() => session.ApplySampledColor(new PixelSample(new Rgba32(10, 50, 90, 255))));
        Assert.Equal(revision, session.DocumentVersion);
    }
    [Fact]
    public void Sampling_locked_hidden_layer_is_read_only_and_raw()
    {
        var session = new EditorWorkspace().NewDocument(8, 8); var cel = session.CaptureSnapshot().Cels.Single();
        var color = new Rgba32(22, 44, 66, 128);
        session.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(2, 2, color)], "Seed"));
        session.SetLayerLocked(session.CurrentLayerId, true); session.SetLayerVisibility(session.CurrentLayerId, false);
        var revision = session.DocumentVersion;
        Assert.Equal(color, session.SampleCanvasPixel(2, 2, true)!.Color);
        Assert.Equal((byte)0, session.SampleCanvasPixel(2, 2, false)!.Color.A);
        Assert.Equal(revision, session.DocumentVersion);
    }
    [Fact]
    public async Task Background_recovery_captures_content_before_later_edits()
    {
        var workspace = new EditorWorkspace(); var session = workspace.NewDocument(8, 8);
        var directory = Path.Combine(Path.GetTempPath(), "mlp-recovery-" + Guid.NewGuid().ToString("N"));
        var recovery = new RecoveryWorkspaceCoordinator(workspace, directory);
        try
        {
            session.SetLayerOpacity(session.CurrentLayerId, 123);
            var method = typeof(RecoveryWorkspaceCoordinator).GetMethod("TickAsync", [typeof(DateTimeOffset)]);
            Assert.True(method is not null, "Recovery disk writes must run from a detached snapshot off the UI thread.");
            var task = (Task<IReadOnlyList<AutosaveAttemptPresentation>>)method!.Invoke(recovery, [DateTimeOffset.UtcNow])!;
            session.SetLayerOpacity(session.CurrentLayerId, 200);
            var result = await task; Assert.Single(result); Assert.True(result[0].WroteCheckpoint);
            var recovered = recovery.Recover(result[0].RecoveryId!);
            Assert.Equal((byte)123, recovered.GetLayers().Single().Opacity);
            Assert.Equal((byte)200, session.GetLayers().Single().Opacity);
            Assert.True(session.IsDirty);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
''',encoding='utf-8')
p=Path('tests/MyLovePixel.Desktop.UxTests/Program.cs'); s=p.read_text(encoding='utf-8')
a='        Console.WriteLine($"Desktop UX tests: {_tests - _failures}/{_tests} passed.");'; assert a in s
s=s.replace(a,r'''
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
            foreach (var session in workspace.Sessions.ToArray()) workspace.Close(session); Pump();
            var export = window.GetVisualDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Export");
            Check(!export.IsEnabled, "Export is enabled without a document.");
        });
''' + a)
p.write_text(s,encoding='utf-8')
# Collect independent UI failures even when a new application regression is red.
p=Path('.github/workflows/ux-release.yml'); s=p.read_text(encoding='utf-8')
s=s.replace("          if ($LASTEXITCODE -ne 0) { throw 'Solution tests failed.' }", "          $coreExit = $LASTEXITCODE\n          $uiExit = 0")
s=s.replace("            if ($LASTEXITCODE -ne 0) { throw 'Desktop interaction tests failed.' }", "            $uiExit = $LASTEXITCODE")
s=s.replace('      - name: Record tested source', "          if ($coreExit -ne 0 -or $uiExit -ne 0) { throw 'Regression tests failed.' }\n      - name: Record tested source")
s=s.replace('git add src tests docs HANDOFF.md','git add --all -- src tests docs scripts .github HANDOFF.md')
p.write_text(s,encoding='utf-8')
