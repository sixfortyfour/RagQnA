namespace Knowably.Contracts.Models;

public sealed class VectorIndexInfo
{
    public long VectorCount { get; set; }
    public int Dimension { get; set; }
    public string SimilarityFunction { get; set; } = string.Empty;
}
