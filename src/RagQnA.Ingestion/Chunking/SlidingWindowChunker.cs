using Microsoft.Extensions.Options;
using RagQnA.Contracts.Interfaces;
using RagQnA.Contracts.Models;
using RagQnA.Contracts.Options;

namespace RagQnA.Ingestion.Chunking;

public sealed class SlidingWindowChunker : ITextChunker
{
    private readonly int _chunkSize;
    private readonly int _overlap;
    private readonly int _step;

    public SlidingWindowChunker(IOptions<IngestionOptions> options)
    {
        _chunkSize = options.Value.ChunkSize;
        _overlap = (int)(_chunkSize * options.Value.ChunkOverlapPercent / 100.0);
        _step = _chunkSize - _overlap;
        if (_step <= 0) _step = 1;
    }

    public IEnumerable<TextChunk> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
            yield break;

        var index = 0;
        var chunkIndex = 0;

        while (index < words.Length)
        {
            var end = Math.Min(index + _chunkSize, words.Length);
            var chunkWords = words[index..end];
            var chunkText = string.Join(" ", chunkWords);

            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                yield return new TextChunk
                {
                    Index = chunkIndex++,
                    Text = chunkText
                };
            }

            if (end == words.Length)
                break;

            index += _step;
        }
    }
}
