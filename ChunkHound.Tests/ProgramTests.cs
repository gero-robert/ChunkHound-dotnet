using Xunit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ChunkHound.Core;
using ChunkHound.Services;
using ChunkHound.Parsers.Concrete;
using System.Threading.Tasks;
using System.IO;

namespace ChunkHound.Tests;

/// <summary>
/// Tests for the Program class, focusing on dependency injection setup and service registration.
/// </summary>
public class ProgramTests
{
    /// <summary>
    /// Tests that the host can be built successfully with all required services registered.
    /// This validates the dependency injection configuration and service wiring.
    /// </summary>
    [Fact]
    public async Task HostBuilder_CanBuildHost_ServicesAreRegistered()
    {
        // Arrange & Act
        var builder = Host.CreateApplicationBuilder();

        // Register services (same as in Program.cs)
        builder.Services.AddSingleton<CSharpParser>();
        builder.Services.AddSingleton<Dictionary<Language, IUniversalParser>>(sp => {
          var dict = new Dictionary<Language, IUniversalParser>();
          dict[Language.CSharp] = sp.GetRequiredService<CSharpParser>();
          return dict;
        });
        builder.Services.AddSingleton<IIndexingCoordinator>(sp =>
            new IndexingCoordinator(
                sp.GetRequiredService<IDatabaseProvider>(),
                Path.GetTempPath(),
                sp.GetService<IEmbeddingProvider>(),
                sp.GetRequiredService<Dictionary<Language, IUniversalParser>>(),
                null, // chunkCacheService
                null, // config
                sp.GetService<ILogger<IndexingCoordinator>>(),
                null // progress
            ));
        builder.Services.AddSingleton<IUniversalParser>(sp => new UniversalParser(sp.GetService<ILogger<UniversalParser>>(), sp.GetRequiredService<ILanguageConfigProvider>(), sp.GetRequiredService<Dictionary<Language, IUniversalParser>>()));
        builder.Services.AddSingleton<IEmbeddingProvider, EmbeddingProvider>();
        builder.Services.AddSingleton<IDatabaseProvider, DatabaseProvider>();
        builder.Services.AddSingleton<ILanguageConfigProvider, LanguageConfigProvider>();

        var app = builder.Build();

        // Assert
        Assert.NotNull(app);

        // Verify that key services can be resolved
        var indexer = app.Services.GetRequiredService<IIndexingCoordinator>();
        Assert.NotNull(indexer);
        Assert.IsType<IndexingCoordinator>(indexer);

        var parser = app.Services.GetRequiredService<IUniversalParser>();
        Assert.NotNull(parser);
        Assert.IsType<UniversalParser>(parser);

        var embedder = app.Services.GetRequiredService<IEmbeddingProvider>();
        Assert.NotNull(embedder);
        Assert.IsType<EmbeddingProvider>(embedder);

        var database = app.Services.GetRequiredService<IDatabaseProvider>();
        Assert.NotNull(database);
        Assert.IsType<DatabaseProvider>(database);

        // Verify logger can be resolved
        var logger = app.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
        Assert.NotNull(logger);

        app.Dispose();
    }

    /// <summary>
    /// Tests that the application can start and execute the main indexing workflow.
    /// This validates the end-to-end application startup and basic functionality.
    /// </summary>
    [Fact]
    public async Task Application_CanStartAndExecute_WithoutThrowing()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Register services with test-friendly implementations
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

        // Act & Assert - Application should start without throwing
        var indexer = app.Services.GetRequiredService<IIndexingCoordinator>();
        var logger = app.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();

        // This simulates the main execution path from Program.cs
        logger.Log(LogLevel.Information, 0, "ChunkHound application started successfully", null, (state, ex) => state.ToString());

        // The indexer.IndexAsync call handles exceptions internally and logs errors
        // For this test, we just ensure the call completes without throwing
        try
        {
            await indexer.IndexAsync("nonexistent-directory");
        }
        catch (System.IO.DirectoryNotFoundException)
        {
            // Expected for nonexistent directory
        }

        logger.Log(LogLevel.Information, 0, "Application foundation is working", null, (state, ex) => state.ToString());

        app.Dispose();
    }

    /// <summary>
    /// Tests that all core services implement their interfaces correctly.
    /// This validates the service implementation contracts.
    /// </summary>
    [Fact]
    public void Services_ImplementInterfacesCorrectly()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton<CSharpParser>();
        builder.Services.AddSingleton<Dictionary<Language, IUniversalParser>>(sp => {
          var dict = new Dictionary<Language, IUniversalParser>();
          dict[Language.CSharp] = sp.GetRequiredService<CSharpParser>();
          return dict;
        });
        builder.Services.AddSingleton<IIndexingCoordinator>(sp =>
            new IndexingCoordinator(
                sp.GetRequiredService<IDatabaseProvider>(),
                Path.GetTempPath(),
                sp.GetService<IEmbeddingProvider>(),
                sp.GetRequiredService<Dictionary<Language, IUniversalParser>>(),
                null, // chunkCacheService
                null, // config
                sp.GetService<ILogger<IndexingCoordinator>>(),
                null // progress
            ));
        builder.Services.AddSingleton<IUniversalParser>(sp => new UniversalParser(sp.GetService<ILogger<UniversalParser>>(), sp.GetRequiredService<ILanguageConfigProvider>(), sp.GetRequiredService<Dictionary<Language, IUniversalParser>>()));
        builder.Services.AddSingleton<IEmbeddingProvider, EmbeddingProvider>();
        builder.Services.AddSingleton<IDatabaseProvider, DatabaseProvider>();
        builder.Services.AddSingleton<ILanguageConfigProvider, LanguageConfigProvider>();

        var app = builder.Build();

        // Act
        var indexer = app.Services.GetRequiredService<IIndexingCoordinator>();
        var parser = app.Services.GetRequiredService<IUniversalParser>();
        var embedder = app.Services.GetRequiredService<IEmbeddingProvider>();
        var database = app.Services.GetRequiredService<IDatabaseProvider>();

        // Assert - Verify interfaces are implemented
        Assert.IsAssignableFrom<IIndexingCoordinator>(indexer);
        Assert.IsAssignableFrom<IUniversalParser>(parser);
        Assert.IsAssignableFrom<IEmbeddingProvider>(embedder);
        Assert.IsAssignableFrom<IDatabaseProvider>(database);

        app.Dispose();
    }
}