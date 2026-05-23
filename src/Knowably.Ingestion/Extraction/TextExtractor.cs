using Knowably.Contracts.Interfaces;
using UglyToad.PdfPig;

namespace Knowably.Ingestion.Extraction;

public sealed class TextExtractor : ITextExtractor
{
    public async Task<string> ExtractAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".txt" or ".md" => await File.ReadAllTextAsync(filePath),
            ".pdf" => ExtractPdf(filePath),
            _ => throw new NotSupportedException($"Unsupported file extension: {extension}")
        };
    }

    private static string ExtractPdf(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        var words = document.GetPages()
            .SelectMany(page => page.GetWords())
            .Select(word => word.Text);
        return string.Join(" ", words);
    }
}
