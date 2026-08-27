using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Selection;

public enum SelectionCombineMode
{
    Add,
    Subtract,
    Intersect,
}

public static class SelectionMaskOperations
{
    public static SelectionMask Combine(SelectionMask left, SelectionMask right, SelectionCombineMode mode)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Size != right.Size) throw new ArgumentException("Selection masks must have identical sizes.", nameof(right));

        var format = left.Format == SelectionMaskFormat.Alpha8 || right.Format == SelectionMaskFormat.Alpha8
            ? SelectionMaskFormat.Alpha8
            : SelectionMaskFormat.Bit1;
        var leftCoverage = left.CopyCoverage();
        var rightCoverage = right.CopyCoverage();
        var result = new byte[leftCoverage.Length];

        for (var index = 0; index < result.Length; index++)
        {
            var a = leftCoverage[index];
            var b = rightCoverage[index];
            result[index] = mode switch
            {
                SelectionCombineMode.Add => Union(a, b),
                SelectionCombineMode.Subtract => Scale(a, byte.MaxValue - b),
                SelectionCombineMode.Intersect => Scale(a, b),
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            };
        }

        return SelectionMask.FromCoverage(left.Size, format, result);
    }

    public static SelectionMask Invert(SelectionMask mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        var coverage = mask.CopyCoverage();
        for (var index = 0; index < coverage.Length; index++) coverage[index] = (byte)(byte.MaxValue - coverage[index]);
        return SelectionMask.FromCoverage(mask.Size, mask.Format, coverage);
    }

    private static byte Union(byte a, byte b)
    {
        var inverse = Scale(byte.MaxValue - a, byte.MaxValue - b);
        return (byte)(byte.MaxValue - inverse);
    }

    private static byte Scale(int value, int factor) =>
        (byte)(((value * factor) + 127) / 255);
}
