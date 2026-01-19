# LanceDBProvider Design Document

> **Spec Version:** 1.0 | **Code:** `ChunkHound.Providers.Database.LanceDBProvider` | **Status:** draft

## Overview

The `LanceDBProvider` class implements a vector database provider using LanceDB for ChunkHound's C# indexing flow. This provider handles vector storage, semantic search, fragment optimization, and schema management for efficient code chunk indexing and retrieval.

This design is ported from the Python `LanceDBProvider` in `chunkhound/providers/database/lancedb_provider.py`, adapted for C# with .NET conventions and assuming a .NET LanceDB client library.

## Class Definition

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChunkHound.Core.Models;
using ChunkHound.Core.Types;
using ChunkHound.Providers.Database;
using ChunkHound.Services.Embedding;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Providers.Database
{
    /// <summary>
    /// LanceDB implementation of the database provider for ChunkHound.
    /// Optimized for performance using native LanceDB queries instead of DataFrame filtering
    /// wherever possible to reduce memory usage and improve query performance for large datasets.
    /// </summary>
    public class LanceDBProvider : SerialDatabaseProvider
    {
        // Properties and methods defined below
    }
}
```

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `IndexType` | `string?` | Type of vector index (e.g., "ivf_hnsw_sq", "ivf_rq") |
| `FragmentThreshold` | `int` | Threshold for fragment optimization (default: 100) |
| `Connection` | `object?` | LanceDB connection object (for backward compatibility) |
| `FilesTable` | `object?` | Reference to the files table |
| `ChunksTable` | `object?` | Reference to the chunks table |

## Constructor

```csharp
/// <summary>
/// Initializes a new instance of the LanceDBProvider.
/// </summary>
/// <param name="dbPath">Path to LanceDB database directory</param>
/// <param name="baseDirectory">Base directory for path normalization</param>
/// <param name="embeddingManager">Optional embedding manager for vector generation</param>
/// <param name="config">Database configuration for provider-specific settings</param>
/// <param name="logger">Logger instance for diagnostics</param>
public LanceDBProvider(
    string dbPath,
    string baseDirectory,
    IEmbeddingManager? embeddingManager = null,
    DatabaseConfig? config = null,
    ILogger<LanceDBProvider>? logger = null)
    : base(dbPath, baseDirectory, embeddingManager, config)
{
    IndexType = config?.LanceDBIndexType;
    FragmentThreshold = config?.LanceDBOptimizeFragmentThreshold ?? 100;
    Connection = null;
    FilesTable = null;
    ChunksTable = null;
    _logger = logger ?? LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<LanceDBProvider>();
}
```

## Schema Management

### Schema Definitions

```csharp
/// <summary>
/// Gets the PyArrow schema for the files table.
/// </summary>
private static Schema GetFilesSchema() => new Schema(new Field[]
{
    new Field("id", DataType.Int64, false),
    new Field("path", DataType.String, false),
    new Field("size", DataType.Int64, false),
    new Field("modified_time", DataType.Float64, false),
    new Field("content_hash", DataType.String, true),
    new Field("indexed_time", DataType.Float64, false),
    new Field("language", DataType.String, false),
    new Field("encoding", DataType.String, true),
    new Field("line_count", DataType.Int64, false)
});

