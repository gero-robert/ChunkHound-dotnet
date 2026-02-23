using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.IO;
using ChunkHound.Core;
using ChunkHound.Services;
using ChunkHound.Parsers;
using ChunkHound.Parsers.Concrete;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Services.AddSerilog();

// Register services
builder.Services.AddSingleton<CSharpParser>();
builder.Services.AddSingleton<Dictionary<Language, IUniversalParser>>(sp => {
  var dict = new Dictionary<Language, IUniversalParser>();
  dict[Language.CSharp] = sp.GetRequiredService<CSharpParser>();
  return dict;
});
builder.Services.AddSingleton<ChunkCacheService>();
builder.Services.AddSingleton<IndexingConfig>(sp => sp.GetRequiredService<IConfiguration>().GetSection("Indexing").Get<IndexingConfig>() ?? new IndexingConfig());
builder.Services.AddSingleton<IIndexingCoordinator>(sp =>
    new IndexingCoordinator(
        sp.GetRequiredService<IDatabaseProvider>(),
        sp.GetRequiredService<IConfiguration>().GetValue<string>("Indexing:TempPath") ?? System.IO.Path.GetTempPath(),
        sp.GetService<IEmbeddingProvider>(),
        sp.GetRequiredService<Dictionary<Language, IUniversalParser>>(), // match exact dict type from ctor
        sp.GetService<ChunkCacheService>(),
        sp.GetService<IndexingConfig>(),
        sp.GetRequiredService<ILogger<IndexingCoordinator>>(),
        sp.GetService<IProgress<IndexingProgress>>()
    ));
builder.Services.AddSingleton<IUniversalParser>(sp => new UniversalParser(sp.GetRequiredService<ILogger<UniversalParser>>(), sp.GetRequiredService<ILanguageConfigProvider>(), sp.GetRequiredService<Dictionary<Language, IUniversalParser>>()));
builder.Services.AddSingleton<IEmbeddingProvider, EmbeddingProvider>();
builder.Services.AddSingleton<IDatabaseProvider, DatabaseProvider>();
builder.Services.AddSingleton<ILanguageConfigProvider, LanguageConfigProvider>();

// Register parsers
builder.Services.AddSingleton<IParserFactory, ParserFactory>();
builder.Services.AddSingleton<RecursiveChunkSplitter>();
builder.Services.AddSingleton<IChunkParser, RapidYamlParser>();
builder.Services.AddSingleton<IChunkParser, VueChunkParser>();
builder.Services.AddSingleton<IChunkParser, MarkdownParser>();
builder.Services.AddSingleton<IChunkParser, CodeChunkParser>();
builder.Services.AddSingleton<IChunkParser, UniversalTextParser>();

var app = builder.Build();

// Get logger and log startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("ChunkHound application started successfully");

// Get indexing coordinator and demonstrate DI
var indexer = app.Services.GetRequiredService<IIndexingCoordinator>();
await indexer.IndexAsync(".");

logger.LogInformation("Application foundation is working");

await app.RunAsync();
