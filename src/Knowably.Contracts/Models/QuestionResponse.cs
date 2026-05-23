namespace Knowably.Contracts.Models;

public sealed record QuestionResponse
{
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public bool Cached { get; init; }
    public long DurationMs { get; init; }
    public List<SourceChunk> SourceChunks { get; init; } = [];
}
