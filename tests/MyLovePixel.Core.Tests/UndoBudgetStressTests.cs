using MyLovePixel.Commands;
using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Core.Tests;

public sealed class UndoBudgetStressTests
{
    [Fact]
    public void FiveThousandCommands_RemainBoundedByHistoryBudget()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var cel = document.Cels.Single();
        var bus = new CommandBus(document, new UndoHistoryOptions(MemoryBudgetBytes: 4096));

        for (var index = 0; index < 5000; index++)
        {
            var value = checked((byte)((index % 251) + 1));
            bus.Execute(new PixelPatchCommand(
                cel.SurfaceId,
                [new PixelWrite(0, 0, new Rgba32(value, 0, 0, 255))]));
        }

        var diagnostics = bus.HistoryDiagnostics;
        Assert.True(diagnostics.EstimatedHistoryBytes <= diagnostics.MemoryBudgetBytes);
        Assert.True(diagnostics.UndoCount <= 24);
        Assert.True(diagnostics.EvictedUndoEntryCount >= 4_900);
        Assert.Equal(0, diagnostics.RedoCount);
        Assert.True(bus.CanUndo);

        while (bus.CanUndo) bus.Undo();
        Assert.False(bus.CanUndo);
    }
}
