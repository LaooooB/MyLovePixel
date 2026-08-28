using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Persistence;
using Xunit;

namespace MyLovePixel.Recovery.Tests;

public sealed class RecoveryCrashMatrixTests
{
    [Theory]
    [InlineData(RecoveryWriteStage.BeforeCheckpoint, false)]
    [InlineData(RecoveryWriteStage.AfterCheckpointValidated, false)]
    [InlineData(RecoveryWriteStage.BeforeJournalCommit, false)]
    [InlineData(RecoveryWriteStage.AfterJournalCommit, true)]
    [InlineData(RecoveryWriteStage.BeforeRotation, true)]
    [InlineData(RecoveryWriteStage.AfterRotation, true)]
    public void FailureAtEveryWriteStage_LeavesAtLeastOneVerifiedRecoveryPoint(
        RecoveryWriteStage stage,
        bool newCheckpointShouldBeDiscoverable)
    {
        using var temp = new TempDirectory();
        var store = new RecoveryStore(new RecoveryOptions(temp.Path, RetentionCount: 1));
        var project = new PixelProject(PixelDocumentFactory.CreateBlank(1, 1));
        var baseTime = new DateTimeOffset(2026, 8, 28, 8, 0, 0, TimeSpan.Zero);
        var first = store.WriteCheckpoint(project, createdUtc: baseTime);
        Paint(project, new Rgba32(7, 8, 9, 255));

        Assert.Throws<InjectedRecoveryFailure>(() => store.WriteCheckpoint(
            project,
            createdUtc: baseTime.AddMinutes(1),
            failureInjector: new ThrowingInjector(stage)));

        var discovery = store.Discover();
        Assert.NotEmpty(discovery.ValidCandidates);
        var newest = Assert.IsType<RecoveryCandidate>(discovery.NewestValid);
        var recovered = store.Recover(newest);
        var cel = recovered.Document.Cels.Single();
        var color = recovered.Document.Resources.GetSurface(cel.SurfaceId).GetPixel(0, 0);

        Assert.Equal(
            newCheckpointShouldBeDiscoverable ? new Rgba32(7, 8, 9, 255) : Rgba32.Transparent,
            color);

        if (newCheckpointShouldBeDiscoverable)
            Assert.NotEqual(first.RecoveryId, newest.RecoveryId);
        else
            Assert.Equal(first.RecoveryId, newest.RecoveryId);
    }

    private static void Paint(PixelProject project, Rgba32 color)
    {
        var cel = project.Document.Cels.Single();
        new CommandBus(project.Document).Execute(new PixelPatchCommand(
            cel.SurfaceId,
            [new PixelWrite(0, 0, color)]));
    }

    private sealed class ThrowingInjector(RecoveryWriteStage stage) : IRecoveryFailureInjector
    {
        public void Checkpoint(RecoveryWriteStage current)
        {
            if (current == stage) throw new InjectedRecoveryFailure();
        }
    }

    private sealed class InjectedRecoveryFailure : Exception;

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "MyLovePixel-RecoveryCrashMatrix",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
