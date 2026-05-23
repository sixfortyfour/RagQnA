using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Knowably.Contracts.Interfaces;
using Knowably.Contracts.Models;
using Knowably.Contracts.Options;

namespace Knowably.Infrastructure.Clients;

public sealed class QStashClient : IQStashClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string QStashBase = "https://qstash.upstash.io/v2";

    public QStashClient(HttpClient http, IOptions<QStashOptions> options)
    {
        _http = http;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Value.Token);
    }

    public async Task<string> PublishAsync(string destinationUrl, object body, QStashPublishOptions? options = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{QStashBase}/publish/{destinationUrl}")
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };

        if (options?.Retries.HasValue == true)
            request.Headers.Add("Upstash-Retries", options.Retries.Value.ToString());
        if (options?.Delay.HasValue == true)
            request.Headers.Add("Upstash-Delay", $"{(long)options.Delay.Value.TotalSeconds}s");
        if (options?.DeduplicationId is not null)
            request.Headers.Add("Upstash-Deduplication-Id", options.DeduplicationId);

        var response = await _http.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"QStash publish failed ({response.StatusCode}): {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement.GetProperty("messageId").GetString()!;
    }

    public async Task<QStashMessage> GetMessageAsync(string messageId)
    {
        var response = await _http.GetAsync($"{QStashBase}/messages/{messageId}");
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"QStash getmessage failed ({response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        return MapMessage(root);
    }

    public async Task<IEnumerable<QStashMessage>> ListMessagesAsync()
    {
        var response = await _http.GetAsync($"{QStashBase}/messages");
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"QStash list messages failed ({response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var messages = new List<QStashMessage>();
        var array = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("messages", out var arr) ? arr : default;

        if (array.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in array.EnumerateArray())
                messages.Add(MapMessage(el));
        }

        return messages;
    }

    private static QStashMessage MapMessage(JsonElement root) => new()
    {
        MessageId = root.TryGetProperty("messageId", out var mid) ? mid.GetString() ?? "" : "",
        State = root.TryGetProperty("state", out var state) ? state.GetString() ?? "" : "",
        Url = root.TryGetProperty("url", out var url) ? url.GetString() ?? "" : "",
        DeliveredCount = root.TryGetProperty("deliveredCount", out var dc) ? dc.GetInt32() : 0,
        CreatedAt = root.TryGetProperty("createdAt", out var ca)
            ? DateTimeOffset.FromUnixTimeMilliseconds(ca.GetInt64())
            : default
    };
}
