namespace Knowably.Contracts.Options;

public sealed class UpstashRedisOptions
{
    public string RestUrl { get; set; } = string.Empty;
    public string RestToken { get; set; } = string.Empty;
}
