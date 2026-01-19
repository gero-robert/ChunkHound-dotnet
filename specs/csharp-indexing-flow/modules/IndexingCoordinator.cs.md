# IndexingCoordinator Service Design Document

> **Spec Version:** 1.0 | **Code:** `ChunkHound.Services.IndexingCoordinator` | **Status:** draft

## Overview

The `IndexingCoordinator` class is the central orchestrator for the ChunkHound indexing pipeline in C#. This service coordinates the discovery, parsing, chunking, embedding generation, and storage of source code files with parallelism and change detection. All chunks get embedded during initial indexing in a streamlined Parse → Embed → Store pipeline. It implements async patterns throughout for scalability and uses concurrent data structures for thread-safe operations.

This design is ported from the Python `IndexingCoordinator` in `chunkhound/services/indexing_coordinator.py`, adapted for C# with .NET async/await patterns, dependency injection, and concurrent collections.

## Class Definition

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core.Models;
using ChunkHound.Interfaces;
using ChunkHound.Parsers;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Services
{
    /// <summary>
    /// Orchestrates file indexing workflows with parsing, chunking, and embeddings.
    /// This service coordinates the streamlined indexing process: discovery→parse→embed→store
    /// where all chunks get embedded during initial indexing without separate missing embedding queries.
    /// </summary>
    public class IndexingCoordinator : IIndexingCoordinator
    {
        // Properties and methods defined below
    }
}
```

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `DatabaseProvider` | `IDatabaseProvider` | Database provider for persistence operations |
| `EmbeddingProvider` | `IEmbeddingProvider?` | Optional embedding provider for vector generation |
| `LanguageParsers` | `Dictionary<Language, IUniversalParser>` | Mapping of language to parser implementations |
| `BaseDirectory` | `string` | Base directory for path normalization |
| `Config` | `IndexingConfig?` | Optional configuration for indexing settings |
| `Logger` | `ILogger<IndexingCoordinator>` | Logger for diagnostic information |
| `Progress` | `IProgress<IndexingProgress>?` | Optional progress reporting interface |

## Constructor and Dependencies

```csharp
/// <summary>
/// Initializes a new instance of the IndexingCoordinator class.
/// </summary>
public IndexingCoordinator(
    IDatabaseProvider databaseProvider,
    string baseDirectory,
    IEmbeddingProvider? embeddingProvider = null,
    Dictionary<Language, IUniversalParser>? languageParsers = null,
    IndexingConfig? config = null,
    ILogger<IndexingCoordinator>? logger = null,
    IProgress<IndexingProgress>? progress = null)
{
    DatabaseProvider = databaseProvider ?? throw new ArgumentNullException(nameof(databaseProvider));
    BaseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
    EmbeddingProvider = embeddingProvider;
    LanguageParsers = languageParsers ?? new Dictionary<Language, IUniversalParser>();
    Config = config;
    Logger = logger ?? NullLogger<IndexingCoordinator>.Instance;
    Progress = progress;

    // Initialize concurrent data structures
    _filesQueue = new ConcurrentQueue<string>();
    _chunksQueue = new ConcurrentQueue<Chunk>();
    _embedChunksQueue = new ConcurrentQueue<EmbedChunk>();

    // Initialize synchronization primitives
    _dbLock = new ReaderWriterLockSlim();
    _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

    // Initialize worker cancellation
    _cancellationTokenSource = new CancellationTokenSource();
}

