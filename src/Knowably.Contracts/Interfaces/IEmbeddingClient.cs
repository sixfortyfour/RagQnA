namespace Knowably.Contracts.Interfaces;

public interface IEmbeddingClient
{
    Task<float[]> EmbedAsync(string text);
    Task<IEnumerable<float[]>> EmbedBatchAsync(IEnumerable<string> texts);
}
