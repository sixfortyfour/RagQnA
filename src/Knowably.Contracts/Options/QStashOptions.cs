namespace Knowably.Contracts.Options;

public sealed class QStashOptions
{
    public string Token { get; set; } = string.Empty;
    public string CurrentSigningKey { get; set; } = string.Empty;
    public string NextSigningKey { get; set; } = string.Empty;
}