// Private fields
private readonly ConcurrentQueue<string> _filesQueue;
private readonly ConcurrentQueue<Chunk> _chunksQueue;
private readonly ConcurrentQueue<EmbedChunk> _embedChunksQueue;
private readonly ReaderWriterLockSlim _dbLock;
private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks;
private readonly CancellationTokenSource _cancellationTokenSource;
```

## Core Methods

### ProcessDirectoryAsync

```csharp
/// <summary>
/// Processes all supported files in a directory with batch optimization and consistency checks.
/// </summary>
public async Task<IndexingResult> ProcessDirectoryAsync(
    string directory,
    List<string>? patterns = null,
    List<string>? excludePatterns = null,
    CancellationToken cancellationToken = default)
{
    Logger.LogInformation("Starting directory processing for {Directory}", directory);

    try
    {
        // Phase 1: Discovery
        var files = await DiscoverFilesAsync(directory, patterns, excludePatterns, cancellationToken);

        if (!files.Any())
        {
            return new IndexingResult { Status = IndexingStatus.NoFiles };
        }

        // Phase 2: Change detection and filtering
        var filesToProcess = await FilterChangedFilesAsync(files, cancellationToken);

        // Phase 3: Parallel processing pipeline
        var result = await ProcessFilesPipelineAsync(filesToProcess, cancellationToken);

        Logger.LogInformation("Directory processing completed: {FilesProcessed} files, {ChunksStored} chunks",
            result.FilesProcessed, result.TotalChunks);

        return result;
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to process directory {Directory}", directory);
        return new IndexingResult { Status = IndexingStatus.Error, Error = ex.Message };
    }
}
```

### ParseFileAsync

```csharp
/// <summary>
/// Parses a single file and returns chunks (used by worker pipeline).
/// </summary>
private async Task<List<Chunk>> ParseFileAsync(
    string filePath,
    CancellationToken cancellationToken = default)
{
    // Detect language
    var language = await DetectFileLanguageAsync(filePath);
    if (language == Language.Unknown)
    {
        return new List<Chunk>();
    }

    // Get parser
    if (!LanguageParsers.TryGetValue(language, out var parser))
    {
        return new List<Chunk>();
    }

    // Get or create fileId for parsing
    var relativePath = Path.GetRelativePath(BaseDirectory, filePath);
    var existingFile = await DatabaseProvider.GetFileByPathAsync(relativePath, cancellationToken);
    var fileId = existingFile?.Id ?? 0;

    // Parse file
    return await parser.ParseFileAsync(filePath, fileId, cancellationToken);
}
```

### ProcessFileAsync

```csharp
/// <summary>
/// Processes a single file through the parsing and chunking pipeline.
/// </summary>
public async Task<FileProcessingResult> ProcessFileAsync(
    string filePath,
    CancellationToken cancellationToken = default)
{
    Logger.LogInformation("Processing single file {FilePath}", filePath);

    // File-level locking to prevent concurrent processing
    var fileLock = await GetFileLockAsync(filePath);
    await fileLock.WaitAsync(cancellationToken);

    try
    {
        // Detect language
        var language = await DetectFileLanguageAsync(filePath);
        if (language == Language.Unknown)
        {
            return new FileProcessingResult { Status = FileProcessingStatus.UnsupportedLanguage };
        }

        // Get parser
        if (!LanguageParsers.TryGetValue(language, out var parser))
        {
            return new FileProcessingResult { Status = FileProcessingStatus.NoParser };
        }

        // Get or create fileId for parsing
        var relativePath = Path.GetRelativePath(BaseDirectory, filePath);
        var existingFile = await DatabaseProvider.GetFileByPathAsync(relativePath, cancellationToken);
        var fileId = existingFile?.Id ?? 0;

        // Parse file
        var chunks = await parser.ParseFileAsync(filePath, fileId, cancellationToken);
        if (!chunks.Any())
        {
            return new FileProcessingResult { Status = FileProcessingStatus.NoChunks };
        }

        // Store chunks with diffing
        var storeResult = await StoreParsedChunksAsync(filePath, chunks, cancellationToken);

        return new FileProcessingResult
        {
            Status = FileProcessingStatus.Success,
            ChunksProcessed = chunks.Count,
            ChunksStored = storeResult.ChunksStored,
            FileId = storeResult.FileId
        };
    }
    finally
    {
        fileLock.Release();
    }
}
```



## Async Patterns and Concurrency

### Task-Based Parallelism

```csharp
/// <summary>
/// Processes files using a producer-consumer pipeline with async workers.
/// </summary>
private async Task<IndexingResult> ProcessFilesPipelineAsync(
    List<string> files,
    CancellationToken cancellationToken)
{
    // Enqueue all files
    foreach (var file in files)
    {
        _filesQueue.Enqueue(file);
    }

    // Start worker tasks
    var parseWorkers = Enumerable.Range(0, Config?.ParseWorkers ?? 4)
        .Select(_ => Task.Run(() => ParseWorkerAsync(cancellationToken)))
        .ToArray();

    var embedWorkers = Enumerable.Range(0, Config?.EmbedWorkers ?? 2)
        .Select(_ => Task.Run(() => EmbedWorkerAsync(cancellationToken)))
        .ToArray();

    var storeWorkers = Enumerable.Range(0, Config?.StoreWorkers ?? 2)
        .Select(_ => Task.Run(() => StoreWorkerAsync(cancellationToken)))
        .ToArray();

    // Wait for all workers to complete
    await Task.WhenAll(parseWorkers.Concat(embedWorkers).Concat(storeWorkers));

    // Collect results
    return await CollectPipelineResultsAsync();
}
```

### File-Level Locking

```csharp
/// <summary>
/// Gets or creates a semaphore for file-level locking.
/// </summary>
private async Task<SemaphoreSlim> GetFileLockAsync(string filePath)
{
    var key = Path.GetFullPath(filePath);
    return _fileLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
}
```

### Database Synchronization

```csharp
/// <summary>
/// Executes database operations with appropriate locking.
/// </summary>
private async Task<T> ExecuteWithLockAsync<T>(
    Func<Task<T>> operation,
    bool writeLock = false,
    CancellationToken cancellationToken = default)
{
    if (writeLock)
    {
        _dbLock.EnterWriteLock();
        try
        {
            return await operation();
        }
        finally
        {
            _dbLock.ExitWriteLock();
        }
    }
    else
    {
        _dbLock.EnterReadLock();
        try
        {
            return await operation();
        }
        finally
        {
            _dbLock.ExitReadLock();
        }
    }
}
```

## Worker Methods

### Parse Worker

```csharp
/// <summary>
/// Worker that dequeues files, parses them, and enqueues chunks.
/// </summary>
private async Task ParseWorkerAsync(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        if (!_filesQueue.TryDequeue(out var filePath))
        {
            await Task.Delay(10, cancellationToken); // Small delay to prevent busy waiting
            continue;
        }

        try
        {
            var chunks = await ParseFileAsync(filePath, cancellationToken);
            foreach (var chunk in chunks)
            {
                _chunksQueue.Enqueue(chunk);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Parse worker failed for file {FilePath}", filePath);
        }
    }
}
```

### Embed Worker

```csharp
/// <summary>
/// Worker that dequeues chunks, generates embeddings, and enqueues embed chunks.
/// </summary>
private async Task EmbedWorkerAsync(CancellationToken cancellationToken)
{
    var batch = new List<Chunk>();
    var batchSize = Config?.EmbeddingBatchSize ?? 100;

    while (!cancellationToken.IsCancellationRequested)
    {
        // Collect batch
        while (batch.Count < batchSize && _chunksQueue.TryDequeue(out var chunk))
        {
            batch.Add(chunk);
        }

        if (batch.Any())
        {
            try
            {
                var texts = batch.Select(c => c.Code).ToList();
                var embeddings = await EmbeddingProvider!.EmbedAsync(texts, cancellationToken);

                for (var i = 0; i < batch.Count; i++)
                {
                    var embedChunk = new EmbedChunk(batch[i], embeddings[i]);
                    _embedChunksQueue.Enqueue(embedChunk);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Embed worker failed for batch of {Count} chunks", batch.Count);
            }
            finally
            {
                batch.Clear();
            }
        }
        else
        {
            await Task.Delay(10, cancellationToken);
        }
    }
}
```

### Store Worker

```csharp
/// <summary>
/// Worker that dequeues embed chunks and stores them in the database.
/// </summary>
private async Task StoreWorkerAsync(CancellationToken cancellationToken)
{
    var batch = new List<EmbedChunk>();
    var batchSize = Config?.DatabaseBatchSize ?? 1000;

    while (!cancellationToken.IsCancellationRequested)
    {
        // Collect batch
        while (batch.Count < batchSize && _embedChunksQueue.TryDequeue(out var embedChunk))
        {
            batch.Add(embedChunk);
        }

        if (batch.Any())
        {
            try
            {
                await ExecuteWithLockAsync(async () =>
                {
                    var chunks = batch.Select(ec => ec.Chunk).ToList();
                    var embeddings = batch.Select(ec => ec.Embedding).ToList();

                    var chunkIds = await DatabaseProvider.InsertChunksBatchAsync(chunks, cancellationToken);

                    // Associate embeddings with chunk IDs
                    await DatabaseProvider.InsertEmbeddingsBatchAsync(chunkIds, embeddings, cancellationToken);

                    return chunkIds.Count;
                }, writeLock: true, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Store worker failed for batch of {Count} chunks", batch.Count);
            }
            finally
            {
                batch.Clear();
            }
        }
        else
        {
            await Task.Delay(10, cancellationToken);
        }
    }
}
```

## Helper Methods

### DiscoverFilesAsync

```csharp
/// <summary>
/// Discovers files in directory matching patterns with efficient exclude filtering.
/// </summary>
private async Task<List<string>> DiscoverFilesAsync(
    string directory,
    List<string>? patterns,
    List<string>? excludePatterns,
    CancellationToken cancellationToken)
{
    // Implementation using parallel directory traversal
    // Similar to Python _discover_files_parallel but with async patterns
    var files = new ConcurrentBag<string>();

    // Use Task.Run for CPU-bound directory traversal
    await Task.Run(() =>
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (ShouldIncludeFile(file, patterns, excludePatterns))
            {
                files.Add(file);
            }
        }
    }, cancellationToken);

    return files.ToList();
}
```

### FilterChangedFilesAsync

```csharp
/// <summary>
/// Filters files to only those that have changed since last indexing.
/// </summary>
private async Task<List<string>> FilterChangedFilesAsync(
    List<string> files,
    CancellationToken cancellationToken)
{
    var changedFiles = new List<string>();

    foreach (var file in files)
    {
        if (cancellationToken.IsCancellationRequested) break;

        if (await HasFileChangedAsync(file, cancellationToken))
        {
            changedFiles.Add(file);
        }
    }

    return changedFiles;
}
```

## Testing Stubs

### Unit Tests

```csharp
using Xunit;
using Moq;
using ChunkHound.Services;
using ChunkHound.Interfaces;
using ChunkHound.Core.Models;
using System.Threading.Tasks;

