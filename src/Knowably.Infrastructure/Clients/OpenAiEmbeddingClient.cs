using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using Knowably.Contracts.Interfaces;
using Knowably.Contracts.Options;

namespace Knowably.Infrastructure.Clients;

public sealed class OpenAiEmbeddingClient : IEmbeddingClient
{
    private readonly EmbeddingClient _client;

    public OpenAiEmbeddingClient(IOptions<OpenAiOptions> options)
    {
        var opts = options.Value;
        _client = new EmbeddingClient(opts.EmbeddingModel, opts.ApiKey);
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        var result = await _client.GenerateEmbeddingAsync(text);
        return result.Value.ToFloats().ToArray();
    }

    public async Task<IEnumerable<float[]>> EmbedBatchAsync(IEnumerable<string> texts)
    {
        var textList = texts.ToList();
        var result = await _client.GenerateEmbeddingsAsync(textList);
        return result.Value.Select(e => e.ToFloats().ToArray());
    }
}
