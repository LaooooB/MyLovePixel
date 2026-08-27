using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Raster.Coordinates;

public interface ICoordinatePolicy
{
    bool TryResolve(IntSize surfaceSize, IntPoint input, out IntPoint resolved);
}

public sealed class ClipCoordinatePolicy : ICoordinatePolicy
{
    public static ClipCoordinatePolicy Instance { get; } = new();

    private ClipCoordinatePolicy()
    {
    }

    public bool TryResolve(IntSize surfaceSize, IntPoint input, out IntPoint resolved)
    {
        if ((uint)input.X < (uint)surfaceSize.Width && (uint)input.Y < (uint)surfaceSize.Height)
        {
            resolved = input;
            return true;
        }

        resolved = default;
        return false;
    }
}

public sealed class TiledCoordinatePolicy(bool wrapX = true, bool wrapY = true) : ICoordinatePolicy
{
    public bool WrapX { get; } = wrapX;
    public bool WrapY { get; } = wrapY;

    public bool TryResolve(IntSize surfaceSize, IntPoint input, out IntPoint resolved)
    {
        if (!WrapX && (uint)input.X >= (uint)surfaceSize.Width ||
            !WrapY && (uint)input.Y >= (uint)surfaceSize.Height)
        {
            resolved = default;
            return false;
        }

        resolved = new IntPoint(
            WrapX ? Wrap(input.X, surfaceSize.Width) : input.X,
            WrapY ? Wrap(input.Y, surfaceSize.Height) : input.Y);
        return true;
    }

    private static int Wrap(int value, int length)
    {
        var remainder = value % length;
        return remainder < 0 ? remainder + length : remainder;
    }
}
