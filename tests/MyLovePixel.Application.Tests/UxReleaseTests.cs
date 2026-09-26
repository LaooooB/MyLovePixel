using System.Reflection;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class UxReleaseTests
{
    private static object? Read(DocumentSession session, int x, int y, bool layer, CanvasPresentation? composite = null)
    {
        var type = typeof(DocumentSession).Assembly.GetType("MyLovePixel.Application.PixelSampling");
        Assert.NotNull(type);
        var method = type.GetMethod("ReadPixel", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        return method.Invoke(null, [session, x, y, layer, composite]);
    }

    private static T Property<T>(object value, string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;

    private static DocumentSession Painted(Rgba32 color)
    {
        var session = new EditorWorkspace().NewDocument(3, 2);
        var cel = session.CaptureSnapshot().Cels.Single();
        session.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(1, 0, color)]));
        return session;
    }

    [Fact]
    public void Picker_ReadsExactRgba_WithoutTouchingDocumentOrUndo()
    {
        var color = new Rgba32(13, 47, 201, 128);
        var session = Painted(color);
        var snapshot = session.CaptureSnapshot();
        var surface = snapshot.GetSurface(snapshot.Cels.Single().SurfaceId);
        var undo = session.Commands.HistoryDiagnostics;
        var sample = Read(session, 1, 0, true);
        Assert.NotNull(sample);
        Assert.Equal(color, Property<Rgba32>(sample, "Color"));
        Assert.Equal(surface.Revision, session.CaptureSnapshot().GetSurface(surface.Id).Revision);
        Assert.Equal(undo, session.Commands.HistoryDiagnostics);
    }

    [Fact]
    public void Picker_TransparentPixelKeepsZeroAlpha()
    {
        var session = new EditorWorkspace().NewDocument(2, 2);
        var sample = Read(session, 0, 0, true);
        Assert.NotNull(sample);
        Assert.Equal((byte)0, Property<Rgba32>(sample, "Color").A);
        Assert.False(session.IsDirty);
        Assert.False(session.CanUndo);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(3, 0)]
    [InlineData(0, 2)]
    public void Picker_OutsideCanvasReturnsNoSample(int x, int y)
    {
        var session = new EditorWorkspace().NewDocument(3, 2);
        Assert.Null(Read(session, x, y, false));
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void Picker_IgnoresPreviewPixelsAndDisplayDecorations()
    {
        var session = new EditorWorkspace().NewDocument(1, 1);
        var presentation = new CanvasPresentation(session.CurrentFrameId, new IntSize(1, 1),
            new byte[] { 7, 13, 29, 99 },
            [new CanvasPreviewPixel(new IntPoint(0, 0), new Rgba32(255, 0, 0, 255))],
            [new IntRect(0, 0, 1, 1)]);
        var sample = Read(session, 0, 0, false, presentation);
        Assert.NotNull(sample);
        Assert.Equal(new Rgba32(7, 13, 29, 99), Property<Rgba32>(sample, "Color"));
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void Picker_CanReadLockedLayer()
    {
        var color = new Rgba32(1, 2, 3, 255);
        var session = Painted(color);
        session.SetLayerLocked(session.CurrentLayerId, true);
        Assert.Equal(color, Property<Rgba32>(Read(session, 1, 0, true)!, "Color"));
    }

    [Fact]
    public void LockedLayer_RejectsCanvasEraseWithoutMutation()
    {
        var color = new Rgba32(1, 2, 3, 255);
        var session = Painted(color);
        session.SetLayerLocked(session.CurrentLayerId, true);
        Assert.Throws<InvalidOperationException>(() => session.EraseCanvasPixel(1, 0));
        Assert.Equal(color, session.GetCanvasPixel(1, 0));
    }

    [Fact]
    public void LockedLayer_DrawingCannotCommit()
    {
        var session = new EditorWorkspace().NewDocument(3, 2);
        session.SetLayerLocked(session.CurrentLayerId, true);
        var before = session.Commands.HistoryDiagnostics;
        var down = new EditorPointerEvent(1, EditorPointerDevice.Mouse, EditorPointerKind.Pressed,
            new IntPoint(1, 0), 1, EditorPointerButtons.Primary, EditorInputModifiers.None, 1);
        Assert.Throws<InvalidOperationException>(() => session.DispatchPointer(down));
        Assert.Equal(before, session.Commands.HistoryDiagnostics);
        Assert.Equal(Rgba32.Transparent, session.GetCanvasPixel(1, 0));
    }

    [Fact]
    public void ClearCanvas_PreservesLockedLayers()
    {
        var color = new Rgba32(1, 2, 3, 255);
        var session = Painted(color);
        session.SetLayerLocked(session.CurrentLayerId, true);
        session.ClearCurrentCanvas();
        Assert.Equal(color, session.GetCanvasPixel(1, 0));
    }

    [Fact]
    public void ToolOptions_SurviveSwitchingAwayAndBack()
    {
        var session = new EditorWorkspace().NewDocument(8, 8);
        var size = session.GetToolOptions().First(x => x.DisplayName == "Brush Size");
        session.SetToolOption(size.Id, 3);
        session.SelectTool("core.eraser");
        session.SelectTool("core.pencil");
        Assert.Equal(3, session.GetToolOptions().Single(x => x.Id == size.Id).Value);
    }

    [Fact]
    public void FailedSave_DoesNotMarkDocumentSaved()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(2, 2);
        var cel = session.CaptureSnapshot().Cels.Single();
        session.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, new Rgba32(2, 3, 4, 255))]));
        var directory = Path.Combine(Path.GetTempPath(), "mlpx-ux-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Assert.ThrowsAny<Exception>(() => workspace.Save(session, directory));
            Assert.True(session.IsDirty);
            Assert.Null(session.FilePath);
            Assert.Equal(new Rgba32(2, 3, 4, 255), session.GetCanvasPixel(0, 0));
        }
        finally { Directory.Delete(directory, true); }
    }
}
