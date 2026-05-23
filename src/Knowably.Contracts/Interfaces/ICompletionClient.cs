namespace Knowably.Contracts.Interfaces;

public interface ICompletionClient
{
    Task<string> CompleteAsync(string systemPrompt, string userPrompt);
}
