using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Knowably.Api.Filters;
using Knowably.Contracts.Interfaces;
using Knowably.Contracts.Models;

namespace Knowably.Api.Controllers;

[ApiController]
[Route("monitor")]
[RequireMonitorKey]
public sealed class MonitorController : ControllerBase
{
    private readonly IUpstashRedisClient _redis;
    private readonly IUpstashVectorClient _vector;
    private readonly IEmbeddingClient _embeddings;
    private readonly IQStashClient _qstash;

    public MonitorController(
        IUpstashRedisClient redis,
        IUpstashVectorClient vector,
        IEmbeddingClient embeddings,
        IQStashClient qstash)
    {
        _redis = redis;
        _vector = vector;
        _embeddings = embeddings;
        _qstash = qstash;
    }

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments()
    {
        var ids = (await _redis.SMembersAsync("rag:doc:all")).ToList();
        var tasks = ids.Select(id => _redis.HGetAllAsync($"rag:doc:{id}"));
        var results = await Task.WhenAll(tasks);

        var documents = ids.Zip(results, (id, fields) =>
        {
            if (fields.Count == 0) return null;

            _ = Enum.TryParse<Knowably.Contracts.Enums.DocumentStatus>(fields.GetValueOrDefault("status"), out var status);
            _ = DateTimeOffset.TryParse(fields.GetValueOrDefault("createdAt"), out var createdAt);
            int? chunkCount = int.TryParse(fields.GetValueOrDefault("chunkCount"), out var cc) ? cc : null;
            DateTimeOffset? indexedAt = DateTimeOffset.TryParse(fields.GetValueOrDefault("indexedAt"), out var ia) ? ia : null;

            return new DocumentMetadata
            {
                Id = id,
                FileName = fields.GetValueOrDefault("fileName") ?? string.Empty,
                Status = status,
                CreatedAt = createdAt,
                ChunkCount = chunkCount,
                IndexedAt = indexedAt,
                ErrorMessage = fields.GetValueOrDefault("errorMessage")
            };
        })
        .Where(d => d is not null)
        .ToList();

        return Ok(documents);
    }

    [HttpGet("cache")]
    public async Task<IActionResult> GetCacheEntries()
    {
        var keys = (await _redis.KeysAsync("rag:cache:*")).ToList();

        var valueTasks = keys.Select(k => _redis.GetAsync(k));
        var ttlTasks = keys.Select(k => _redis.TtlAsync(k));

        var values = await Task.WhenAll(valueTasks);
        var ttls = await Task.WhenAll(ttlTasks);

        var entries = keys.Select((key, i) =>
        {
            var hash = key["rag:cache:".Length..];
            var question = string.Empty;

            if (values[i] is not null)
            {
                try
                {
                    var resp = JsonSerializer.Deserialize<QuestionResponse>(values[i]!, JsonSerializerOptions.Web);
                    question = resp?.Question ?? string.Empty;
                }
                catch { }
            }

            return new MonitorCacheEntry
            {
                Hash = hash,
                Question = question,
                TtlSeconds = ttls[i]
            };
        })
        .ToList();

        return Ok(entries);
    }

    [HttpDelete("cache/{hash}")]
    public async Task<IActionResult> DeleteCacheEntry(string hash)
    {
        await _redis.DeleteAsync($"rag:cache:{hash}");
        return NoContent();
    }

    [HttpDelete("cache")]
    public async Task<IActionResult> FlushCache()
    {
        var keys = await _redis.KeysAsync("rag:cache:*");
        var deleteTasks = keys.Select(k => _redis.DeleteAsync(k));
        await Task.WhenAll(deleteTasks);
        return NoContent();
    }

    [HttpGet("qstash-jobs")]
    public async Task<IActionResult> GetQStashJobs()
    {
        var messages = await _qstash.ListMessagesAsync();
        return Ok(messages);
    }

    [HttpPost("vector/query")]
    public async Task<IActionResult> VectorProbe([FromBody] VectorProbeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Text must not be empty.");

        var vector = await _embeddings.EmbedAsync(request.Text);
        var results = await _vector.QueryAsync(vector, 10);
        return Ok(results);
    }
}
