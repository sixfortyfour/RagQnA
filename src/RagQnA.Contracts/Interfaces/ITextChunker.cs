using RagQnA.Contracts.Models;

namespace RagQnA.Contracts.Interfaces;

public interface ITextChunker
{
    IEnumerable<TextChunk> Chunk(string text);
}
