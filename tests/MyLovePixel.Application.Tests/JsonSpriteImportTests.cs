using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Export;

namespace MyLovePixel.Application.Tests;

public sealed class JsonSpriteImportTests
{
    [Fact]
    public void Imports_export_metadata_and_downscales_export_scale()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mylovepixel-json-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var original = new byte[]
            {
                255, 0, 0, 255,   0, 255, 0, 255,
                0, 0, 255, 255,   0, 0, 0, 0,
            };
            var scaled = new ExportImage(new IntSize(2, 2), original).ScaleNearest(2);
            File.WriteAllBytes(Path.Combine(root, "sprite.png"), PngCodec.Encode(scaled));
            File.WriteAllText(Path.Combine(root, "sprite.json"),
                """
                {
                  "version": 1,
                  "scale": 2,
                  "images": ["sprite.png"],
                  "frames": [
                    {
                      "durationTicks": 123000,
                      "image": "sprite.png",
                      "rect": { "x": 0, "y": 0, "width": 4, "height": 4 },
                      "sourceRect": { "x": 0, "y": 0, "width": 2, "height": 2 },
                      "sourceSize": { "width": 2, "height": 2 },
                      "empty": false
                    }
                  ]
                }
                """);

            var workspace = new EditorWorkspace();
            var session = workspace.ImportSpriteMetadata(Path.Combine(root, "sprite.json"));

            Assert.Equal(new Rgba32(255, 0, 0, 255), session.GetCanvasPixel(0, 0));
            Assert.Equal(new Rgba32(0, 255, 0, 255), session.GetCanvasPixel(1, 0));
            Assert.Equal(new Rgba32(0, 0, 255, 255), session.GetCanvasPixel(0, 1));
            Assert.Equal(Rgba32.Transparent, session.GetCanvasPixel(1, 1));
            Assert.Equal(123000, session.CaptureSnapshot().GetFrame(session.CurrentFrameId).DurationTicks);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Right_click_erase_command_is_undoable()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(2, 2);
        var rgba = new byte[16];
        rgba[0] = 250;
        rgba[1] = 40;
        rgba[2] = 20;
        rgba[3] = 255;
        session.ReplaceCurrentCanvasWithRgba(rgba, "Seed");

        session.EraseCanvasPixel(0, 0);
        Assert.Equal(Rgba32.Transparent, session.GetCanvasPixel(0, 0));

        session.Undo();
        Assert.Equal(new Rgba32(250, 40, 20, 255), session.GetCanvasPixel(0, 0));
    }
}
