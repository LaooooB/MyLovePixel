using MyLovePixel.Commands;
using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Core.Tests;

public sealed class UndoBudgetTests
{
    [Fact]
    public void Budget_EvictsOldestEntriesAndKeepsRecentUndoChain()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        var bus = new CommandBus(document, new UndoHistoryOptions(MemoryBudgetBytes: 400));

        for (byte value = 1; value <= 5; value++)
            bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, new Rgba32(value, 0, 0, 255))]));

        Assert.Equal(2, bus.UndoCount);
        Assert.Equal(3, bus.HistoryDiagnostics.EvictedUndoEntryCount);
        Assert.True(bus.HistoryDiagnostics.EstimatedHistoryBytes <= 400);

        bus.Undo();
        Assert.Equal(new Rgba32(4, 0, 0, 255), surface.GetPixel(0, 0));
        bus.Undo();
        Assert.Equal(new Rgba32(3, 0, 0, 255), surface.GetPixel(0, 0));
        Assert.False(bus.CanUndo);
    }

    [Fact]
    public void SingleOversizeNewestEntry_IsRetainedAndReported()
    {
        var document = PixelDocumentFactory.CreateBlank(8, 8);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document, new UndoHistoryOptions(MemoryBudgetBytes: 128));
        var writes = Enumerable.Range(0, 64)
            .Select(index => new PixelWrite(index % 8, index / 8, new Rgba32(1, 2, 3, 255)))
            .ToArray();

        bus.Execute(new PixelPatchCommand(cel.SurfaceId, writes));

        Assert.Equal(1, bus.UndoCount);
        Assert.True(bus.HistoryDiagnostics.IsOverBudget);
        bus.Undo();
        Assert.All(document.Resources.GetSurface(cel.SurfaceId).Snapshot().Bytes.ToArray(), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void RedoBranch_IsClearedBeforeNewEntryBudgetAccounting()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document, new UndoHistoryOptions(MemoryBudgetBytes: 400));

        bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, new Rgba32(1, 0, 0, 255))]));
        bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, new Rgba32(2, 0, 0, 255))]));
        bus.Undo();
        Assert.Equal(1, bus.RedoCount);

        bus.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, new Rgba32(3, 0, 0, 255))]));

        Assert.Equal(0, bus.RedoCount);
        Assert.True(bus.HistoryDiagnostics.EstimatedHistoryBytes <= 400);
    }
}
