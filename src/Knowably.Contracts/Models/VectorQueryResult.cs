namespace Knowably.Contracts.Models;

public sealed class VectorQueryResult
{
    public string Id { get; set; } = string.Empty;
    public float Score { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}