/// <summary>
/// Gets the PyArrow schema for the chunks table.
/// </summary>
/// <param name="embeddingDims">Number of dimensions for embedding vectors</param>
private static Schema GetChunksSchema(int? embeddingDims = null)
{
    var embeddingField = embeddingDims.HasValue
        ? new Field("embedding", new FixedSizeList(DataType.Float32, embeddingDims.Value), true)
        : new Field("embedding", new List(DataType.Float32), true);

    return new Schema(new Field[]
    {
        new Field("id", DataType.Int64, false),
        new Field("file_id", DataType.Int64, false),
        new Field("content", DataType.String, false),
        new Field("start_line", DataType.Int64, false),
        new Field("end_line", DataType.Int64, false),
        new Field("chunk_type", DataType.String, false),
        new Field("language", DataType.String, false),
        new Field("name", DataType.String, true),
        embeddingField,
        new Field("provider", DataType.String, true),
        new Field("model", DataType.String, true),
        new Field("embedding_signature", DataType.String, true),
        new Field("embedding_status", DataType.String, true),
        new Field("created_time", DataType.Float64, false)
    });
}
```

### Schema Operations

```csharp
/// <summary>
/// Creates database schema for files, chunks, and embeddings.
/// </summary>
public override async Task CreateSchemaAsync()
{
    await ExecuteInDbThreadAsync("create_schema");
}

/// <summary>
/// Executor method for create_schema - runs in DB thread.
/// </summary>
private async Task ExecutorCreateSchemaAsync(object connection, Dictionary<string, object> state)
{
    // Create files table if it doesn't exist
    try
    {
        FilesTable = await connection.OpenTableAsync("files");
    }
    catch
    {
        FilesTable = await connection.CreateTableAsync("files", GetFilesSchema());
        _logger.LogInformation("Created files table");
    }

    // Create chunks table if it doesn't exist
    try
    {
        ChunksTable = await connection.OpenTableAsync("chunks");
        _logger.LogDebug("Opened existing chunks table");

        // Try to add missing columns (migration)
        await TryAddEmbeddingColumnsAsync(ChunksTable);
    }
    catch
    {
        var embeddingDims = await GetEmbeddingDimensionsSafeAsync();
        ChunksTable = await connection.CreateTableAsync("chunks", GetChunksSchema(embeddingDims));
        _logger.LogInformation("Created chunks table");
    }
}
```

## Vector DB Storage Methods

### Embedding Operations

```csharp
/// <summary>
/// Inserts multiple embedding vectors efficiently using merge operations.
/// </summary>
public override async Task<int> InsertEmbeddingsBatchAsync(
    List<Dictionary<string, object>> embeddingsData,
    List<Dictionary<string, object>> chunksData)
{
    return await ExecuteInDbThreadAsync<int>("insert_embeddings_batch", embeddingsData, chunksData);
}

/// <summary>
/// Executor method for insert_embeddings_batch - runs in DB thread.
/// </summary>
private async Task<int> ExecutorInsertEmbeddingsBatchAsync(
    object connection,
    Dictionary<string, object> state,
    List<Dictionary<string, object>> embeddingsData,
    List<Dictionary<string, object>> chunksData)
{
    if (chunksData == null || chunksData.Count == 0 || ChunksTable == null)
        return 0;

    // Determine embedding dimensions and prepare data
    var embeddingDims = embeddingsData?.Count > 0
        ? GetEmbeddingDimensions(embeddingsData[0])
        : (int?)null;

    // Ensure schema compatibility
    await EnsureEmbeddingSchemaCompatibilityAsync(ChunksTable, embeddingDims);

    // Prepare merge data
    var mergeData = PrepareMergeData(embeddingsData, chunksData, embeddingDims);

    // Perform batch insert with merge
    var table = CreateTableFromData(mergeData, GetChunksSchema(embeddingDims));
    await ChunksTable.MergeInsertAsync("id")
        .WhenMatchedUpdateAll()
        .WhenNotMatchedInsertAll()
        .ExecuteAsync(table);

    // Create vector index if needed
    if (embeddingsData?.Count > 0)
    {
        await CreateVectorIndexIfNeededAsync(connection, state);
    }

    return mergeData.Count;
}
```

### Search Operations

```csharp
/// <summary>
/// Performs semantic search using vector similarity.
/// </summary>
public override async Task<(List<Dictionary<string, object>>, Dictionary<string, object>)> SearchSemanticAsync(
    List<float> queryEmbedding,
    string provider,
    string model,
    int pageSize = 10,
    int offset = 0,
    float? threshold = null,
    string? pathFilter = null)
{
    return await ExecuteInDbThreadAsync<(List<Dictionary<string, object>>, Dictionary<string, object>)>(
        "search_semantic", queryEmbedding, provider, model, pageSize, offset, threshold, pathFilter);
}

