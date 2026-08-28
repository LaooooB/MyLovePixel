using MyLovePixel.Commands.Color;
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
    public void QuantizeSurface_RoundTripsFormatAndPaletteThroughSingleUndo()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(2, 2);

        var paletteId = session.QuantizeCurrentSurface(4, reserveTransparentIndex: true);

        Assert.Equal(PixelFormat.Indexed8, session.GetCurrentSurfaceFormat());
        Assert.Contains(session.GetPaletteEditors(), value => value.Id == paletteId);
        Assert.Equal(1, session.Commands.UndoCount);

        session.Undo();

        Assert.Equal(PixelFormat.Rgba32, session.GetCurrentSurfaceFormat());
        Assert.DoesNotContain(session.GetPaletteEditors(), value => value.Id == paletteId);
    }

    [Fact]
    public void PaletteResize_IsUndoableAndRejectsDanglingIndexedReferences()
    {
        var original = new[] { new Rgba32(0, 0, 0, 0), new Rgba32(10, 20, 30, 255) };
        var document = IndexedDocumentFactory.Create(new IntSize(1, 1), original, 0, [1]);
        var session = new DocumentSession(new PixelProject(document));
        var paletteId = session.GetPaletteEditors().Single().Id;

        session.Execute(new ReplacePaletteColorsCommand(
            paletteId,
            [original[0], original[1], new Rgba32(200, 210, 220, 255)]));
        Assert.Equal(3, session.GetPaletteEditors().Single().Colors.Count);

        session.Undo();
        Assert.Equal(2, session.GetPaletteEditors().Single().Colors.Count);

        Assert.Throws<InvalidOperationException>(() => session.Execute(new ReplacePaletteColorsCommand(paletteId, [original[0]])));
        Assert.Equal(2, session.GetPaletteEditors().Single().Colors.Count);
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
