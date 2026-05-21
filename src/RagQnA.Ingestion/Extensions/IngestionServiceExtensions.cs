using Microsoft.Extensions.DependencyInjection;
using RagQnA.Contracts.Interfaces;
using RagQnA.Ingestion.Chunking;
using RagQnA.Ingestion.Extraction;

namespace RagQnA.Ingestion.Extensions;

public static class IngestionServiceExtensions
{
    public static IServiceCollection AddIngestion(this IServiceCollection services)
    {
        services.AddScoped<ITextChunker, SlidingWindowChunker>();
        services.AddScoped<ITextExtractor, TextExtractor>();
        return services;
    }
}
