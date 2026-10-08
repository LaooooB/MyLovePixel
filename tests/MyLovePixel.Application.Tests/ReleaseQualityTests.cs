using System.Reflection;
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
