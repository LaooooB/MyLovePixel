using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Selection;

public enum QuarterTurn
{
    Clockwise,
    CounterClockwise,
}

public static class SelectionTransforms
{
    public static SelectionMask Translate(SelectionMask mask, IntPoint delta)
    {
        ArgumentNullException.ThrowIfNull(mask);
        if (mask.IsEmpty || delta == default) return SelectionMask.FromCoverage(mask.Size, mask.Format, mask.CopyCoverage());

        var result = new byte[checked(mask.Size.Width * mask.Size.Height)];
        foreach (var point in mask.EnumerateSelected())
        {
            var targetX = (long)point.X + delta.X;
            var targetY = (long)point.Y + delta.Y;
            if ((ulong)targetX >= (ulong)mask.Size.Width || (ulong)targetY >= (ulong)mask.Size.Height) continue;
            result[((int)targetY * mask.Size.Width) + (int)targetX] = mask.GetCoverage(point.X, point.Y);
        }
        return SelectionMask.FromCoverage(mask.Size, mask.Format, result);
    }

    public static SelectionMask FlipHorizontal(SelectionMask mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        if (mask.IsEmpty) return SelectionMask.Empty(mask.Size, mask.Format);
        var bounds = mask.Bounds;
        return Remap(mask, point => new IntPoint(bounds.X + bounds.Width - 1 - (point.X - bounds.X), point.Y));
    }

    public static SelectionMask FlipVertical(SelectionMask mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        if (mask.IsEmpty) return SelectionMask.Empty(mask.Size, mask.Format);
        var bounds = mask.Bounds;
        return Remap(mask, point => new IntPoint(point.X, bounds.Y + bounds.Height - 1 - (point.Y - bounds.Y)));
    }

    public static SelectionMask Rotate90(SelectionMask mask, QuarterTurn turn)
    {
        ArgumentNullException.ThrowIfNull(mask);
        if (mask.IsEmpty) return SelectionMask.Empty(mask.Size, mask.Format);
        var bounds = mask.Bounds;
        var result = new byte[checked(mask.Size.Width * mask.Size.Height)];

        foreach (var point in mask.EnumerateSelected())
        {
            var localX = point.X - bounds.X;
            var localY = point.Y - bounds.Y;
            var target = turn switch
            {
                QuarterTurn.Clockwise => new IntPoint(bounds.X + bounds.Height - 1 - localY, bounds.Y + localX),
                QuarterTurn.CounterClockwise => new IntPoint(bounds.X + localY, bounds.Y + bounds.Width - 1 - localX),
                _ => throw new ArgumentOutOfRangeException(nameof(turn)),
            };

            if ((uint)target.X >= (uint)mask.Size.Width || (uint)target.Y >= (uint)mask.Size.Height) continue;
            result[(target.Y * mask.Size.Width) + target.X] = mask.GetCoverage(point.X, point.Y);
        }

        return SelectionMask.FromCoverage(mask.Size, mask.Format, result);
    }

    public static SelectionMask ScaleNearest(SelectionMask mask, IntRect targetBounds)
    {
        ArgumentNullException.ThrowIfNull(mask);
        if (targetBounds.Width <= 0 || targetBounds.Height <= 0 || mask.IsEmpty)
            return SelectionMask.Empty(mask.Size, mask.Format);

        var source = mask.Bounds;
        var result = new byte[checked(mask.Size.Width * mask.Size.Height)];
        var startX = (int)Math.Max(0L, targetBounds.X);
        var startY = (int)Math.Max(0L, targetBounds.Y);
        var endX = (int)Math.Min(mask.Size.Width, (long)targetBounds.X + targetBounds.Width);
        var endY = (int)Math.Min(mask.Size.Height, (long)targetBounds.Y + targetBounds.Height);

        for (var y = startY; y < endY; y++)
        for (var x = startX; x < endX; x++)
        {
            var localX = (long)x - targetBounds.X;
            var localY = (long)y - targetBounds.Y;
            var sourceX = source.X + (int)((localX * source.Width) / targetBounds.Width);
            var sourceY = source.Y + (int)((localY * source.Height) / targetBounds.Height);
            result[(y * mask.Size.Width) + x] = mask.GetCoverage(sourceX, sourceY);
        }

        return SelectionMask.FromCoverage(mask.Size, mask.Format, result);
    }

    private static SelectionMask Remap(SelectionMask mask, Func<IntPoint, IntPoint> map)
    {
        var result = new byte[checked(mask.Size.Width * mask.Size.Height)];
        foreach (var point in mask.EnumerateSelected())
        {
            var target = map(point);
            if ((uint)target.X >= (uint)mask.Size.Width || (uint)target.Y >= (uint)mask.Size.Height) continue;
            result[(target.Y * mask.Size.Width) + target.X] = mask.GetCoverage(point.X, point.Y);
        }
        return SelectionMask.FromCoverage(mask.Size, mask.Format, result);
    }
}