/// <summary>
/// Executor method for search_semantic - runs in DB thread.
/// </summary>
private async Task<(List<Dictionary<string, object>>, Dictionary<string, object>)> ExecutorSearchSemanticAsync(
    object connection,
    Dictionary<string, object> state,
    List<float> queryEmbedding,
    string provider,
    string model,
    int pageSize,
    int offset,
    float? threshold,
    string? pathFilter)
{
    if (ChunksTable == null)
        throw new InvalidOperationException("Chunks table not initialized");

    // Validate embeddings exist
    var totalChunks = await ChunksTable.CountRowsAsync();
    if (totalChunks == 0)
        return (new List<Dictionary<string, object>>(), CreatePaginationInfo(offset, 0, false, 0));

    // Perform vector search
    var query = ChunksTable.Search(queryEmbedding, "embedding")
        .Where($"provider = '{provider}' AND model = '{model}' AND embedding IS NOT NULL")
        .Limit(pageSize)
        .Offset(offset);

    if (threshold.HasValue)
        query = query.Where($"_distance <= {threshold.Value}");

    var results = await query.ToListAsync();

    // Process results
    var formattedResults = new List<Dictionary<string, object>>();
    foreach (var result in results)
    {
        var filePath = await GetFilePathAsync(result["file_id"]);
        var similarity = 1.0f - (result.GetValueOrDefault("_distance", 0.0f) as float? ?? 0.0f);

        formattedResults.Add(new Dictionary<string, object>
        {
            ["chunk_id"] = result["id"],
            ["symbol"] = result.GetValueOrDefault("name", ""),
            ["content"] = result.GetValueOrDefault("content", ""),
            ["chunk_type"] = result.GetValueOrDefault("chunk_type", ""),
            ["start_line"] = result.GetValueOrDefault("start_line", 0),
            ["end_line"] = result.GetValueOrDefault("end_line", 0),
            ["file_path"] = filePath,
            ["language"] = result.GetValueOrDefault("language", ""),
            ["similarity"] = similarity
        });
    }

    var pagination = new Dictionary<string, object>
    {
        ["offset"] = offset,
        ["page_size"] = formattedResults.Count,
        ["has_more"] = results.Count > offset + pageSize,
        ["total"] = results.Count
    };

    return (formattedResults, pagination);
}
```

## Fragment Optimization

### Fragment Management

```csharp
/// <summary>
/// Gets current fragment counts for chunks and files tables.
/// </summary>
public async Task<Dictionary<string, int>> GetFragmentCountAsync()
{
    return await ExecuteInDbThreadAsync<Dictionary<string, int>>("get_fragment_count");
}

/// <summary>
/// Executor method for get_fragment_count - runs in DB thread.
/// </summary>
private async Task<Dictionary<string, int>> ExecutorGetFragmentCountAsync(
    object connection, Dictionary<string, object> state)
{
    var result = new Dictionary<string, int>();

    if (ChunksTable != null)
    {
        try
        {
            var stats = await ChunksTable.GetStatsAsync();
            result["chunks"] = ExtractFragmentCount(stats);
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Could not get chunks fragment count: {ex.Message}");
            result["chunks"] = 0;
        }
    }

    if (FilesTable != null)
    {
        try
        {
            var stats = await FilesTable.GetStatsAsync();
            result["files"] = ExtractFragmentCount(stats);
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Could not get files fragment count: {ex.Message}");
            result["files"] = 0;
        }
    }

    return result;
}

