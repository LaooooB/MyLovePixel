namespace MyLovePixel.Core.Primitives;

public readonly record struct IntPoint(int X, int Y);

public readonly record struct IntSize
{
    public IntSize(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }
}

public readonly record struct IntRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public static IntRect FromPoint(int x, int y) => new(x, y, 1, 1);

    public static IntRect Union(IntRect a, IntRect b)
    {
        if (a.IsEmpty) return b;
        if (b.IsEmpty) return a;
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.Right, b.Right);
        var bottom = Math.Max(a.Bottom, b.Bottom);
        return new IntRect(x, y, right - x, bottom - y);
    }
}
