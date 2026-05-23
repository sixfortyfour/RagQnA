using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Knowably.Contracts.Interfaces;
using Knowably.Contracts.Models;
using Knowably.Infrastructure.Exceptions;

namespace Knowably.Infrastructure.Clients;

public sealed class UpstashVectorClient : IUpstashVectorClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public UpstashVectorClient(HttpClient http)
    {
        _http = http;
    }

    public async Task UpsertAsync(IEnumerable<VectorRecord> records)
    {
        var payload = records.Select(r => new
        {
            id = r.Id,
            vector = r.Vector,
            metadata = r.Metadata
        });

        var response = await _http.PostAsJsonAsync("upsert", payload, JsonOptions);
        await EnsureSuccessAsync(response, "upsert");
    }

    public async Task<IEnumerable<VectorQueryResult>> QueryAsync(float[] vector, int topK, string? filter = null)
    {
        var payload = new { vector, topK, filter, includeMetadata = true };
        var response = await _http.PostAsJsonAsync("query", payload, JsonOptions);
        await EnsureSuccessAsync(response, "query");

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var results = doc.RootElement.GetProperty("result");

        return results.EnumerateArray().Select(r => new VectorQueryResult
        {
            Id = r.GetProperty("id").GetString()!,
            Score = r.GetProperty("score").GetSingle(),
            Metadata = r.TryGetProperty("metadata", out var meta)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(meta.GetRawText(), JsonOptions) ?? []
                : []
        }).ToList();
    }

    public async Task<IEnumerable<VectorRecord>> FetchAsync(IEnumerable<string> ids)
    {
        var payload = new { ids = ids.ToArray(), includeVectors = true, includeMetadata = true };
        var response = await _http.PostAsJsonAsync("fetch", payload, JsonOptions);
        await EnsureSuccessAsync(response, "fetch");

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var results = doc.RootElement.GetProperty("result");

        return results.EnumerateArray().Select(r => new VectorRecord
        {
            Id = r.GetProperty("id").GetString()!,
            Vector = r.TryGetProperty("vector", out var vec)
                ? JsonSerializer.Deserialize<float[]>(vec.GetRawText())!
                : [],
            Metadata = r.TryGetProperty("metadata", out var meta)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(meta.GetRawText(), JsonOptions) ?? []
                : []
        }).ToList();
    }

    public async Task DeleteAsync(IEnumerable<string> ids)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "delete")
        {
            Content = JsonContent.Create(new { ids = ids.ToArray() }, options: JsonOptions)
        };
        var response = await _http.SendAsync(request);
        await EnsureSuccessAsync(response, "delete");
    }

    public async Task DeleteByFilterAsync(string filter)
    {
        var payload = new { filter };
        var response = await _http.PostAsJsonAsync("delete", payload, JsonOptions);
        await EnsureSuccessAsync(response, "delete-by-filter");
    }

    public async Task<VectorIndexInfo> InfoAsync()
    {
        var response = await _http.GetAsync("info");
        await EnsureSuccessAsync(response, "info");

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var result = doc.RootElement.GetProperty("result");

        return new VectorIndexInfo
        {
            VectorCount = result.TryGetProperty("vectorCount", out var vc) ? vc.GetInt64() : 0,
            Dimension = result.TryGetProperty("dimension", out var dim) ? dim.GetInt32() : 0,
            SimilarityFunction = result.TryGetProperty("similarityFunction", out var sf) ? sf.GetString() ?? "" : ""
        };
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new UpstashVectorException($"Vector {operation} failed ({response.StatusCode}): {body}");
        }
    }
}