namespace ChunkHound.Services.Tests
{
    public class IndexingCoordinatorTests
    {
        private readonly Mock<IDatabaseProvider> _mockDb;
        private readonly Mock<IEmbeddingProvider> _mockEmbed;
        private readonly IndexingCoordinator _coordinator;

        public IndexingCoordinatorTests()
        {
            _mockDb = new Mock<IDatabaseProvider>();
            _mockEmbed = new Mock<IEmbeddingProvider>();
            _coordinator = new IndexingCoordinator(_mockDb.Object, "/test", _mockEmbed.Object);
        }

        [Fact]
        public async Task ProcessFileAsync_ValidFile_ReturnsSuccess()
        {
            // Arrange
            var filePath = "/test/main.cs";
            _mockDb.Setup(db => db.GetFileByPathAsync(It.IsAny<string>()))
                   .ReturnsAsync((File?)null);

            // Act
            var result = await _coordinator.ProcessFileAsync(filePath);

            // Assert
            Assert.Equal(FileProcessingStatus.Success, result.Status);
            Assert.True(result.ChunksProcessed > 0);
        }

        [Fact]
        public async Task ProcessDirectoryAsync_NoFiles_ReturnsNoFiles()
        {
            // Arrange
            var directory = "/empty";

            // Act
            var result = await _coordinator.ProcessDirectoryAsync(directory);

            // Assert
            Assert.Equal(IndexingStatus.NoFiles, result.Status);
        }


    }
}
```

### Integration Tests

```csharp
using Xunit;
using ChunkHound.Services;
using ChunkHound.Providers.Database;
using System.IO;
using System.Threading.Tasks;

