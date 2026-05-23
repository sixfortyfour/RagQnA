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
        Span<byte> header = stackalloc byte[5];
        using (var fs = File.OpenRead(filePath))
        {
            var read = fs.Read(header);
            if (read < 5 || header[0] != 0x25 || header[1] != 0x50 ||
                header[2] != 0x44 || header[3] != 0x46 || header[4] != 0x2D)
                throw new InvalidDataException("File does not appear to be a valid PDF (missing %PDF- header).");
        }

        using var document = PdfDocument.Open(filePath);
        var words = document.GetPages()
            .SelectMany(page => page.GetWords())
            .Select(word => word.Text);
        return string.Join(" ", words);
    }
}
