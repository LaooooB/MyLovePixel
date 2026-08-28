using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Persistence;
using MyLovePixel.Render;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class HardeningApplicationTests
{
    [Fact]
    public void DirtySurfaceHistory_DrivesPartialRecomposeAndDebugRegion()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(4, 4);
        var cel = session.CaptureSnapshot().Cels.Single();

        var initial = session.RenderCanvas();
        Assert.Equal(RenderCacheOutcome.FullRecompose, initial.Diagnostics!.CacheOutcome);
        session.SetDirtyRegionVisualization(true);

        session.Execute(new PixelPatchCommand(
            cel.SurfaceId,
            [new PixelWrite(1, 1, new Rgba32(255, 0, 0, 255))]));
        session.Execute(new PixelPatchCommand(
            cel.SurfaceId,
            [new PixelWrite(2, 1, new Rgba32(0, 255, 0, 255))]));

        var changed = session.RenderCanvas();

        Assert.Equal(RenderCacheOutcome.PartialRecompose, changed.Diagnostics!.CacheOutcome);
        Assert.Equal(TextureUploadMode.Partial, changed.Diagnostics.UploadMode);
        Assert.Equal(2, changed.Diagnostics.UploadPixelCount);
        Assert.Equal(new IntRect(1, 1, 2, 1), Assert.Single(changed.DirtyRegions));

        var hit = session.RenderCanvas();
        Assert.Equal(RenderCacheOutcome.CacheHit, hit.Diagnostics!.CacheOutcome);
        Assert.Empty(hit.DirtyRegions);
    }

    [Fact]
    public void AutosaveRecovery_OpensDetachedDirtyCopyThatRequiresExplicitSavePath()
    {
        using var temp = new TempDirectory();
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(2, 1);
        var cel = session.CaptureSnapshot().Cels.Single();
        session.Execute(new PixelPatchCommand(
            cel.SurfaceId,
            [new PixelWrite(0, 0, new Rgba32(11, 22, 33, 255))]));
        var coordinator = new RecoveryWorkspaceCoordinator(
            workspace,
            temp.Path,
            new AutosavePolicy(TimeSpan.FromMinutes(1), RetentionCount: 2));
        var now = new DateTimeOffset(2026, 8, 28, 8, 0, 0, TimeSpan.Zero);

        var attempt = Assert.Single(coordinator.Tick(now));
        Assert.True(attempt.WroteCheckpoint);
        var candidate = Assert.Single(coordinator.Discover().Where(value => value.IsRecoverable));

        var recovered = coordinator.Recover(candidate.RecoveryId);

        Assert.True(recovered.IsRecovered);
        Assert.True(recovered.IsDirty);
        Assert.Null(recovered.FilePath);
        var recoveredCel = recovered.CaptureSnapshot().Cels.Single();
        Assert.Equal(
            new Rgba32(11, 22, 33, 255),
            recovered.CaptureSnapshot().GetSurface(recoveredCel.SurfaceId).GetPixel(0, 0));

        var savePath = Path.Combine(temp.Path, "explicit-recovered-save.pixelproj");
        workspace.Save(recovered, savePath);

        Assert.False(recovered.IsRecovered);
        Assert.False(recovered.IsDirty);
        Assert.Equal(Path.GetFullPath(savePath), recovered.FilePath);
        Assert.True(File.Exists(savePath));
        Assert.True(coordinator.Dismiss(candidate.RecoveryId));
        Assert.DoesNotContain(coordinator.Discover(), value => value.RecoveryId == candidate.RecoveryId);
    }

    [Fact]
    public void AutosavePolicy_DoesNotRewriteBeforeInterval()
    {
        using var temp = new TempDirectory();
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(1, 1);
        var cel = session.CaptureSnapshot().Cels.Single();
        session.Execute(new PixelPatchCommand(
            cel.SurfaceId,
            [new PixelWrite(0, 0, new Rgba32(1, 2, 3, 255))]));
        var coordinator = new RecoveryWorkspaceCoordinator(
            workspace,
            temp.Path,
            new AutosavePolicy(TimeSpan.FromMinutes(2), RetentionCount: 3));
        var firstTime = new DateTimeOffset(2026, 8, 28, 8, 0, 0, TimeSpan.Zero);

        Assert.Single(coordinator.Tick(firstTime));
        Assert.Empty(coordinator.Tick(firstTime.AddSeconds(119)));
        Assert.Single(coordinator.Tick(firstTime.AddMinutes(2)));
        Assert.Equal(2, coordinator.Discover().Count(value => value.IsRecoverable));
    }

    [Fact]
    public void ThousandFrameTimeline_OnlyMaterializesRequestedWindow()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var first = document.FrameOrder.Single();
        var bus = new CommandBus(document);
        for (var index = 1; index < 1000; index++)
            bus.Execute(new CopyFrameCommand(first, FrameCopyMode.Linked));

        var session = new DocumentSession(new PixelProject(document));
        var window = session.GetTimelineWindow(500, 24);

        Assert.Equal(1000, window.TotalCount);
        Assert.Equal(500, window.StartIndex);
        Assert.Equal(24, window.Items.Count);
        Assert.Equal(500, window.Items[0].Index);
        Assert.Equal(523, window.Items[^1].Index);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MyLovePixel-ApplicationHardening", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
