namespace Knowably.Contracts.Models;

public sealed class QStashPublishOptions
{
    public int? Retries { get; set; }
    public TimeSpan? Delay { get; set; }
    public string? DeduplicationId { get; set; }
}
