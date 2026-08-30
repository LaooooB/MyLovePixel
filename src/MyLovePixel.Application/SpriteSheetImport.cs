using MyLovePixel.Commands.Pixel;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Application;

public enum SpriteSheetTraversalOrder
{
    LeftToRightTopToBottom = 0,
    LeftToRightBottomToTop = 1,
    TopToBottomLeftToRight = 2,
}

public sealed record SpriteSheetGridSuggestion(
    int Columns,
    int Rows,
    int FrameWidth,
    int FrameHeight,
    string Reason);

public sealed record SpriteSheetFrameSlice(int Index, IntRect Bounds);

public static class SpriteSheetGrid
{
    public const int MaxFrameCount = 1024;

    public static SpriteSheetGridSuggestion SuggestGrid(
        int imageWidth,
        int imageHeight,
        IntSize? preferredFrameSize = null)
    {
        ValidateImageSize(imageWidth, imageHeight);

        if (preferredFrameSize is { } preferred &&
            imageWidth % preferred.Width == 0 &&
            imageHeight % preferred.Height == 0)
        {
            var columns = imageWidth / preferred.Width;
            var rows = imageHeight / preferred.Height;
            var count = checked(columns * rows);
            if (count is > 1 and <= MaxFrameCount)
            {
                return new SpriteSheetGridSuggestion(
                    columns,
                    rows,
                    preferred.Width,
                    preferred.Height,
                    $"Matched the current canvas size ({preferred.Width} × {preferred.Height}).");
            }
        }

        var targetAspect = preferredFrameSize is { } preferredAspect
            ? preferredAspect.Width / (double)preferredAspect.Height
            : 1d;
        SpriteSheetGridSuggestion? best = null;
        var bestScore = double.MaxValue;
        var maxColumns = Math.Min(32, imageWidth);
        var maxRows = Math.Min(32, imageHeight);

        for (var columns = 1; columns <= maxColumns; columns++)
        {
            if (imageWidth % columns != 0) continue;
            var frameWidth = imageWidth / columns;
            for (var rows = 1; rows <= maxRows; rows++)
            {
                if (imageHeight % rows != 0) continue;
                var count = checked(columns * rows);
                if (count is <= 1 or > MaxFrameCount) continue;

                var frameHeight = imageHeight / rows;
                var frameAspect = frameWidth / (double)frameHeight;
                var aspectPenalty = Math.Abs(Math.Log(frameAspect / targetAspect));
                var commonSizePenalty = (IsPowerOfTwo(frameWidth) ? 0d : 0.12d) +
                                        (IsPowerOfTwo(frameHeight) ? 0d : 0.12d);
                var stripPenalty = columns == 1 || rows == 1 ? 0.05d : 0d;
                var frameCountPenalty = count * 0.001d;
                var score = aspectPenalty + commonSizePenalty + stripPenalty + frameCountPenalty;
                if (score >= bestScore) continue;

                bestScore = score;
                best = new SpriteSheetGridSuggestion(
                    columns,
                    rows,
                    frameWidth,
                    frameHeight,
                    "Best regular-grid guess. Confirm Columns / Rows if the sheet has an unusual layout.");
            }
        }

        return best ?? throw new InvalidOperationException(
            "Could not infer a multi-frame grid from the image dimensions. Turn off Auto detect and enter Columns / Rows manually.");
    }

    public static IReadOnlyList<SpriteSheetFrameSlice> BuildSlices(
        int imageWidth,
        int imageHeight,
        int columns,
        int rows,
        SpriteSheetTraversalOrder order = SpriteSheetTraversalOrder.LeftToRightTopToBottom)
    {
        ValidateImageSize(imageWidth, imageHeight);
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
        var frameCount = checked(columns * rows);
        if (frameCount > MaxFrameCount)
            throw new ArgumentOutOfRangeException(nameof(columns), $"A sprite sheet can contain at most {MaxFrameCount} imported frames.");
        if (imageWidth % columns != 0 || imageHeight % rows != 0)
            throw new ArgumentException(
                $"Image size {imageWidth} × {imageHeight} is not evenly divisible by a {columns} × {rows} grid.");

        var frameWidth = imageWidth / columns;
        var frameHeight = imageHeight / rows;
        var result = new List<SpriteSheetFrameSlice>(frameCount);

        void Add(int column, int row) => result.Add(new SpriteSheetFrameSlice(
            result.Count,
            new IntRect(column * frameWidth, row * frameHeight, frameWidth, frameHeight)));

        switch (order)
        {
            case SpriteSheetTraversalOrder.LeftToRightTopToBottom:
                for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                    Add(column, row);
                break;

            case SpriteSheetTraversalOrder.LeftToRightBottomToTop:
                for (var row = rows - 1; row >= 0; row--)
                for (var column = 0; column < columns; column++)
                    Add(column, row);
                break;

            case SpriteSheetTraversalOrder.TopToBottomLeftToRight:
                for (var column = 0; column < columns; column++)
                for (var row = 0; row < rows; row++)
                    Add(column, row);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(order));
        }

