namespace Knowably.Contracts.Models;

public sealed class TextChunk
{
    public int Index { get; init; }
    public string Text { get; init; } = string.Empty;
}
