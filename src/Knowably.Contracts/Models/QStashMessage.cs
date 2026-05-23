namespace Knowably.Contracts.Models;

public sealed class QStashMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int DeliveredCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
