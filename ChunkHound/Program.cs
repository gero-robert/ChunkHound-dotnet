using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO;
using ChunkHound.Core;
using ChunkHound.Services;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Services.AddSerilog();

// Register services
builder.Services.AddSingleton<IIndexingCoordinator>(sp =>
    new IndexingCoordinator(
        sp.GetRequiredService<IDatabaseProvider>(),
        Path.GetTempPath(),
        sp.GetService<IEmbeddingProvider>(),
        sp.GetService<Dictionary<Language, IUniversalParser>>(),
        null, // chunkCacheService
        null, // config
        sp.GetService<ILogger<IndexingCoordinator>>(),
        null // progress
    ));
builder.Services.AddSingleton<IUniversalParser, UniversalParser>();
builder.Services.AddSingleton<IEmbeddingProvider, EmbeddingProvider>();
builder.Services.AddSingleton<IDatabaseProvider, DatabaseProvider>();
builder.Services.AddSingleton<ILanguageConfigProvider, LanguageConfigProvider>();

var app = builder.Build();

// Get logger and log startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("ChunkHound application started successfully");

// Get indexing coordinator and demonstrate DI
var indexer = app.Services.GetRequiredService<IIndexingCoordinator>();
await indexer.IndexAsync(".");

logger.LogInformation("Application foundation is working");

await app.RunAsync();
