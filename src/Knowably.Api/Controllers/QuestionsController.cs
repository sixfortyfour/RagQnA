using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Knowably.Contracts.Interfaces;
using Knowably.Contracts.Models;
using Knowably.Contracts.Options;

namespace Knowably.Api.Controllers;

[ApiController]
[Route("questions")]
public sealed class QuestionsController : ControllerBase
{
    private readonly IUpstashRedisClient _redis;
    private readonly IUpstashVectorClient _vector;
    private readonly IEmbeddingClient _embeddings;
    private readonly ICompletionClient _completion;
    private readonly CacheOptions _cacheOptions;

    public QuestionsController(
        IUpstashRedisClient redis,
        IUpstashVectorClient vector,
        IEmbeddingClient embeddings,
        ICompletionClient completion,
        IOptions<CacheOptions> cacheOptions)
    {
        _redis = redis;
        _vector = vector;
        _embeddings = embeddings;
        _completion = completion;
        _cacheOptions = cacheOptions.Value;
    }

    [HttpPost]
    public async Task<IActionResult> AskQuestion([FromBody] QuestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question must not be empty.");

        var sw = Stopwatch.StartNew();

        var normalised = NormaliseQuestion(request.Question);
        var cacheKey = $"rag:cache:{HashQuestion(normalised)}";

        var cached = await _redis.GetAsync(cacheKey);
        if (cached is not null)
        {
            await _redis.IncrAsync("rag:stats:hits");
            await _redis.IncrAsync("rag:stats:queries");
            await RecordStatBucketAsync("hits");

            var cachedResponse = JsonSerializer.Deserialize<QuestionResponse>(cached, JsonSerializerOptions.Web)!;
            sw.Stop();

            return Ok(cachedResponse with { Cached = true, DurationMs = sw.ElapsedMilliseconds });
        }

        await _redis.IncrAsync("rag:stats:queries");
        await _redis.IncrAsync("rag:stats:misses");

        var questionVector = await _embeddings.EmbedAsync(normalised);
        var vectorResults = (await _vector.QueryAsync(questionVector, 5)).ToList();

        var sourceChunks = vectorResults
            .Select(r => new SourceChunk
            {
                Text = r.Metadata.GetValueOrDefault("text", string.Empty),
                DocumentId = r.Metadata.GetValueOrDefault("docId", string.Empty),
                ChunkIndex = int.TryParse(r.Metadata.GetValueOrDefault("chunkIndex"), out var ci) ? ci : 0,
                Score = r.Score
            })
            .ToList();

        var systemPrompt = BuildSystemPrompt(sourceChunks);
        var answer = await _completion.CompleteAsync(systemPrompt, normalised);

        sw.Stop();

        var response = new QuestionResponse
        {
            Question = normalised,
            Answer = answer,
            Cached = false,
            DurationMs = sw.ElapsedMilliseconds,
            SourceChunks = sourceChunks
        };

        var serialised = JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
        await _redis.SetAsync(cacheKey, serialised, TimeSpan.FromSeconds(_cacheOptions.TtlSeconds));

        await RecordStatBucketAsync("misses");

        return Ok(response);
    }

    private static string NormaliseQuestion(string question)
        => Regex.Replace(question.Trim().ToLowerInvariant(), @"\s+", " ");

    private static string HashQuestion(string normalised)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalised));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildSystemPrompt(IEnumerable<SourceChunk> chunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a helpful assistant. Answer the user's question using only the context provided below.");
        sb.AppendLine("If the answer is not in the context, say so clearly. Do not make up information.");
        sb.AppendLine();
        sb.AppendLine("CONTEXT:");
        var i = 1;
        foreach (var chunk in chunks)
        {
            sb.AppendLine($"[{i++}] (source: {chunk.DocumentId}, chunk {chunk.ChunkIndex})");
            sb.AppendLine(chunk.Text);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private async Task RecordStatBucketAsync(string field)
    {
        var bucket = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm");
        await _redis.HIncrByAsync($"rag:stats:history:{bucket}", field, 1);
    }
}
