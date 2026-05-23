namespace Knowably.Contracts.Models;

public sealed class VectorRecord
{
    public string Id { get; set; } = string.Empty;
    public float[] Vector { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}
