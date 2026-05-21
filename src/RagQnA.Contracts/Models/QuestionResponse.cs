namespace RagQnA.Contracts.Models;

public sealed record QuestionResponse
{
    public string Answer { get; init; } = string.Empty;
    public bool Cached { get; init; }
    public long DurationMs { get; init; }
    public List<SourceChunk> SourceChunks { get; init; } = [];
}
