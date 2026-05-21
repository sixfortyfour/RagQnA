namespace RagQnA.Contracts.Models;

public sealed class SourceChunk
{
    public string Text { get; init; } = string.Empty;
    public string DocumentId { get; init; } = string.Empty;
    public int ChunkIndex { get; init; }
    public float Score { get; init; }
}
