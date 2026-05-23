using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Options;
using Knowably.Contracts.Interfaces;
using Knowably.Contracts.Options;

namespace Knowably.Infrastructure.Clients;

public sealed class AnthropicCompletionClient : ICompletionClient
{
    private readonly AnthropicClient _client;
    private readonly string _model;

    public AnthropicCompletionClient(IOptions<AnthropicOptions> options)
    {
        var opts = options.Value;
        _client = new AnthropicClient(opts.ApiKey);
        _model = opts.CompletionModel;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt)
    {
        var parameters = new MessageParameters
        {
            Model = _model,
            MaxTokens = 1024,
            System = [new SystemMessage(systemPrompt)],
            Messages =
            [
                new Message { Role = RoleType.User, Content = [new TextContent { Text = userPrompt }] }
            ]
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters);
        return response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;
    }
}
