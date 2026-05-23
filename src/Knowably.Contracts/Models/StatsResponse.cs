namespace Knowably.Contracts.Models;

public sealed class StatsResponse
{
    public long TotalQueries { get; init; }
    public long CacheHits { get; init; }
    public long CacheMisses { get; init; }
    public double HitRatePercent { get; init; }
}