namespace ChunkHound.Services.IntegrationTests
{
    public class IndexingCoordinatorIntegrationTests : IDisposable
    {
        private readonly string _testDir;
        private readonly IndexingCoordinator _coordinator;

        public IndexingCoordinatorIntegrationTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "ChunkHoundTest");
            Directory.CreateDirectory(_testDir);

            // Setup with in-memory database for testing
            var dbProvider = new InMemoryDatabaseProvider();
            _coordinator = new IndexingCoordinator(dbProvider, _testDir);
        }

        [Fact]
        public async Task ProcessDirectoryAsync_WithTestFiles_IndexesSuccessfully()
        {
            // Arrange
            var testFile = Path.Combine(_testDir, "test.cs");
            await File.WriteAllTextAsync(testFile, "class Test { }");

            // Act
            var result = await _coordinator.ProcessDirectoryAsync(_testDir);

            // Assert
            Assert.Equal(IndexingStatus.Success, result.Status);
            Assert.True(result.FilesProcessed > 0);
        }

        public void Dispose()
        {
            Directory.Delete(_testDir, true);
        }
    }
}
```

## Dependencies

- `ChunkHound.Interfaces.IDatabaseProvider`
- `ChunkHound.Interfaces.IEmbeddingProvider`
- `ChunkHound.Parsers.IUniversalParser`
- `ChunkHound.Core.Models.File`
- `ChunkHound.Core.Models.Chunk`
- `Microsoft.Extensions.Logging`
- `System.Collections.Concurrent`
- `System.Threading`

## Notes

- This class uses async/await patterns throughout for scalability and non-blocking I/O operations.
- Concurrent collections (ConcurrentQueue, ConcurrentDictionary) ensure thread-safe operations in the producer-consumer pipeline.
- ReaderWriterLockSlim provides efficient database synchronization with separate read/write locking.
- File-level semaphores prevent race conditions during concurrent file processing.
- The design follows dependency injection principles for testability and flexibility.
- Worker patterns use Task.Run for CPU-bound operations and async/await for I/O-bound operations.
- Cancellation tokens are propagated throughout for cooperative cancellation.
- Progress reporting uses IProgress<T> for UI integration.
- All database operations are wrapped in transactions for consistency.