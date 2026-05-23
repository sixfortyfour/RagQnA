using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Knowably.Contracts.Interfaces;
using Knowably.Infrastructure.Exceptions;

namespace Knowably.Infrastructure.Clients;

public sealed class UpstashRedisClient : IUpstashRedisClient
{
    private readonly HttpClient _http;

    public UpstashRedisClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string?> GetAsync(string key)
    {
        var response = await ExecuteAsync<JsonElement>("GET", key);
        if (response.ValueKind == JsonValueKind.Null) return null;
        return response.GetString();
    }

    public async Task SetAsync(string key, string value, TimeSpan? ttl = null)
    {
        if (ttl.HasValue)
            await ExecuteAsync<JsonElement>("SET", key, value, "EX", ((long)ttl.Value.TotalSeconds).ToString());
        else
            await ExecuteAsync<JsonElement>("SET", key, value);
    }

    public async Task<long> IncrAsync(string key)
    {
        var result = await ExecuteAsync<long>("INCR", key);
        return result;
    }

    public async Task HSetAsync(string key, Dictionary<string, string> fields)
    {
        var args = new List<string> { key };
        foreach (var (k, v) in fields)
        {
            args.Add(k);
            args.Add(v);
        }
        await ExecuteAsync<JsonElement>("HSET", [.. args]);
    }

    public async Task<Dictionary<string, string>> HGetAllAsync(string key)
    {
        var result = await ExecuteAsync<string[]>("HGETALL", key);
        var dict = new Dictionary<string, string>();
        for (var i = 0; i < result.Length - 1; i += 2)
            dict[result[i]] = result[i + 1];
        return dict;
    }

    public async Task SAddAsync(string key, string member)
        => await ExecuteAsync<JsonElement>("SADD", key, member);

    public async Task SRemAsync(string key, string member)
        => await ExecuteAsync<JsonElement>("SREM", key, member);

    public async Task<IEnumerable<string>> SMembersAsync(string key)
    {
        var result = await ExecuteAsync<string[]>("SMEMBERS", key);
        return result;
    }

    public async Task DeleteAsync(string key)
        => await ExecuteAsync<JsonElement>("DEL", key);

    public async Task<IEnumerable<string>> KeysAsync(string pattern)
    {
        // SCAN-based: collect all keys matching the pattern
        var keys = new List<string>();
        var cursor = "0";
        do
        {
            var scanResult = await ExecuteRawAsync("SCAN", cursor, "MATCH", pattern, "COUNT", "100");
            var arr = scanResult.EnumerateArray().ToArray();
            cursor = arr[0].GetString()!;
            foreach (var key in arr[1].EnumerateArray())
                keys.Add(key.GetString()!);
        } while (cursor != "0");

        return keys;
    }

    public async Task<long> TtlAsync(string key)
    {
        var result = await ExecuteAsync<long>("TTL", key);
        return result;
    }

    public async Task<long> HIncrByAsync(string key, string field, long increment)
    {
        var result = await ExecuteAsync<long>("HINCRBY", key, field, increment.ToString());
        return result;
    }

    // --- helpers ---

    private async Task<T> ExecuteAsync<T>(string command, params string[] args)
    {
        var segments = new[] { command }.Concat(args)
            .Select(Uri.EscapeDataString);
        var path = string.Join("/", segments);

        var response = await _http.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new UpstashRedisException($"Redis command {command} failed ({response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var err))
            throw new UpstashRedisException($"Redis error: {err.GetString()}");

        var resultEl = root.GetProperty("result");
        return JsonSerializer.Deserialize<T>(resultEl.GetRawText())!;
    }

    private async Task<JsonElement> ExecuteRawAsync(string command, params string[] args)
    {
        var segments = new[] { command }.Concat(args)
            .Select(Uri.EscapeDataString);
        var path = string.Join("/", segments);

        var response = await _http.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new UpstashRedisException($"Redis command {command} failed ({response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var err))
            throw new UpstashRedisException($"Redis error: {err.GetString()}");

        // Return a clone so the document can be disposed
        return root.GetProperty("result").Clone();
    }
}
