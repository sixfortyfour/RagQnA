namespace Knowably.Contracts.Models;

public sealed class StatsBucket
{
    public string Bucket { get; init; } = string.Empty;
    public long Hits { get; init; }
    public long Misses { get; init; }
}
