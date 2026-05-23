using Knowably.Contracts.Models;

namespace Knowably.Contracts.Interfaces;

public interface ITextChunker
{
    IEnumerable<TextChunk> Chunk(string text);
}
