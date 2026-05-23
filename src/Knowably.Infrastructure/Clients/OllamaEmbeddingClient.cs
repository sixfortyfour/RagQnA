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
        var results = new List<float[]>();
        foreach (var text in texts)
            results.Add(await EmbedAsync(text));
        return results;
    }
}
