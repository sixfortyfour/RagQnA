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
    private readonly IUpstashVectorClient _vector;
    private readonly IEmbeddingClient _embeddings;
    private readonly ITextChunker _chunker;
    private readonly ITextExtractor _extractor;
    private readonly QStashSignatureVerifier _signatureVerifier;

    public InternalController(
        IUpstashRedisClient redis,
        IUpstashVectorClient vector,
        IEmbeddingClient embeddings,
        ITextChunker chunker,
        ITextExtractor extractor,
        QStashSignatureVerifier signatureVerifier)
    {
        _redis = redis;
        _vector = vector;
        _embeddings = embeddings;
        _chunker = chunker;
        _extractor = extractor;
        _signatureVerifier = signatureVerifier;
    }

    [HttpPost("process-document")]
    public async Task<IActionResult> ProcessDocument()
    {
        Request.EnableBuffering();

        var rawBody = await new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
        Request.Body.Seek(0, SeekOrigin.Begin);

        if (!Request.Headers.TryGetValue("Upstash-Signature", out var jwtValues) || string.IsNullOrEmpty(jwtValues))
            return Unauthorized("Missing Upstash-Signature header.");

        var jwt = jwtValues.ToString();
        var requestUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}";

        if (!_signatureVerifier.Verify(jwt, rawBody, requestUrl))
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

        try
        {
            var text = await _extractor.ExtractAsync(tempFilePath);

            if (System.IO.File.Exists(tempFilePath))
                System.IO.File.Delete(tempFilePath);

            await _redis.HSetAsync($"rag:doc:{documentId}", new Dictionary<string, string>
            {
                ["status"] = DocumentStatus.Indexing.ToString()
            });

            var chunks = _chunker.Chunk(text).ToList();

            var chunkTexts = chunks.Select(c => c.Text);
            var embeddings = (await _embeddings.EmbedBatchAsync(chunkTexts)).ToList();

            var records = chunks.Zip(embeddings, (chunk, vector) => new VectorRecord
            {
                Id = $"{documentId}_chunk_{chunk.Index}",
                Vector = vector,
                Metadata = new Dictionary<string, string>
                {
                    ["docId"] = documentId,
                    ["chunkIndex"] = chunk.Index.ToString(),
                    ["text"] = chunk.Text,
                    ["source"] = fileName
                }
            }).ToList();

            await _vector.UpsertAsync(records);

            await _redis.HSetAsync($"rag:doc:{documentId}", new Dictionary<string, string>
            {
                ["status"] = DocumentStatus.Indexed.ToString(),
                ["chunkCount"] = records.Count.ToString(),
                ["indexedAt"] = DateTimeOffset.UtcNow.ToString("O")
            });

            return Ok(new { documentId, chunkCount = records.Count });
        }
        catch (Exception ex)
        {
            await _redis.HSetAsync($"rag:doc:{documentId}", new Dictionary<string, string>
            {
                ["status"] = DocumentStatus.Failed.ToString(),
                ["errorMessage"] = ex.Message
            });
            throw;
        }
    }

    private sealed class ProcessDocumentPayload
    {
        public string DocumentId { get; init; } = string.Empty;
    }
}