/// <summary>
/// Checks if fragment-based optimization is warranted.
/// </summary>
public bool ShouldOptimizeFragments(int? threshold = null, string operation = "")
{
    try
    {
        var effectiveThreshold = threshold ?? FragmentThreshold;
        var chunksFragments = GetChunksFragmentCount();

        if (chunksFragments < effectiveThreshold)
        {
            _logger.LogDebug($"Skipping {operation} optimization: {chunksFragments} fragments < threshold {effectiveThreshold}");
            return false;
        }
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogDebug($"Could not check fragment count, will optimize: {ex.Message}");
        return true;
    }
}

/// <summary>
/// Optimizes tables by compacting fragments and rebuilding indexes.
/// </summary>
public override async Task OptimizeTablesAsync()
{
    // Use higher timeout for optimization operations
    var originalTimeout = Environment.GetEnvironmentVariable("CHUNKHOUND_DB_EXECUTE_TIMEOUT");
    try
    {
        Environment.SetEnvironmentVariable("CHUNKHOUND_DB_EXECUTE_TIMEOUT", "600"); // 10 minutes
        await ExecuteInDbThreadAsync("optimize_tables");
    }
    finally
    {
        Environment.SetEnvironmentVariable("CHUNKHOUND_DB_EXECUTE_TIMEOUT", originalTimeout);
    }
}

/// <summary>
/// Executor method for optimize_tables - runs in DB thread.
/// </summary>
private async Task ExecutorOptimizeTablesAsync(object connection, Dictionary<string, object> state)
{
    var initialCounts = await ExecutorGetFragmentCountAsync(connection, state);
    _logger.LogDebug($"Initial fragment counts: chunks={initialCounts.GetValueOrDefault("chunks", 0)}, files={initialCounts.GetValueOrDefault("files", 0)}");

    // Perform optimization
    if (ChunksTable != null)
    {
        _logger.LogDebug("Optimizing chunks table - compacting fragments...");
        await ChunksTable.OptimizeAsync(deleteUnverified: true);
        _logger.LogDebug("Chunks table optimization complete");
    }

    if (FilesTable != null)
    {
        _logger.LogDebug("Optimizing files table - compacting fragments...");
        await FilesTable.OptimizeAsync(deleteUnverified: true);
        _logger.LogDebug("Files table optimization complete");
    }

    var finalCounts = await ExecutorGetFragmentCountAsync(connection, state);
    var chunksReduction = initialCounts.GetValueOrDefault("chunks", 0) - finalCounts.GetValueOrDefault("chunks", 0);
    var filesReduction = initialCounts.GetValueOrDefault("files", 0) - finalCounts.GetValueOrDefault("files", 0);

    _logger.LogInformation($"Fragment reduction: chunks={chunksReduction}, files={filesReduction}");

    if (finalCounts.GetValueOrDefault("chunks", 0) > 0 || finalCounts.GetValueOrDefault("files", 0) > 0)
    {
        _logger.LogWarning("Fragments remain after optimization. This may affect embedding deduplication.");
    }
}
```

## Index Management

```csharp
/// <summary>
/// Creates database indexes for performance optimization.
/// </summary>
public override async Task CreateIndexesAsync()
{
    await ExecuteInDbThreadAsync("create_indexes");
}

/// <summary>
/// Executor method for create_indexes - runs in DB thread.
/// </summary>
private async Task ExecutorCreateIndexesAsync(object connection, Dictionary<string, object> state)
{
    if (ChunksTable == null) return;

    // Create scalar indexes
    await CreateScalarIndexIfNotExistsAsync(ChunksTable, "id");
    await CreateScalarIndexIfNotExistsAsync(ChunksTable, "embedding_signature");
    await CreateScalarIndexIfNotExistsAsync(ChunksTable, "embedding_status");
}

/// <summary>
/// Creates vector index for specific provider/model/dims combination.
/// </summary>
public async Task CreateVectorIndexAsync(string provider, string model, int dims, string metric = "cosine")
{
    await ExecuteInDbThreadAsync("create_vector_index", provider, model, dims, metric);
}

