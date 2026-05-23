namespace Knowably.Contracts.Options;

public sealed class AnthropicOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string CompletionModel { get; set; } = "claude-sonnet-4-20250514";
}
