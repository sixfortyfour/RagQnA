using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Knowably.Contracts.Enums;
using Knowably.Contracts.Interfaces;
using Knowably.Contracts.Models;
using Knowably.Infrastructure.Security;

namespace Knowably.Api.Controllers;

[ApiController]
[Route("internal")]
public sealed class InternalController : ControllerBase
{
    private readonly IUpstashRedisClient _redis;
    private readonly QStashSignatureVerifier _signatureVerifier;
    private readonly IServiceScopeFactory _scopeFactory;

    public InternalController(
        IUpstashRedisClient redis,
        QStashSignatureVerifier signatureVerifier,
        IServiceScopeFactory scopeFactory)
    {
        _redis = redis;
        _signatureVerifier = signatureVerifier;
        _scopeFactory = scopeFactory;
    }

    [HttpPost("process-document")]
    public async Task<IActionResult> ProcessDocument()
    {
        Request.EnableBuffering();

        var rawBody = await new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
        Request.Body.Seek(0, SeekOrigin.Begin);

        if (!Request.Headers.TryGetValue("Upstash-Signature", out var jwtValues) || string.IsNullOrEmpty(jwtValues))
            return Unauthorized("Missing Upstash-Signature header.");

        if (!_signatureVerifier.Verify(jwtValues.ToString(), rawBody, $"{Request.Scheme}://{Request.Host}{Request.Path}"))
            return Unauthorized("Invalid or expired Upstash-Signature.");

        var payload = JsonSerializer.Deserialize<ProcessDocumentPayload>(rawBody, JsonSerializerOptions.Web);
        if (payload is null || string.IsNullOrEmpty(payload.DocumentId))
            return BadRequest("Missing documentId in payload.");

        var documentId = payload.DocumentId;
        var fields = await _redis.HGetAllAsync($"rag:doc:{documentId}");
        if (fields.Count == 0)
            return NotFound($"Document {documentId} not found.");

        if (!fields.TryGetValue("tempFilePath", out var tempFilePath) || string.IsNullOrEmpty(tempFilePath))
            return BadRequest("No tempFilePath found for document.");

        var fileName = fields.GetValueOrDefault("fileName") ?? documentId;

        _ = Task.Run(() => ProcessInBackgroundAsync(documentId, tempFilePath, fileName));

        return Ok(new { documentId, status = "processing" });
    }

    private async Task ProcessInBackgroundAsync(string documentId, string tempFilePath, string fileName)
    {
        using var scope = _scopeFactory.CreateScope();
        var redis    = scope.ServiceProvider.GetRequiredService<IUpstashRedisClient>();
        var vector   = scope.ServiceProvider.GetRequiredService<IUpstashVectorClient>();
        var embedder = scope.ServiceProvider.GetRequiredService<IEmbeddingClient>();
        var chunker  = scope.ServiceProvider.GetRequiredService<ITextChunker>();
        var extractor = scope.ServiceProvider.GetRequiredService<ITextExtractor>();

        try
        {
            string text;
            try
            {
                text = await extractor.ExtractAsync(tempFilePath);
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath))
                    System.IO.File.Delete(tempFilePath);
            }

            await redis.HSetAsync($"rag:doc:{documentId}", new Dictionary<string, string>
            {
                ["status"] = DocumentStatus.Indexing.ToString()
            });

            var chunks = chunker.Chunk(text).ToList();
            var vectors = (await embedder.EmbedBatchAsync(chunks.Select(c => c.Text))).ToList();

            var records = chunks.Zip(vectors, (chunk, vec) => new VectorRecord
            {
                Id = $"{documentId}_chunk_{chunk.Index}",
                Vector = vec,
                Metadata = new Dictionary<string, string>
                {
                    ["docId"]      = documentId,
                    ["chunkIndex"] = chunk.Index.ToString(),
                    ["text"]       = chunk.Text,
                    ["source"]     = fileName
                }
            }).ToList();

            await vector.UpsertAsync(records);

            await redis.HSetAsync($"rag:doc:{documentId}", new Dictionary<string, string>
            {
                ["status"]     = DocumentStatus.Indexed.ToString(),
                ["chunkCount"] = records.Count.ToString(),
                ["indexedAt"]  = DateTimeOffset.UtcNow.ToString("O")
            });
        }
        catch (Exception ex)
        {
            await redis.HSetAsync($"rag:doc:{documentId}", new Dictionary<string, string>
            {
                ["status"]       = DocumentStatus.Failed.ToString(),
                ["errorMessage"] = ex.Message
            });
        }
    }

    private sealed class ProcessDocumentPayload
    {
        public string DocumentId { get; init; } = string.Empty;
    }
}
