using Knowably.Contracts.Enums;

namespace Knowably.Contracts.Models;

public sealed class DocumentListItem
{
    public string Id { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DocumentStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public int? ChunkCount { get; init; }
}
