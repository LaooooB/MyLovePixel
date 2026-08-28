using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;

namespace MyLovePixel.Tilemap;

public static class TileCellTransform
{
    public static IntPoint MapDestinationToSource(
        IntPoint destination,
        IntSize tileSize,
        TileCellFlags flags)
    {
        if ((uint)destination.X >= (uint)tileSize.Width)
            throw new ArgumentOutOfRangeException(nameof(destination));
        if ((uint)destination.Y >= (uint)tileSize.Height)
            throw new ArgumentOutOfRangeException(nameof(destination));

        var x = destination.X;
        var y = destination.Y;

        // Visual transform order is Rotate90 clockwise, then FlipX/FlipY.
        // Sampling applies the inverse order: undo flips, then undo rotation.
        if ((flags & TileCellFlags.FlipX) != 0) x = tileSize.Width - 1 - x;
        if ((flags & TileCellFlags.FlipY) != 0) y = tileSize.Height - 1 - y;

        if ((flags & TileCellFlags.Rotate90) == 0)
            return new IntPoint(x, y);

        if (tileSize.Width != tileSize.Height)
            throw new InvalidOperationException("Rotate90 requires a square tile size.");
        return new IntPoint(y, tileSize.Width - 1 - x);
    }
}
