# EmbeddingService Design Document

> **Spec Version:** 1.0 | **Code:** `ChunkHound.Services.EmbeddingService` | **Status:** draft

## Overview

The `EmbeddingService` class is a comprehensive service for managing vector embedding generation, caching, and optimization in the ChunkHound system. This service handles batched embedding generation with intelligent error recovery, dynamic batch sizing based on performance metrics, and integration with the database for tracking embedding status. It implements the core embedding functionality ported from the Python `EmbeddingService`, adapted for C# async patterns and dependency injection.

This service manages the complete embedding lifecycle: **Chunk Processing → Batch Generation → Error Recovery → Database Storage**.

## Architecture Integration

The EmbeddingService integrates with the broader ChunkHound architecture as follows:

```
Database ←→ EmbeddingService ←→ IEmbeddingProvider
    ↑              ↓
ChunkCacheService  BatchProcessor (internal)
    ↑              ↓
   IndexingCoordinator
```

- **Dependencies:** `IDatabaseProvider`, `IEmbeddingProvider`, `ILogger`
- **Internal Components:** `EmbeddingBatchProcessor`, `EmbeddingErrorHandler`, `EmbeddingProgressManager`
- **Concurrency:** Supports concurrent batch processing with configurable limits
- **Error Recovery:** Implements transient/permanent error classification with retry logic

## Core Components

### EmbeddingBatchProcessor

Handles the complex batch processing loop with dynamic sizing and error recovery.

### EmbeddingErrorHandler

Tracks transient failures and determines when to abort generation due to excessive errors.

### EmbeddingProgressManager

Manages progress reporting for embedding operations.

## Class Definition

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core.Models;
using ChunkHound.Interfaces;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Services
{
    /// <summary>
    /// Service for managing embedding generation, caching, and optimization.
    /// Handles batched generation with error recovery and dynamic batch sizing.
    /// </summary>
    public class EmbeddingService
    {
        // Properties and methods defined below
    }
}
```

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `DatabaseProvider` | `IDatabaseProvider` | Database provider for persistence and queries |
| `EmbeddingProvider` | `IEmbeddingProvider?` | Provider for generating vector embeddings |
| `Logger` | `ILogger<EmbeddingService>` | Logger for diagnostic information |
| `EmbeddingBatchSize` | `int` | Number of texts per embedding API request |
| `DatabaseBatchSize` | `int` | Number of records per database transaction |
| `MaxConcurrentBatches` | `int` | Maximum concurrent batches (auto-detected from provider) |

## Constructor and Dependencies

```csharp
/// <summary>
/// Initializes a new instance of the EmbeddingService class.
/// </summary>
public EmbeddingService(
    IDatabaseProvider databaseProvider,
    IEmbeddingProvider? embeddingProvider = null,
    int embeddingBatchSize = 1000,
    int databaseBatchSize = 2000,
    int? maxConcurrentBatches = null,
    int optimizationBatchFrequency = 100,
    IProgress<EmbeddingProgressInfo>? progress = null,
    int errorSampleLimit = 5,
    int maxConsecutiveTransientFailures = 5,
    TimeSpan? transientErrorWindow = null,
    ILogger<EmbeddingService>? logger = null)
{
    DatabaseProvider = databaseProvider ?? throw new ArgumentNullException(nameof(databaseProvider));
    EmbeddingProvider = embeddingProvider;
    Logger = logger ?? NullLogger<EmbeddingService>.Instance;

    EmbeddingBatchSize = embeddingBatchSize;
    DatabaseBatchSize = databaseBatchSize;
    OptimizationBatchFrequency = optimizationBatchFrequency;
    Progress = progress;



    ErrorSampleLimit = errorSampleLimit;
    MaxConsecutiveTransientFailures = maxConsecutiveTransientFailures;
    TransientErrorWindow = transientErrorWindow ?? TimeSpan.FromSeconds(300);

    // Auto-detect optimal concurrency from provider
    MaxConcurrentBatches = maxConcurrentBatches ??
        (embeddingProvider as dynamic)?.GetRecommendedConcurrency() ?? 8;

    // Initialize internal components
    _errorHandler = new EmbeddingErrorHandler(MaxConsecutiveTransientFailures, TransientErrorWindow);
    _progressManager = new EmbeddingProgressManager(Progress);
    _batchProcessor = new EmbeddingBatchProcessor(this, _errorHandler, _progressManager);
}

