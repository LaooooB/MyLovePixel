using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class ColorWorkflowSafetyTests
{
    [Theory]
    [InlineData("\"2\"")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("true")]
    public void InvalidSchemaTypeDoesNotCrashOrOverwriteOriginal(string version)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var original = "{\"schemaVersion\":" + version + ",\"colors\":[\"#123456\"]}";
        try
        {
            File.WriteAllText(path, original);
            var store = new UserPaletteStore(path);
            Assert.NotNull(store.LoadError);
            Assert.Throws<InvalidOperationException>(() => store.Add(new Rgba32(1, 2, 3)));
            Assert.Equal(original, File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SamplingArtworkCanSaveTwoTemporaryColorsWithoutAnUndoOrPixelChange()
    {
        var type = typeof(UserPaletteStore).Assembly.GetType("MyLovePixel.Application.PixelSampling");
        Assert.NotNull(type);
        var read = type.GetMethod("ReadPixel"); Assert.NotNull(read);
        var session = new EditorWorkspace().NewDocument(3, 1);
        var cel = session.CaptureSnapshot().Cels.Single();
        var one = new Rgba32(101, 67, 33, 128); var two = new Rgba32(12, 34, 56, 255);
        session.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(0, 0, one), new PixelWrite(1, 0, two)]));
        session.SetLayerLocked(session.CurrentLayerId, true);
        var before = session.RenderCanvas().Rgba.ToArray(); var undo = session.Commands.UndoCount;
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "colors.json");
        try
        {
            var store = new UserPaletteStore(path);
            foreach (var (x, expected) in new[] { (0, one), (1, two) })
            {
                dynamic sample = read.Invoke(null, new object?[] { session, x, 0, true, null })!;
                Assert.Equal(expected, (Rgba32)sample.Color);
                type.GetMethod("ApplySample")!.Invoke(null, new object[] { session, sample, false });
                store.AddTemporary(session.GetToolColors().Primary);
            }
            Assert.Equal(new[] { one, two }, new UserPaletteStore(path).TemporaryColors);
            Assert.Equal(undo, session.Commands.UndoCount);
            Assert.Equal(before, session.RenderCanvas().Rgba.ToArray());
            Assert.Null(read.Invoke(null, new object?[] { session, -1, 0, false, null }));
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void FullTemporaryRackNeverEvictsExistingSamples()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "colors.json");
        try
        {
            var store = new UserPaletteStore(path);
            for (var i = 0; i < UserPaletteStore.MaxTemporaryColors; i++) store.AddTemporary(new Rgba32((byte)i, 1, 2));
            var original = File.ReadAllBytes(path);
            Assert.Throws<InvalidOperationException>(() => store.AddTemporary(new Rgba32(255, 4, 5)));
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal(UserPaletteStore.MaxTemporaryColors, new UserPaletteStore(path).TemporaryColors.Count);
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
}