        return result.AsReadOnly();
    }

    private static void ValidateImageSize(int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0) throw new ArgumentOutOfRangeException(nameof(imageWidth));
        if (imageHeight <= 0) throw new ArgumentOutOfRangeException(nameof(imageHeight));
    }

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
}

public static partial class AdvancedEditingExtensions
{
    public static IReadOnlyList<FrameId> ImportSpriteSheetFrames(
        this DocumentSession session,
        IReadOnlyList<byte[]> rgbaFrames,
        IntSize frameSize,
        int durationMilliseconds = 100,
        bool append = false,
        string name = "Import Sprite Sheet")
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(rgbaFrames);
        if (rgbaFrames.Count == 0) throw new ArgumentException("Sprite sheet contains no frames.", nameof(rgbaFrames));
        if (rgbaFrames.Count > SpriteSheetGrid.MaxFrameCount)
            throw new ArgumentOutOfRangeException(nameof(rgbaFrames), $"A sprite sheet can contain at most {SpriteSheetGrid.MaxFrameCount} imported frames.");
        if (durationMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(durationMilliseconds));

        var before = session.CaptureSnapshot();
        if (before.Canvas.Size != frameSize)
            throw new InvalidOperationException(
                $"Frame size {frameSize.Width} × {frameSize.Height} does not match the current canvas {before.Canvas.Size.Width} × {before.Canvas.Size.Height}.");
        if (!append && before.FrameOrder.Count != 1)
            throw new InvalidOperationException("Replacing a timeline with a sprite sheet requires a one-frame document. Use Append timeline instead.");

        var expectedBytes = checked(frameSize.Width * frameSize.Height * 4);
        for (var index = 0; index < rgbaFrames.Count; index++)
        {
            var frame = rgbaFrames[index] ?? throw new ArgumentException($"Frame {index + 1} is null.", nameof(rgbaFrames));
            if (frame.Length != expectedBytes)
                throw new ArgumentException($"Frame {index + 1} byte length does not match {frameSize.Width} × {frameSize.Height} RGBA.", nameof(rgbaFrames));
        }

        var durationTicks = checked((long)durationMilliseconds * 1000L);
        var originalFrameId = session.CurrentFrameId;
        var imported = new List<FrameId>(rgbaFrames.Count);
        using var transaction = session.Commands.BeginTransaction(name);
        try
        {
            if (append)
            {
                var sourceFrameId = before.FrameOrder[^1];
                for (var index = 0; index < rgbaFrames.Count; index++)
                {
                    var copy = new CopyFrameCommand(
                        sourceFrameId,
                        FrameCopyMode.Independent,
                        session.CaptureSnapshot().FrameOrder.Count);
                    session.Execute(copy);
                    session.SelectFrame(copy.NewFrameId);
                    WriteCurrentFrame(session, rgbaFrames[index], durationTicks, name);
                    imported.Add(copy.NewFrameId);
                    sourceFrameId = copy.NewFrameId;
                }
            }
            else
            {
                var firstFrameId = before.FrameOrder[0];
                session.SelectFrame(firstFrameId);
                WriteCurrentFrame(session, rgbaFrames[0], durationTicks, name);
                imported.Add(firstFrameId);

                var sourceFrameId = firstFrameId;
                for (var index = 1; index < rgbaFrames.Count; index++)
                {
                    var copy = new CopyFrameCommand(
                        sourceFrameId,
                        FrameCopyMode.Independent,
                        session.CaptureSnapshot().FrameOrder.Count);
                    session.Execute(copy);
                    session.SelectFrame(copy.NewFrameId);
                    WriteCurrentFrame(session, rgbaFrames[index], durationTicks, name);
                    imported.Add(copy.NewFrameId);
                    sourceFrameId = copy.NewFrameId;
                }
            }

            transaction.Commit();
            session.SelectFrame(append ? originalFrameId : imported[0]);
            return imported.AsReadOnly();
        }
        catch
        {
            transaction.Rollback();
            session.SelectFrame(originalFrameId);
            throw;
        }
    }

    private static void WriteCurrentFrame(
        DocumentSession session,
        ReadOnlyMemory<byte> rgba,
        long durationTicks,
        string name)
    {
        session.EnsureEditableCel();
        var snapshot = session.CaptureSnapshot();
        var cel = snapshot.Cels.FirstOrDefault(value =>
            value.LayerId == session.CurrentLayerId && value.FrameId == session.CurrentFrameId)
            ?? throw new InvalidOperationException("Current Layer/Frame has no editable Cel.");
        var surface = snapshot.GetSurface(cel.SurfaceId);
        if (surface.Size != snapshot.Canvas.Size)
            throw new InvalidOperationException("Current Cel surface must match the canvas size for sprite-sheet import.");

        session.Execute(new ReplacePixelSurfaceCommand(
            cel.SurfaceId,
            PixelFormat.Rgba32,
            null,
            rgba,
            name));
        session.Execute(new SetFrameDurationCommand(session.CurrentFrameId, durationTicks));
    }
}