private readonly EmbeddingErrorHandler _errorHandler;
private readonly EmbeddingProgressManager _progressManager;
private readonly EmbeddingBatchProcessor _batchProcessor;
```

## Core Methods

### GenerateEmbeddingsForChunksAsync

```csharp
/// <summary>
/// Generates embeddings for a list of chunks with progress tracking.
/// </summary>
public async Task<EmbeddingResult> GenerateEmbeddingsForChunksAsync(
    IReadOnlyList<Chunk> chunks,
    IProgress<EmbeddingProgressInfo>? progress = null,
    CancellationToken cancellationToken = default)
{
    if (EmbeddingProvider == null)
    {
        Logger.LogWarning("No embedding provider configured");
        return new EmbeddingResult { TotalGenerated = 0, FailedChunks = 0, PermanentFailures = 0 };
    }

    if (chunks.Count == 0)
    {
        return new EmbeddingResult { TotalGenerated = 0, FailedChunks = 0, PermanentFailures = 0 };
    }

    try
    {
        Logger.LogDebug("Generating embeddings for {Count} chunks", chunks.Count);

        // Filter out chunks that already have embeddings
        var filteredChunks = await FilterExistingEmbeddingsAsync(chunks, cancellationToken);

        if (filteredChunks.Count == 0)
        {
            Logger.LogDebug("All chunks already have embeddings");
            return new EmbeddingResult { TotalGenerated = 0, FailedChunks = 0, PermanentFailures = 0 };
        }

        // Generate embeddings in batches with error recovery
        var result = await GenerateEmbeddingsInBatchesAsync(
            filteredChunks, progress, cancellationToken);

        Logger.LogDebug(
            "Successfully generated {Generated} embeddings (processed: {Processed}, failed: {Failed}, permanent: {Permanent})",
            result.TotalGenerated, result.TotalProcessed, result.FailedChunks, result.PermanentFailures);

        return result;
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to generate embeddings for {Count} chunks", chunks.Count);
        return new EmbeddingResult
        {
            TotalGenerated = 0,
            FailedChunks = 0,
            PermanentFailures = 0,
            Error = ex.Message
        };
    }
}
```



### RegenerateEmbeddingsAsync

```csharp
/// <summary>
/// Regenerates embeddings for specific files or chunks.
/// </summary>
public async Task<RegenerateResult> RegenerateEmbeddingsAsync(
    string? filePath = null,
    IReadOnlyList<long>? chunkIds = null,
    CancellationToken cancellationToken = default)
{
    try
    {
        if (EmbeddingProvider == null)
        {
            return new RegenerateResult { Status = "error", Error = "No embedding provider configured" };
        }

        // Determine which chunks to regenerate
        var chunksToRegenerate = filePath != null
            ? await GetChunksByFilePathAsync(filePath, cancellationToken)
            : await GetChunksByIdsAsync(chunkIds ?? Array.Empty<long>(), cancellationToken);

        if (chunksToRegenerate.Count == 0)
        {
            return new RegenerateResult { Status = "complete", Regenerated = 0, Message = "No chunks found" };
        }

        Logger.LogInformation("Regenerating embeddings for {Count} chunks", chunksToRegenerate.Count);

        // Delete existing embeddings
        var providerName = EmbeddingProvider.ProviderName;
        var modelName = EmbeddingProvider.ModelName;

        await DeleteEmbeddingsForChunksAsync(
            chunksToRegenerate.Select(c => c.Id).ToList(), providerName, modelName, cancellationToken);

        // Generate new embeddings
        var chunkTexts = chunksToRegenerate.Select(c => c.Code).ToList();
        var result = await GenerateEmbeddingsForChunksAsync(chunksToRegenerate, cancellationToken: cancellationToken);
        var regeneratedCount = result.TotalGenerated;

        return new RegenerateResult
        {
            Status = "success",
            Regenerated = regeneratedCount,
            TotalChunks = chunksToRegenerate.Count,
            Provider = providerName,
            Model = modelName
        };
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to regenerate embeddings");
        return new RegenerateResult { Status = "error", Error = ex.Message };
    }
}
```

## Batched Generation with Error Recovery

### GenerateEmbeddingsInBatchesAsync

```csharp
/// <summary>
/// Generates embeddings for chunks in optimized batches with granular failure handling.
/// </summary>
private async Task<EmbeddingResult> GenerateEmbeddingsInBatchesAsync(
    IReadOnlyList<Chunk> chunks,
    IProgress<EmbeddingProgressInfo>? progress = null,
    CancellationToken cancellationToken = default)
{
    var errorClassifier = new EmbeddingErrorClassifier();
    var batches = CreateTokenAwareBatches(chunks);

    Logger.LogDebug("Processing {Count} token-aware batches", batches.Count);

    var totalGenerated = 0;
    var totalProcessed = 0;
    var failedChunks = 0;
    var permanentFailures = 0;
    var retryAttempts = 0;
    var allErrorStats = new Dictionary<string, int>();
    var allErrorSamples = new Dictionary<string, List<string>>();

    // Process batches with concurrency control
    var semaphore = new SemaphoreSlim(MaxConcurrentBatches);

    async Task<BatchResult> ProcessBatchAsync(List<Chunk> batch, int batchNum)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            return await ProcessBatchWithRetriesAsync(batch, batchNum, errorClassifier, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    var batchTasks = batches.Select((batch, i) => ProcessBatchAsync(batch, i));
    var batchResults = await Task.WhenAll(batchTasks);

    // Collect and process results
    var allEmbeddingsData = new List<EmbeddingData>();

    foreach (var batchResult in batchResults)
    {
        if (batchResult == null) continue;

        // Add successful embeddings
        allEmbeddingsData.AddRange(batchResult.SuccessfulChunks.Select(c => new EmbeddingData
        {
            ChunkId = c.Chunk.Id,
            Provider = EmbeddingProvider!.ProviderName,
            Model = EmbeddingProvider!.ModelName,
            Dimensions = c.Embedding.Count,
            Embedding = c.Embedding,
            Status = "success"
        }));

        // Track statistics
        totalGenerated += batchResult.SuccessfulChunks.Count;
        failedChunks += batchResult.FailedChunks.Count(f => f.Classification == EmbeddingErrorClassification.Transient);
        permanentFailures += batchResult.FailedChunks.Count(f => f.Classification == EmbeddingErrorClassification.Permanent);

        // Merge error statistics
        foreach (var (errorType, count) in batchResult.ErrorStats)
        {
            allErrorStats[errorType] = allErrorStats.GetValueOrDefault(errorType) + count;
        }

        // Merge error samples
        foreach (var (errorType, samples) in batchResult.ErrorSamples)
        {
            if (!allErrorSamples.ContainsKey(errorType))
                allErrorSamples[errorType] = new List<string>();

            allErrorSamples[errorType].AddRange(samples.Take(ErrorSampleLimit - allErrorSamples[errorType].Count));
        }
    }

    // Update chunks with failed statuses
    var chunkIdToStatus = new Dictionary<long, string>();
    foreach (var batchResult in batchResults)
    {
        if (batchResult == null) continue;

        foreach (var (chunk, _, classification) in batchResult.FailedChunks)
        {
            var status = classification == EmbeddingErrorClassification.Permanent ? "permanent_failure" : "failed";
            chunkIdToStatus[chunk.Id] = status;
        }
    }

    // Insert successful embeddings and update failed chunks
    if (allEmbeddingsData.Any() || chunkIdToStatus.Any())
    {
        Logger.LogDebug("Inserting {Embeddings} embeddings and updating status for {Failed} failed chunks",
            allEmbeddingsData.Count, chunkIdToStatus.Count);

        await DatabaseProvider.InsertEmbeddingsBatchAsync(allEmbeddingsData, chunkIdToStatus, cancellationToken);

        // Run optimization after bulk insert
        await DatabaseProvider.OptimizeTablesAsync(cancellationToken);
    }

    totalProcessed = totalGenerated + failedChunks + permanentFailures;

    Logger.LogDebug(
        "GenerateEmbeddingsInBatches completed: generated={Generated}, processed={Processed}, failed={Failed}, permanent={Permanent}, retries={Retries}",
        totalGenerated, totalProcessed, failedChunks, permanentFailures, retryAttempts);

    return new EmbeddingResult
    {
        TotalGenerated = totalGenerated,
        TotalProcessed = totalProcessed,
        SuccessfulChunks = totalGenerated,
        FailedChunks = failedChunks,
        PermanentFailures = permanentFailures,
        RetryAttempts = retryAttempts,
        ErrorStats = allErrorStats,
        ErrorSamples = allErrorSamples
    };
}
```

### ProcessBatchWithRetriesAsync

```csharp
/// <summary>
/// Processes a batch with intelligent retry logic for transient failures.
/// </summary>
private async Task<BatchResult> ProcessBatchWithRetriesAsync(
    List<Chunk> batch,
    int batchNum,
    EmbeddingErrorClassifier errorClassifier,
    CancellationToken cancellationToken)
{
    const int maxRetries = 3;
    var result = new BatchResult { BatchNum = batchNum };
    var currentBatch = batch;
    var attempt = 0;

    while (attempt < maxRetries && currentBatch.Any())
    {
        attempt++;
        if (attempt > 1)
        {
            result.RetryAttempts++;
            Logger.LogDebug("Batch {BatchNum} retry attempt {Attempt}/{MaxRetries}", batchNum, attempt, maxRetries);
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), 5)), cancellationToken);
        }

        var batchResult = await ProcessSingleBatchAsync(currentBatch, batchNum, errorClassifier, cancellationToken);

        // Add successful chunks
        result.SuccessfulChunks.AddRange(batchResult.SuccessfulChunks);

        // Handle failed chunks based on classification
        var transientFailures = new List<Chunk>();
        var permanentFailures = new List<Chunk>();

        foreach (var (chunk, _, classification) in batchResult.FailedChunks)
        {
            if (classification == EmbeddingErrorClassification.Transient)
                transientFailures.Add(chunk);
            else
                permanentFailures.Add(chunk);
        }

        // Permanent failures are final
        result.FailedChunks.AddRange(permanentFailures.Select(c => (c, "Permanent failure", EmbeddingErrorClassification.Permanent)));

        // For transient failures, retry if attempts remain
        if (transientFailures.Any() && attempt < maxRetries)
        {
            currentBatch = transientFailures;
            continue;
        }
        else
        {
            // Max retries reached or no transient failures
            result.FailedChunks.AddRange(transientFailures.Select(c => (c, "Transient failure after retries", EmbeddingErrorClassification.Permanent)));
        }

        // Merge error stats and samples
        foreach (var (errorType, count) in batchResult.ErrorStats)
        {
            result.ErrorStats[errorType] = result.ErrorStats.GetValueOrDefault(errorType) + count;
        }

        foreach (var (errorType, samples) in batchResult.ErrorSamples)
        {
            if (!result.ErrorSamples.ContainsKey(errorType))
                result.ErrorSamples[errorType] = new List<string>();

            result.ErrorSamples[errorType].AddRange(samples);
        }

        if (attempt >= maxRetries) break;
    }

    return result;
}
```

### CreateTokenAwareBatches

```csharp
/// <summary>
/// Creates batches that respect provider token limits using provider-agnostic logic.
/// </summary>
private List<List<Chunk>> CreateTokenAwareBatches(IReadOnlyList<Chunk> chunks)
{
    if (!chunks.Any()) return new List<List<Chunk>>();
    if (EmbeddingProvider == null)
    {
        // Simple batching without provider
        const int defaultBatchSize = 20;
        var batches = new List<List<Chunk>>();
        for (var i = 0; i < chunks.Count; i += defaultBatchSize)
        {
            batches.Add(chunks.Skip(i).Take(defaultBatchSize).ToList());
        }
        return batches;
    }

    const int maxChunksPerBatch = 300;
    var maxTokens = EmbeddingProvider.GetMaxTokensPerBatch();
    var maxDocuments = EmbeddingProvider.GetMaxDocumentsPerBatch();
    var safeLimit = (int)(maxTokens * 0.80);

    var batches = new List<List<Chunk>>();
    var currentBatch = new List<Chunk>();
    var currentTokens = 0;

    foreach (var chunk in chunks)
    {
        var textTokens = EstimateTokens(chunk.Code, EmbeddingProvider.ProviderName, EmbeddingProvider.ModelName);

        // Check if adding this chunk would exceed limits
        if ((currentTokens + textTokens > safeLimit && currentBatch.Any()) ||
            currentBatch.Count >= maxDocuments ||
            currentBatch.Count >= maxChunksPerBatch)
        {
            batches.Add(currentBatch);
            currentBatch = new List<Chunk> { chunk };
            currentTokens = textTokens;
        }
        else
        {
            currentBatch.Add(chunk);
            currentTokens += textTokens;
        }
    }

    if (currentBatch.Any()) batches.Add(currentBatch);

    Logger.LogInformation(
        "Created {BatchCount} batches for {ChunkCount} chunks (concurrency limit: {Concurrency})",
        batches.Count, chunks.Count, MaxConcurrentBatches);

    return batches;
}
```

## Error Recovery and Resilience

### Error Classification

The service implements sophisticated error classification:

- **Transient Errors:** Network timeouts, rate limits (retry with backoff)
- **Batch Recoverable:** Oversized batches (split and retry)
- **Permanent Errors:** Invalid API keys, unsupported models (fail fast)

### Batch Processing Resilience

- **Dynamic Batch Sizing:** Adjusts batch size based on performance metrics
- **Concurrent Processing:** Multiple batches processed simultaneously
- **Graceful Degradation:** Continues processing other batches when one fails
- **Resource Management:** Proper cleanup and connection pooling

## Testing Stubs

### Unit Tests

```csharp
using Xunit;
using Moq;
using ChunkHound.Services;
using ChunkHound.Interfaces;
using ChunkHound.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChunkHound.Services.Tests
{
    public class EmbeddingServiceTests : IDisposable
    {
        private readonly Mock<IDatabaseProvider> _mockDbProvider;
        private readonly Mock<IEmbeddingProvider> _mockEmbedProvider;
        private readonly EmbeddingService _service;

        public EmbeddingServiceTests()
        {
            _mockDbProvider = new Mock<IDatabaseProvider>();
            _mockEmbedProvider = new Mock<IEmbeddingProvider>();
            _service = new EmbeddingService(
                _mockDbProvider.Object,
                _mockEmbedProvider.Object,
                logger: new NullLogger<EmbeddingService>());
        }

        [Fact]
        public async Task GenerateEmbeddingsForChunksAsync_NoProviderConfigured_ReturnsZeroResults()
        {
            // Arrange
            var service = new EmbeddingService(_mockDbProvider.Object);
            var chunks = new List<Chunk> { CreateTestChunk() };

            // Act
            var result = await service.GenerateEmbeddingsForChunksAsync(chunks);

            // Assert
            Assert.Equal(0, result.TotalGenerated);
            Assert.Equal(0, result.FailedChunks);
        }

        [Fact]
        public async Task GenerateEmbeddingsForChunksAsync_ValidChunks_GeneratesEmbeddings()
        {
            // Arrange
            var chunk = CreateTestChunk();
            var chunks = new List<Chunk> { chunk };
            var embeddings = new List<List<float>> { new List<float> { 0.1f, 0.2f } };

            _mockEmbedProvider.Setup(p => p.EmbedAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                             .ReturnsAsync(embeddings);
            _mockEmbedProvider.Setup(p => p.ProviderName).Returns("test");
            _mockEmbedProvider.Setup(p => p.ModelName).Returns("model");
            _mockDbProvider.Setup(p => p.FilterExistingEmbeddingsAsync(It.IsAny<List<long>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new List<long>());

            // Act
            var result = await _service.GenerateEmbeddingsForChunksAsync(chunks);

            // Assert
            Assert.Equal(1, result.TotalGenerated);
            _mockDbProvider.Verify(p => p.InsertEmbeddingsBatchAsync(It.IsAny<List<EmbeddingData>>(), It.IsAny<Dictionary<long, string>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        private static Chunk CreateTestChunk() =>
            new Chunk("TestFunc", 1, 10, "function test() {}", ChunkType.Function, 1, Language.JavaScript);

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
```

### Integration Tests

```csharp
using Xunit;
using ChunkHound.Services;
using ChunkHound.Providers.Embeddings;
using ChunkHound.Core.Models;
using ChunkHound.Core.Types;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChunkHound.Services.IntegrationTests
{
    public class EmbeddingServiceIntegrationTests : IDisposable
    {
        private readonly EmbeddingService _service;

        public EmbeddingServiceIntegrationTests()
        {
            // Use test implementations
            var dbProvider = new TestDatabaseProvider();
            var embedProvider = new FakeConstantProvider();
            _service = new EmbeddingService(
                dbProvider,
                embedProvider,
                logger: new NullLogger<EmbeddingService>());
        }



        [Fact]
        public async Task RegenerateEmbeddingsAsync_SpecificFile_Succeeds()
        {
            // Arrange
            var filePath = "/test/file.cs";

            // Act
            var result = await _service.RegenerateEmbeddingsAsync(filePath: filePath);

            // Assert
            Assert.Equal("success", result.Status);
        }

        public void Dispose()
        {
            // Cleanup test data
        }
    }
}
```

## Dependencies

- `ChunkHound.Interfaces.IDatabaseProvider`
- `ChunkHound.Interfaces.IEmbeddingProvider`
- `ChunkHound.Core.Models.Chunk`
- `Microsoft.Extensions.Logging`
- `System.Collections.Generic`
- `System.Threading`

## Performance Optimizations

- **Token-Aware Batching:** Respects provider limits for optimal API efficiency
- **Concurrent Processing:** Multiple batches processed simultaneously
- **Dynamic Sizing:** Adjusts batch size based on performance metrics
- **Memory Efficient:** Streams processing to avoid loading all data at once
- **Database Optimization:** Runs optimization after bulk operations

## Notes

- This service is designed for high-throughput embedding generation with error recovery
- Batch sizing is dynamically adjusted based on provider performance and limits
- Error classification enables intelligent retry strategies for transient failures
- Provider and model metadata are automatically captured for each embedding
- The design follows dependency injection principles for testability and flexibility
- Comprehensive logging provides visibility into batch processing and error conditions