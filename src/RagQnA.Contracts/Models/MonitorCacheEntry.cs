namespace RagQnA.Contracts.Models;

public sealed class MonitorCacheEntry
{
    public string Hash { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public long TtlSeconds { get; init; }
}
