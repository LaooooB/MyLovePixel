using System.Reflection;
using MyLovePixel.Application;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class UxReleaseRegressionTests
{
    // Reflection keeps the baseline buildable so this fails for the missing behavior,
    // rather than an unresolved type or a compilation error.
    private static MethodInfo Sampler()
    {
        var method = typeof(DocumentSession).GetMethod("SampleCanvasPixel", [typeof(int), typeof(int), typeof(bool)]);
        Assert.True(method is not null, "A read-only pixel sampler must be available without creating an editable Cel.");
        return method!;
    }

    [Fact]
    public void Sampling_a_blank_pixel_preserves_document_and_history()
    {
        var session = new EditorWorkspace().NewDocument(8, 8);
        var before = session.CaptureSnapshot();
        var result = Sampler().Invoke(session, [2, 3, false]);
        Assert.NotNull(result);
        var after = session.CaptureSnapshot();
        Assert.False(session.IsDirty);
        Assert.False(session.CanUndo);
        Assert.Equal(before.Surfaces.Count, after.Surfaces.Count);
        foreach (var pair in before.Surfaces)
            Assert.Equal(pair.Value.Revision, after.Surfaces[pair.Key].Revision);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(8, 0)]
    [InlineData(0, 8)]
    public void Sampling_outside_canvas_returns_no_color(int x, int y)
    {
        var session = new EditorWorkspace().NewDocument(8, 8);
        Assert.Null(Sampler().Invoke(session, [x, y, false]));
        Assert.False(session.IsDirty);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void Tool_shortcut_labels_come_from_a_shared_catalog()
    {
        var type = typeof(DocumentSession).Assembly.GetType("MyLovePixel.Application.EditorToolShortcuts");
        Assert.True(type is not null, "Tool names and key routing must share one shortcut catalog.");
        var label = type!.GetMethod("ForTool");
        Assert.NotNull(label);
        Assert.Equal("B", label!.Invoke(null, ["core.pencil"]));
        Assert.Equal("E", label.Invoke(null, ["core.eraser"]));
        Assert.Equal("I", label.Invoke(null, ["core.eyedropper"]));
        Assert.NotEqual("1", label.Invoke(null, ["core.pencil"]));
    }
}
