using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Knowably.Contracts.Interfaces;
using Knowably.Contracts.Options;

namespace Knowably.Infrastructure.Clients;

public sealed class OllamaCompletionClient : ICompletionClient
{
    private readonly HttpClient _http;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaCompletionClient(HttpClient http, IOptions<OllamaOptions> options)
    {
        _http = http;
        var opts = options.Value;
        _http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        _model = opts.CompletionModel;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt)
    {
        var payload = new
        {
            model = _model,
            stream = false,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        var response = await _http.PostAsJsonAsync("api/chat", payload, JsonOptions);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama chat failed ({response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
