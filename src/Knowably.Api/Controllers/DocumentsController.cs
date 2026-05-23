using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Knowably.Contracts.Enums;
using Knowably.Contracts.Interfaces;
using Knowably.Contracts.Models;
using Knowably.Contracts.Options;

namespace Knowably.Api.Controllers;

[ApiController]
[Route("documents")]
public sealed class DocumentsController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".txt", ".md"
    };

    private readonly IUpstashRedisClient _redis;
    private readonly IUpstashVectorClient _vector;
    private readonly IQStashClient _qstash;
    private readonly IngestionOptions _ingestionOptions;
    private readonly IConfiguration _configuration;

    public DocumentsController(
        IUpstashRedisClient redis,
        IUpstashVectorClient vector,
        IQStashClient qstash,
        IOptions<IngestionOptions> ingestionOptions,
        IConfiguration configuration)
    {
        _redis = redis;
        _vector = vector;
        _qstash = qstash;
        _ingestionOptions = ingestionOptions.Value;
        _configuration = configuration;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDocument(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            return BadRequest($"Unsupported file type '{extension}'. Allowed: .pdf, .txt, .md");

        var fileSizeBytes = file.Length;
        var maxBytes = (long)_ingestionOptions.MaxFileSizeMb * 1024 * 1024;
        if (fileSizeBytes > maxBytes)
            return BadRequest($"File size exceeds the maximum allowed size of {_ingestionOptions.MaxFileSizeMb} MB.");

        var documentId = Guid.NewGuid().ToString();
        var tempFilePath = Path.Combine(Path.GetTempPath(), documentId + extension);

        await using (var stream = System.IO.File.Create(tempFilePath))
        {
            await file.CopyToAsync(stream);
        }

        await _redis.HSetAsync($"rag:doc:{documentId}", new Dictionary<string, string>
        {
            ["fileName"] = file.FileName,
            ["status"] = DocumentStatus.Pending.ToString(),
            ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["tempFilePath"] = tempFilePath
        });

        await _redis.SAddAsync("rag:doc:all", documentId);

        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var callbackUrl = $"{apiBaseUrl.TrimEnd('/')}/internal/process-document";
        await _qstash.PublishAsync(callbackUrl, new { documentId });

        return Accepted(new UploadDocumentResponse
        {
            DocumentId = documentId,
            StatusUrl = $"/documents/{documentId}/status"
        });
    }

    [HttpGet]
    public async Task<IActionResult> ListDocuments()
    {
        var ids = await _redis.SMembersAsync("rag:doc:all");
        var idList = ids.ToList();

        var tasks = idList.Select(id => _redis.HGetAllAsync($"rag:doc:{id}"));
        var results = await Task.WhenAll(tasks);

        var documents = idList.Zip(results, (id, fields) => MapToListItem(id, fields))
            .Where(d => d is not null)
            .ToList();

        return Ok(documents);
    }

    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetDocumentStatus(string id)
    {
        var fields = await _redis.HGetAllAsync($"rag:doc:{id}");
        if (fields.Count == 0)
            return NotFound();

        return Ok(MapToMetadata(id, fields));
    }

    [HttpGet("{id}/chunks")]
    public async Task<IActionResult> GetDocumentChunks(string id)
    {
        var zeroVector = new float[768];
        var results = await _vector.QueryAsync(zeroVector, 100, $"docId = '{id}'");
        return Ok(results);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDocument(string id)
    {
        var fields = await _redis.HGetAllAsync($"rag:doc:{id}");

        await _redis.DeleteAsync($"rag:doc:{id}");
        await _redis.SRemAsync("rag:doc:all", id);
        await _vector.DeleteByFilterAsync($"docId = '{id}'");

        if (fields.TryGetValue("tempFilePath", out var tempFilePath)
            && !string.IsNullOrEmpty(tempFilePath)
            && System.IO.File.Exists(tempFilePath))
        {
            System.IO.File.Delete(tempFilePath);
        }

        return NoContent();
    }

    private static DocumentListItem? MapToListItem(string id, Dictionary<string, string> fields)
    {
        if (fields.Count == 0) return null;

        _ = Enum.TryParse<DocumentStatus>(fields.GetValueOrDefault("status"), out var status);
        _ = DateTimeOffset.TryParse(fields.GetValueOrDefault("createdAt"), out var createdAt);
        int? chunkCount = int.TryParse(fields.GetValueOrDefault("chunkCount"), out var cc) ? cc : null;

        return new DocumentListItem
        {
            Id = id,
            FileName = fields.GetValueOrDefault("fileName") ?? string.Empty,
            Status = status,
            CreatedAt = createdAt,
            ChunkCount = chunkCount
        };
    }

    private static DocumentMetadata MapToMetadata(string id, Dictionary<string, string> fields)
    {
        _ = Enum.TryParse<DocumentStatus>(fields.GetValueOrDefault("status"), out var status);
        _ = DateTimeOffset.TryParse(fields.GetValueOrDefault("createdAt"), out var createdAt);
        int? chunkCount = int.TryParse(fields.GetValueOrDefault("chunkCount"), out var cc) ? cc : null;
        DateTimeOffset? indexedAt = DateTimeOffset.TryParse(fields.GetValueOrDefault("indexedAt"), out var ia) ? ia : null;

        return new DocumentMetadata
        {
            Id = id,
            FileName = fields.GetValueOrDefault("fileName") ?? string.Empty,
            Status = status,
            CreatedAt = createdAt,
            TempFilePath = fields.GetValueOrDefault("tempFilePath"),
            ChunkCount = chunkCount,
            IndexedAt = indexedAt,
            ErrorMessage = fields.GetValueOrDefault("errorMessage")
        };
    }
}
