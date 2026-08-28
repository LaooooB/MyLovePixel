using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Persistence;
using Xunit;

namespace MyLovePixel.Recovery.Tests;

public sealed class RecoveryStoreTests
{
    [Fact]
    public void WriteDiscoverRecover_RoundTripsLatestCheckpoint()
    {
        using var temp = new TempDirectory();
        var store = new RecoveryStore(new RecoveryOptions(temp.Path, RetentionCount: 3));
        var project = NewProject();
        Paint(project, 0, new Rgba32(10, 20, 30, 255));

        var written = store.WriteCheckpoint(project, "/games/hero.pixelproj", new DateTimeOffset(2026, 8, 28, 1, 2, 3, TimeSpan.Zero));
        var discovery = store.Discover();

        var candidate = Assert.Single(discovery.ValidCandidates);
        Assert.Equal(written.RecoveryId, candidate.RecoveryId);
        Assert.Equal(Path.GetFullPath("/games/hero.pixelproj"), candidate.SourcePath);
        var recovered = store.Recover(candidate);
        var cel = recovered.Document.Cels.Single();
        Assert.Equal(new Rgba32(10, 20, 30, 255), recovered.Document.Resources.GetSurface(cel.SurfaceId).GetPixel(0, 0));
    }

    [Fact]
    public void Rotation_KeepsNewestVerifiedCheckpointsOnly()
    {
        using var temp = new TempDirectory();
        var store = new RecoveryStore(new RecoveryOptions(temp.Path, RetentionCount: 2));
        var project = NewProject();
        var baseTime = new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);

        var first = store.WriteCheckpoint(project, createdUtc: baseTime);
        Paint(project, 0, new Rgba32(1, 0, 0, 255));
        var second = store.WriteCheckpoint(project, createdUtc: baseTime.AddMinutes(1));
        Paint(project, 0, new Rgba32(2, 0, 0, 255));
        var third = store.WriteCheckpoint(project, createdUtc: baseTime.AddMinutes(2));

        var valid = store.Discover().ValidCandidates;
        Assert.Equal(2, valid.Count);
        Assert.Equal(third.RecoveryId, valid[0].RecoveryId);
        Assert.Equal(second.RecoveryId, valid[1].RecoveryId);
        Assert.False(File.Exists(first.JournalPath));
        Assert.False(File.Exists(first.CheckpointPath));
    }

    [Fact]
    public void CrashBeforeJournalCommit_PreservesPreviousRecoverableCheckpoint()
    {
        using var temp = new TempDirectory();
        var store = new RecoveryStore(new RecoveryOptions(temp.Path, RetentionCount: 1));
        var project = NewProject();
        var first = store.WriteCheckpoint(project, createdUtc: new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero));
        Paint(project, 0, new Rgba32(9, 9, 9, 255));

        var injector = new ThrowingInjector(RecoveryWriteStage.BeforeJournalCommit);
        Assert.Throws<InjectedRecoveryFailure>(() =>
            store.WriteCheckpoint(project, createdUtc: new DateTimeOffset(2026, 8, 28, 0, 1, 0, TimeSpan.Zero), failureInjector: injector));

        var candidate = Assert.Single(store.Discover().ValidCandidates);
        Assert.Equal(first.RecoveryId, candidate.RecoveryId);
        var recovered = store.Recover(candidate);
        var cel = recovered.Document.Cels.Single();
        Assert.Equal(Rgba32.Transparent, recovered.Document.Resources.GetSurface(cel.SurfaceId).GetPixel(0, 0));
    }

    [Fact]
    public void CorruptCheckpoint_IsReportedAndDoesNotHideOlderValidCheckpoint()
    {
        using var temp = new TempDirectory();
        var store = new RecoveryStore(new RecoveryOptions(temp.Path, RetentionCount: 3));
        var project = NewProject();
        var first = store.WriteCheckpoint(project, createdUtc: new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero));
        Paint(project, 0, new Rgba32(3, 4, 5, 255));
        var second = store.WriteCheckpoint(project, createdUtc: new DateTimeOffset(2026, 8, 28, 0, 1, 0, TimeSpan.Zero));
        File.WriteAllBytes(second.CheckpointPath, [1, 2, 3, 4]);

        var discovery = store.Discover();
        Assert.Contains(discovery.Candidates, candidate => candidate.RecoveryId == second.RecoveryId && candidate.State == RecoveryCandidateState.CorruptCheckpoint);
        Assert.Equal(first.RecoveryId, discovery.NewestValid?.RecoveryId);
    }

    [Fact]
    public void InvalidJournal_IsStructuredCandidateInsteadOfThrowingDiscovery()
    {
        using var temp = new TempDirectory();
        var documentDirectory = Path.Combine(temp.Path, "bad");
        Directory.CreateDirectory(documentDirectory);
        var journalPath = Path.Combine(documentDirectory, "broken.recovery.json");
        File.WriteAllText(journalPath, "{not-json");
        var store = new RecoveryStore(new RecoveryOptions(temp.Path));

        var candidate = Assert.Single(store.Discover().Candidates);
        Assert.Equal(RecoveryCandidateState.InvalidJournal, candidate.State);
        Assert.False(candidate.IsRecoverable);
    }

    private static PixelProject NewProject() => new(PixelDocumentFactory.CreateBlank(2, 1));

    private static void Paint(PixelProject project, int x, Rgba32 color)
    {
        var cel = project.Document.Cels.Single();
        var bus = new CommandBus(project.Document);
        bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(x, 0, color)]));
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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MyLovePixel-RecoveryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
