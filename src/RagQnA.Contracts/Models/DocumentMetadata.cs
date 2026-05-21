using RagQnA.Contracts.Enums;

namespace RagQnA.Contracts.Models;

public sealed class DocumentMetadata
{
    public string Id { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DocumentStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? TempFilePath { get; init; }
    public int? ChunkCount { get; init; }
    public DateTimeOffset? IndexedAt { get; init; }
    public string? ErrorMessage { get; init; }
}
