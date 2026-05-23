namespace Knowably.Contracts.Options;

public sealed class IngestionOptions
{
    public int ChunkSize { get; set; } = 512;
    public int ChunkOverlapPercent { get; set; } = 10;
    public int MaxFileSizeMb { get; set; } = 5;
}
