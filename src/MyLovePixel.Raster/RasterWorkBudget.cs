namespace MyLovePixel.Raster;

public sealed class RasterWorkBudget
{
    public RasterWorkBudget(int maxVisitedPixels = 4_000_000, int maxWrites = 4_000_000)
    {
        if (maxVisitedPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maxVisitedPixels));
        if (maxWrites <= 0) throw new ArgumentOutOfRangeException(nameof(maxWrites));
        MaxVisitedPixels = maxVisitedPixels;
        MaxWrites = maxWrites;
    }

    public int MaxVisitedPixels { get; }
    public int MaxWrites { get; }

    public static RasterWorkBudget Default { get; } = new();
}

public enum RasterBudgetKind
{
    VisitedPixels,
    Writes,
}

public sealed class RasterWorkBudgetExceededException : InvalidOperationException
{
    public RasterWorkBudgetExceededException(RasterBudgetKind kind, int limit, int observed)
        : base($"Raster work budget '{kind}' exceeded. Limit={limit}, Observed={observed}.")
    {
        Kind = kind;
        Limit = limit;
        Observed = observed;
    }

    public RasterBudgetKind Kind { get; }
    public int Limit { get; }
    public int Observed { get; }
}

internal static class RasterBudgetGuard
{
    public static void CheckVisited(RasterWorkBudget budget, int observed)
    {
        if (observed > budget.MaxVisitedPixels)
            throw new RasterWorkBudgetExceededException(RasterBudgetKind.VisitedPixels, budget.MaxVisitedPixels, observed);
    }

    public static void CheckWrites(RasterWorkBudget budget, int observed)
    {
        if (observed > budget.MaxWrites)
            throw new RasterWorkBudgetExceededException(RasterBudgetKind.Writes, budget.MaxWrites, observed);
    }
}
