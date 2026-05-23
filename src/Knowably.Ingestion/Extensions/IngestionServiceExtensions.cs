using Microsoft.Extensions.DependencyInjection;
using Knowably.Contracts.Interfaces;
using Knowably.Ingestion.Chunking;
using Knowably.Ingestion.Extraction;

namespace Knowably.Ingestion.Extensions;

public static class IngestionServiceExtensions
{
    public static IServiceCollection AddIngestion(this IServiceCollection services)
    {
        services.AddScoped<ITextChunker, SlidingWindowChunker>();
        services.AddScoped<ITextExtractor, TextExtractor>();
        return services;
    }
}
