using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Persistence;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class EditingFacadeTests
{
    [Fact]
    public void LayerEditingFacade_UsesUndoableCommands()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(2, 2);
        var layer = session.GetLayers().Single();

        session.RenameLayer(layer.Id, "Gameplay");
        session.SetLayerVisibility(layer.Id, false);
        session.SetLayerLocked(layer.Id, true);
        session.SetLayerOpacity(layer.Id, 128);

        var edited = session.GetLayers().Single();
        Assert.Equal("Gameplay", edited.Name);
        Assert.False(edited.Visible);
        Assert.True(edited.Locked);
        Assert.Equal((byte)128, edited.Opacity);
        Assert.Equal(4, session.Commands.UndoCount);

        session.Undo();
        Assert.Equal(byte.MaxValue, session.GetLayers().Single().Opacity);
        session.Undo();
        Assert.False(session.GetLayers().Single().Locked);
        session.Undo();
        Assert.True(session.GetLayers().Single().Visible);
        session.Undo();
        Assert.Equal("Layer 1", session.GetLayers().Single().Name);
    }

    [Fact]
    public void PaletteEditingFacade_UsesPaletteCommandAndPreservesPresentationSnapshot()
    {
        var document = IndexedDocumentFactory.Create(
            new IntSize(1, 1),
            [new Rgba32(0, 0, 0, 0), new Rgba32(10, 20, 30, 255)],
            0,
            [1]);
        var session = new DocumentSession(new PixelProject(document));
        var before = session.GetPaletteEditors().Single();
        var paletteId = before.Id;

        session.SetPaletteColor(paletteId, 1, new Rgba32(90, 80, 70, 255));

        Assert.Equal(new Rgba32(10, 20, 30, 255), before.Colors[1].Color);
        Assert.Equal(new Rgba32(90, 80, 70, 255), session.GetPaletteEditors().Single().Colors[1].Color);
        Assert.True(session.IsDirty);

        session.Undo();
        Assert.Equal(new Rgba32(10, 20, 30, 255), session.GetPaletteEditors().Single().Colors[1].Color);
    }

    [Fact]
    public void NoOpEditingFacade_DoesNotCreateUndoEntries()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(1, 1);
        var layer = session.GetLayers().Single();

        session.RenameLayer(layer.Id, layer.Name);
        session.SetLayerVisibility(layer.Id, layer.Visible);
        session.SetLayerLocked(layer.Id, layer.Locked);
        session.SetLayerOpacity(layer.Id, layer.Opacity);

        Assert.Equal(0, session.Commands.UndoCount);
        Assert.False(session.IsDirty);
    }
}