/// <summary>
/// Executor method for create_vector_index - runs in DB thread.
/// </summary>
private async Task ExecutorCreateVectorIndexAsync(
    object connection,
    Dictionary<string, object> state,
    string provider,
    string model,
    int dims,
    string metric)
{
    if (ChunksTable == null) return;

    // Check if sufficient data exists
    var embeddingCount = await GetEmbeddingsCountAsync(provider, model);
    if (embeddingCount < 1000)
    {
        _logger.LogDebug($"Skipping index creation: insufficient data ({embeddingCount} < 1000)");
        return;
    }

    // Create vector index
    await ChunksTable.CreateIndexAsync(
        vectorColumnName: "embedding",
        indexType: IndexType ?? "auto",
        metric: metric);
}
```

## Testing Stubs

### Unit Tests

```csharp
using Xunit;
using Moq;
using ChunkHound.Providers.Database;
using ChunkHound.Core.Models;
using ChunkHound.Services.Embedding;

namespace ChunkHound.Providers.Database.Tests
{
    public class LanceDBProviderTests
    {
        private readonly Mock<IEmbeddingManager> _embeddingManagerMock;
        private readonly Mock<ILogger<LanceDBProvider>> _loggerMock;

        public LanceDBProviderTests()
        {
            _embeddingManagerMock = new Mock<IEmbeddingManager>();
            _loggerMock = new Mock<ILogger<LanceDBProvider>>();
        }

        [Fact]
        public async Task CreateSchemaAsync_WhenCalled_CreatesTables()
        {
            // Arrange
            var provider = new LanceDBProvider(
                "test.db",
                "/base",
                _embeddingManagerMock.Object,
                null,
                _loggerMock.Object);

            // Act
            await provider.CreateSchemaAsync();

            // Assert
            // Verify tables are created
            Assert.NotNull(provider.FilesTable);
            Assert.NotNull(provider.ChunksTable);
        }

        [Fact]
        public async Task InsertEmbeddingsBatchAsync_WithValidData_InsertsSuccessfully()
        {
            // Arrange
            var provider = new LanceDBProvider(
                "test.db",
                "/base",
                _embeddingManagerMock.Object,
                null,
                _loggerMock.Object);

            var embeddingsData = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["chunk_id"] = 1,
                    ["embedding"] = new List<float> { 0.1f, 0.2f, 0.3f },
                    ["provider"] = "test",
                    ["model"] = "test-model",
                    ["status"] = "success"
                }
            };

            var chunksData = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["id"] = 1,
                    ["file_id"] = 1,
                    ["content"] = "test content",
                    ["start_line"] = 1,
                    ["end_line"] = 5,
                    ["chunk_type"] = "function",
                    ["language"] = "csharp",
                    ["name"] = "TestFunction",
                    ["created_time"] = DateTimeOffset.Now.ToUnixTimeSeconds()
                }
            };

            // Act
            var result = await provider.InsertEmbeddingsBatchAsync(embeddingsData, chunksData);

            // Assert
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task SearchSemanticAsync_WithValidQuery_ReturnsResults()
        {
            // Arrange
            var provider = new LanceDBProvider(
                "test.db",
                "/base",
                _embeddingManagerMock.Object,
                null,
                _loggerMock.Object);

            var queryEmbedding = new List<float> { 0.1f, 0.2f, 0.3f };

            // Act
            var (results, pagination) = await provider.SearchSemanticAsync(
                queryEmbedding, "test", "test-model", 10, 0);

            // Assert
            Assert.NotNull(results);
            Assert.NotNull(pagination);
        }

        [Fact]
        public async Task GetFragmentCountAsync_WhenCalled_ReturnsCounts()
        {
            // Arrange
            var provider = new LanceDBProvider(
                "test.db",
                "/base",
                _embeddingManagerMock.Object,
                null,
                _loggerMock.Object);

            // Act
            var counts = await provider.GetFragmentCountAsync();

            // Assert
            Assert.Contains("chunks", counts.Keys);
            Assert.Contains("files", counts.Keys);
        }

        [Fact]
        public async Task OptimizeTablesAsync_WhenCalled_OptimizesSuccessfully()
        {
            // Arrange
            var provider = new LanceDBProvider(
                "test.db",
                "/base",
                _embeddingManagerMock.Object,
                null,
                _loggerMock.Object);

            // Act
            await provider.OptimizeTablesAsync();

            // Assert
            // Verify optimization completed without errors
        }
    }
}
```

### Integration Tests

```csharp
using Xunit;
using ChunkHound.Providers.Database;
using ChunkHound.Core.Models;

