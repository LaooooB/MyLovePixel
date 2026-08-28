namespace MyLovePixel.Render;

public readonly record struct RenderCacheRates(
    long RequestCount,
    long MissCount,
    long HitCount,
    double HitRatio);

public static class RenderCacheDiagnosticsExtensions
{
    public static RenderCacheRates GetRates(this RenderCacheDiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var misses = checked(snapshot.FullRecomposeCount + snapshot.PartialRecomposeCount);
        var requests = checked(misses + snapshot.CacheHitCount);
        var ratio = requests == 0 ? 0d : (double)snapshot.CacheHitCount / requests;
        return new RenderCacheRates(requests, misses, snapshot.CacheHitCount, ratio);
    }
}
