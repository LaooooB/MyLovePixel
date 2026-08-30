using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class SpriteSheetImportTests
{
    [Fact]
    public void BuildSlices_TwoByFive_ReadsLeftToRightThenTopToBottom()
    {
        var slices = SpriteSheetGrid.BuildSlices(20, 50, 2, 5);

        Assert.Equal(10, slices.Count);
        Assert.Equal(new IntRect(0, 0, 10, 10), slices[0].Bounds);
        Assert.Equal(new IntRect(10, 0, 10, 10), slices[1].Bounds);
        Assert.Equal(new IntRect(0, 10, 10, 10), slices[2].Bounds);
        Assert.Equal(new IntRect(10, 40, 10, 10), slices[9].Bounds);
    }

    [Theory]
    [InlineData(128, 320, 64, 64, 2, 5)]
    [InlineData(192, 192, 64, 64, 3, 3)]
    public void SuggestGrid_PrefersCurrentCanvasFrameSize(
        int imageWidth,
        int imageHeight,
        int frameWidth,
        int frameHeight,
        int expectedColumns,
        int expectedRows)
    {
        var suggestion = SpriteSheetGrid.SuggestGrid(
            imageWidth,
            imageHeight,
            new IntSize(frameWidth, frameHeight));

        Assert.Equal(expectedColumns, suggestion.Columns);
        Assert.Equal(expectedRows, suggestion.Rows);
        Assert.Equal(frameWidth, suggestion.FrameWidth);
        Assert.Equal(frameHeight, suggestion.FrameHeight);
    }

    [Fact]
    public void ImportSpriteSheetFrames_CreatesOrderedTimelineWithSingleUndo()
    {
        var workspace = new EditorWorkspace();
        var session = workspace.NewDocument(1, 1);
        var frames = new[]
        {
            Pixel(10, 20, 30),
            Pixel(40, 50, 60),
            Pixel(70, 80, 90),
        };

        var imported = session.ImportSpriteSheetFrames(
            frames,
            new IntSize(1, 1),
            durationMilliseconds: 80);

        Assert.Equal(3, imported.Count);
        Assert.Equal(1, session.Commands.UndoCount);

        var snapshot = session.CaptureSnapshot();
        Assert.Equal(imported, snapshot.FrameOrder);
        Assert.All(snapshot.FrameOrder, frameId => Assert.Equal(80_000, snapshot.GetFrame(frameId).DurationTicks));
        Assert.Equal(new Rgba32(10, 20, 30, 255), GetFramePixel(snapshot, session.CurrentLayerId, snapshot.FrameOrder[0]));
        Assert.Equal(new Rgba32(40, 50, 60, 255), GetFramePixel(snapshot, session.CurrentLayerId, snapshot.FrameOrder[1]));
        Assert.Equal(new Rgba32(70, 80, 90, 255), GetFramePixel(snapshot, session.CurrentLayerId, snapshot.FrameOrder[2]));

        session.Undo();

        var undone = session.CaptureSnapshot();
        Assert.Single(undone.FrameOrder);
        Assert.Equal(Rgba32.Transparent, GetFramePixel(undone, session.CurrentLayerId, undone.FrameOrder[0]));
    }

    [Fact]
    public void BuildSlices_NonDivisibleFourByFour_StillCreatesSixteenFramesAndCoversImage()
    {
        var slices = SpriteSheetGrid.BuildSlices(1254, 1254, 4, 4);

        Assert.Equal(16, slices.Count);
        Assert.Equal(new IntRect(0, 0, 313, 313), slices[0].Bounds);
        Assert.Equal(new IntRect(940, 940, 314, 314), slices[15].Bounds);
        Assert.Equal(1254 * 1254, slices.Sum(slice => slice.Bounds.Width * slice.Bounds.Height));
    }

    [Fact]
    public void BuildSlices_RejectsMoreGridCellsThanImagePixels()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpriteSheetGrid.BuildSlices(3, 3, 4, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpriteSheetGrid.BuildSlices(3, 3, 1, 4));
    }

    private static byte[] Pixel(byte r, byte g, byte b) => [r, g, b, 255];

    private static Rgba32 GetFramePixel(
        MyLovePixel.Core.Document.DocumentSnapshot snapshot,
        LayerId layerId,
        FrameId frameId)
    {
        var cel = snapshot.Cels.Single(value => value.LayerId == layerId && value.FrameId == frameId);
        return snapshot.GetSurface(cel.SurfaceId).GetPixel(0, 0);
    }
}
