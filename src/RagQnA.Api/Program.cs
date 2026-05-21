using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using RagQnA.Api.Middleware;
using RagQnA.Contracts.Options;
using RagQnA.Infrastructure.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure (clients + options)
builder.Services.AddInfrastructure(builder.Configuration);

// Controllers
builder.Services.AddControllers();

// Swagger / Scalar
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RagQnA API",
        Version = "v1",
        Description = "RAG-powered document Q&A using Upstash, OpenAI, and Anthropic"
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",  // rag-demo
                "http://localhost:5174")  // rag-monitor
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Configure multipart form-data limits from IngestionOptions
builder.Services.Configure<FormOptions>(options =>
{
    var ingestion = builder.Configuration.GetSection("Ingestion").Get<IngestionOptions>()
        ?? new IngestionOptions();
    var limitBytes = (long)ingestion.MaxFileSizeMb * 1024 * 1024;
    options.MultipartBodyLengthLimit = limitBytes;
});

builder.WebHost.ConfigureKestrel(kestrel =>
{
    var ingestion = builder.Configuration.GetSection("Ingestion").Get<IngestionOptions>()
        ?? new IngestionOptions();
    kestrel.Limits.MaxRequestBodySize = (long)ingestion.MaxFileSizeMb * 1024 * 1024;
});

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.MapScalarApiReference(options =>
    {
        options.Title = "RagQnA API";
        options.Theme = ScalarTheme.Purple;
        options.OpenApiRoutePattern = "/swagger/v1/swagger.json";
    });
}

app.UseAuthorization();
app.MapControllers();

app.Run();
