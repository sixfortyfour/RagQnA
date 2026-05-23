using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Knowably.Contracts.Interfaces;
using Knowably.Contracts.Options;

namespace Knowably.Infrastructure.Clients;

public sealed class OllamaEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _http;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaEmbeddingClient(HttpClient http, IOptions<OllamaOptions> options)
    {
        _http = http;
        var opts = options.Value;
        _http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        _model = opts.EmbeddingModel;
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        var response = await _http.PostAsJsonAsync("api/embed", new { model = _model, input = text }, JsonOptions);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama embed failed ({response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var embeddings = doc.RootElement.GetProperty("embeddings")[0];
        return embeddings.EnumerateArray().Select(v => v.GetSingle()).ToArray();
    }

    public async Task<IEnumerable<float[]>> EmbedBatchAsync(IEnumerable<string> texts)
    {
        var textList = texts.ToList();
        if (textList.Count == 0)
            return [];

        // Split into sub-batches and process concurrently. Ollama is single-threaded so
        // large parallelism doesn't help — 3 concurrent requests of ~20 keeps each request
        // short while still saturating the model.
        const int subBatchSize = 20;
        const int maxConcurrency = 3;

        var batches = textList.Chunk(subBatchSize).ToList();
        var results = new float[textList.Count][];
        var sem = new SemaphoreSlim(maxConcurrency);

        await Task.WhenAll(batches.Select(async (batch, batchIndex) =>
        {
            await sem.WaitAsync();
            try
            {
                var embeddings = await EmbedSubBatchAsync(batch);
                for (var i = 0; i < embeddings.Count; i++)
                    results[batchIndex * subBatchSize + i] = embeddings[i];
            }
            finally
            {
                sem.Release();
            }
        }));

        return results;
    }

    private async Task<List<float[]>> EmbedSubBatchAsync(string[] texts)
    {
        var response = await _http.PostAsJsonAsync("api/embed", new { model = _model, input = texts }, JsonOptions);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama embed failed ({response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("embeddings")
            .EnumerateArray()
            .Select(e => e.EnumerateArray().Select(v => v.GetSingle()).ToArray())
            .ToList();
    }
}
