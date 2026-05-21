using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RagQnA.Contracts.Interfaces;
using RagQnA.Contracts.Options;
using RagQnA.Infrastructure.Clients;
using RagQnA.Infrastructure.Security;

namespace RagQnA.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options
        services.Configure<UpstashRedisOptions>(configuration.GetSection("UpstashRedis"));
        services.Configure<UpstashVectorOptions>(configuration.GetSection("UpstashVector"));
        services.Configure<QStashOptions>(configuration.GetSection("QStash"));
        services.Configure<OpenAiOptions>(configuration.GetSection("OpenAi"));
        services.Configure<AnthropicOptions>(configuration.GetSection("Anthropic"));
        services.Configure<IngestionOptions>(configuration.GetSection("Ingestion"));
        services.Configure<CacheOptions>(configuration.GetSection("Cache"));
        services.Configure<MonitorOptions>(configuration.GetSection("Monitor"));

        // Upstash Redis
        services.AddHttpClient<IUpstashRedisClient, UpstashRedisClient>("UpstashRedis")
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<UpstashRedisOptions>>().Value;
                client.BaseAddress = new Uri(opts.RestUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.RestToken);
            });

        // Upstash Vector
        services.AddHttpClient<IUpstashVectorClient, UpstashVectorClient>("UpstashVector")
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<UpstashVectorOptions>>().Value;
                client.BaseAddress = new Uri(opts.RestUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.RestToken);
            });

        // QStash
        services.AddHttpClient<IQStashClient, QStashClient>("QStash");

        // OpenAI Embeddings
        services.AddSingleton<IEmbeddingClient, OpenAiEmbeddingClient>();

        // Anthropic Completions
        services.AddSingleton<ICompletionClient, AnthropicCompletionClient>();

        // QStash signature verifier
        services.AddSingleton<QStashSignatureVerifier>();

        return services;
    }
}
