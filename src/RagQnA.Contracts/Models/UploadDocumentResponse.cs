namespace RagQnA.Contracts.Models;

public sealed class UploadDocumentResponse
{
    public string DocumentId { get; init; } = string.Empty;
    public string StatusUrl { get; init; } = string.Empty;
}
