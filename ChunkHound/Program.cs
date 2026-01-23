using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using ChunkHound.Core;
using ChunkHound.Services;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Services.AddSerilog();

// Register services
builder.Services.AddSingleton<IIndexingCoordinator, IndexingCoordinator>();
builder.Services.AddSingleton<IUniversalParser, UniversalParser>();
builder.Services.AddSingleton<IEmbeddingProvider, EmbeddingProvider>();
builder.Services.AddSingleton<IDatabaseProvider, DatabaseProvider>();

var app = builder.Build();

// Get logger and log startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("ChunkHound application started successfully");

// Get indexing coordinator and demonstrate DI
var indexer = app.Services.GetRequiredService<IIndexingCoordinator>();
await indexer.IndexAsync(".");

logger.LogInformation("Application foundation is working");

await app.RunAsync();
