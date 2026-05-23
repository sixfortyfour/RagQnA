using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Knowably.Api.Middleware;
using Knowably.Contracts.Options;
using Knowably.Infrastructure.Extensions;
using Knowably.Ingestion.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure (clients + options)
builder.Services.AddInfrastructure(builder.Configuration);

// Ingestion (chunking + extraction)
builder.Services.AddIngestion();

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Swagger / Scalar
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Knowably API",
        Version = "v1",
        Description = "RAG-powered document Q&A using Upstash and Ollama"
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",  // knowably
                "http://localhost:5174")  // knowably-monitor
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
        options.Title = "Knowably API";
        options.Theme = ScalarTheme.Purple;
        options.OpenApiRoutePattern = "/swagger/v1/swagger.json";
    });
}

app.UseAuthorization();
app.MapControllers();

app.Run();
