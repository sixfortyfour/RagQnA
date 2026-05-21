using Microsoft.AspNetCore.Mvc;
using RagQnA.Contracts.Interfaces;
using RagQnA.Contracts.Models;

namespace RagQnA.Api.Controllers;

[ApiController]
[Route("stats")]
public sealed class StatsController : ControllerBase
{
    private readonly IUpstashRedisClient _redis;

    public StatsController(IUpstashRedisClient redis)
    {
        _redis = redis;
    }

    [HttpGet]
    public async Task<IActionResult> GetStats()
    {
        var queriesRaw = await _redis.GetAsync("rag:stats:queries");
        var hitsRaw = await _redis.GetAsync("rag:stats:hits");
        var missesRaw = await _redis.GetAsync("rag:stats:misses");

        var queries = long.TryParse(queriesRaw, out var q) ? q : 0L;
        var hits = long.TryParse(hitsRaw, out var h) ? h : 0L;
        var misses = long.TryParse(missesRaw, out var m) ? m : 0L;

        var hitRate = queries > 0 ? Math.Round((double)hits / queries * 100, 2) : 0.0;

        return Ok(new StatsResponse
        {
            TotalQueries = queries,
            CacheHits = hits,
            CacheMisses = misses,
            HitRatePercent = hitRate
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetStatsHistory()
    {
        var keys = (await _redis.KeysAsync("rag:stats:history:*")).ToList();

        var tasks = keys.Select(k => _redis.HGetAllAsync(k));
        var results = await Task.WhenAll(tasks);

        var buckets = keys.Zip(results, (key, fields) =>
        {
            var bucket = key["rag:stats:history:".Length..];
            _ = long.TryParse(fields.GetValueOrDefault("hits"), out var hits);
            _ = long.TryParse(fields.GetValueOrDefault("misses"), out var misses);
            return new StatsBucket { Bucket = bucket, Hits = hits, Misses = misses };
        })
        .OrderBy(b => b.Bucket)
        .TakeLast(60)
        .ToList();

        return Ok(buckets);
    }
}