namespace ChunkHound.Providers.Database.IntegrationTests
{
    public class LanceDBProviderIntegrationTests : IDisposable
    {
        private readonly LanceDBProvider _provider;
        private readonly string _testDbPath;

        public LanceDBProviderIntegrationTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_lancedb_{Guid.NewGuid()}");
            _provider = new LanceDBProvider(_testDbPath, "/base");
        }

        [Fact]
        public async Task FullWorkflow_InsertAndSearch_WorksCorrectly()
        {
            // Arrange
            await _provider.ConnectAsync();
            await _provider.CreateSchemaAsync();

            var file = new File(1, "test.cs", 100, DateTime.Now, Language.CSharp);
            await _provider.InsertFileAsync(file);

            var chunk = new Chunk(
                "TestMethod",
                1, 10,
                "public void Test() { }",
                ChunkType.Method,
                1,
                Language.CSharp);

            var chunkId = await _provider.InsertChunkAsync(chunk);

            var embeddingsData = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["chunk_id"] = chunkId,
                    ["embedding"] = new List<float> { 0.1f, 0.2f, 0.3f },
                    ["provider"] = "test",
                    ["model"] = "test-model",
                    ["status"] = "success"
                }
            };

            var chunksData = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["id"] = chunkId,
                    ["file_id"] = 1,
                    ["content"] = chunk.Code,
                    ["start_line"] = chunk.StartLine,
                    ["end_line"] = chunk.EndLine,
                    ["chunk_type"] = chunk.ChunkType.Value,
                    ["language"] = chunk.Language.Value,
                    ["name"] = chunk.Symbol,
                    ["created_time"] = DateTimeOffset.Now.ToUnixTimeSeconds()
                }
            };

            // Act
            await _provider.InsertEmbeddingsBatchAsync(embeddingsData, chunksData);

            var queryEmbedding = new List<float> { 0.1f, 0.2f, 0.3f };
            var (results, pagination) = await _provider.SearchSemanticAsync(
                queryEmbedding, "test", "test-model", 10, 0);

            // Assert
            Assert.NotEmpty(results);
            Assert.Equal(chunkId, results[0]["chunk_id"]);
        }

        public void Dispose()
        {
            _provider.DisconnectAsync().Wait();
            if (Directory.Exists(_testDbPath))
            {
                Directory.Delete(_testDbPath, true);
            }
        }
    }
}
```

## Dependencies

- `ChunkHound.Core.Models.Chunk`
- `ChunkHound.Core.Models.File`
- `ChunkHound.Core.Types.Language`
- `ChunkHound.Core.Types.ChunkType`
- `ChunkHound.Providers.Database.SerialDatabaseProvider`
- `ChunkHound.Services.Embedding.IEmbeddingManager`
- LanceDB .NET client library (assumed)
- PyArrow .NET bindings (assumed)

## Notes

- This provider uses a serial executor pattern for thread safety, similar to the Python implementation.
- Fragment optimization is crucial for maintaining search performance as the database grows.
- Vector indexes are created automatically when sufficient embedding data is available.
- Schema migrations handle the transition from variable-size to fixed-size embedding columns.
- All major query operations avoid loading entire tables into memory for scalability.