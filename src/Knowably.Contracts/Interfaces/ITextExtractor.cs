namespace Knowably.Contracts.Interfaces;

public interface ITextExtractor
{
    Task<string> ExtractAsync(string filePath);
}
