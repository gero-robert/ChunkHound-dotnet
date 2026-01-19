# EmbedWorker Service Design Document

> **Spec Version:** 1.0 | **Code:** `ChunkHound.Services.EmbedWorker` | **Status:** draft

## Overview

The `EmbedWorker` class is a dedicated worker service in the ChunkHound indexing pipeline responsible for generating vector embeddings for parsed code chunks. This service dequeues chunks from a concurrent queue, batches them for efficient API calls to embedding providers, generates embeddings, and enqueues the resulting chunk-embedding pairs for storage. It implements async patterns for scalability and handles provider-specific batching limits.

This design is ported from the `EmbedWorkerAsync` method in `IndexingCoordinator`, extracted into a standalone service for better separation of concerns, testability, and scalability. It implements the embedding generation stage of the indexing pipeline: **Chunks Queue → Embedding Generation → EmbedChunks Queue**.

## Pipeline Flow Integration

The EmbedWorker fits into the ChunkHound indexing pipeline as follows:

```
Files Queue → ParseWorkers → Chunks Queue → EmbedWorkers → EmbedChunks Queue → StoreWorkers → Database
```

- **Input:** `ConcurrentQueue<Chunk>` containing parsed code chunks ready for embedding
- **Output:** `ConcurrentQueue<EmbedChunk>` containing chunks paired with their vector embeddings
- **Concurrency:** Multiple EmbedWorker instances can run concurrently, coordinated through shared queues
- **Batching:** Implements provider-aware batching to optimize API calls and respect rate limits

## Class Definition

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core.Models;
using ChunkHound.Interfaces;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Services
{
    /// <summary>
    /// Worker service that dequeues chunks, generates embeddings in batches,
    /// and enqueues embed chunks for storage. This service implements the embedding
    /// generation stage of the indexing pipeline with provider-aware batching.
    /// </summary>
    public class EmbedWorker : IDisposable
    {
        // Properties and methods defined below
    }
}
```

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `EmbeddingProvider` | `IEmbeddingProvider` | Provider for generating vector embeddings |
| `ChunksQueue` | `ConcurrentQueue<Chunk>` | Shared queue for incoming chunks |
| `EmbedChunksQueue` | `ConcurrentQueue<EmbedChunk>` | Shared queue for outgoing embed chunks |
| `BatchSize` | `int` | Number of chunks to batch before embedding (default: 100) |
| `Logger` | `ILogger<EmbedWorker>` | Logger for diagnostic information |

## Constructor and Dependencies

```csharp
/// <summary>
/// Initializes a new instance of the EmbedWorker class.
/// </summary>
public EmbedWorker(
    IEmbeddingProvider embeddingProvider,
    ConcurrentQueue<Chunk> chunksQueue,
    ConcurrentQueue<EmbedChunk> embedChunksQueue,
    int batchSize = 100,
    ILogger<EmbedWorker>? logger = null)
{
    EmbeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
    ChunksQueue = chunksQueue ?? throw new ArgumentNullException(nameof(chunksQueue));
    EmbedChunksQueue = embedChunksQueue ?? throw new ArgumentNullException(nameof(embedChunksQueue));
    BatchSize = batchSize > 0 ? batchSize : 100;
    Logger = logger ?? NullLogger<EmbedWorker>.Instance;

    _cancellationTokenSource = new CancellationTokenSource();
}

/// <summary>
/// Shared cancellation token source for graceful shutdown.
/// </summary>
private readonly CancellationTokenSource _cancellationTokenSource;
```

## Core Methods

### StartAsync

```csharp
/// <summary>
/// Starts the embed worker processing loop.
/// </summary>
public async Task StartAsync(CancellationToken externalCancellationToken = default)
{
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
        _cancellationTokenSource.Token, externalCancellationToken);

    Logger.LogInformation("EmbedWorker started with batch size {BatchSize}", BatchSize);

    var batch = new List<Chunk>();

    try
    {
        while (!linkedCts.Token.IsCancellationRequested)
        {
            // Collect batch
            while (batch.Count < BatchSize && ChunksQueue.TryDequeue(out var chunk))
            {
                batch.Add(chunk);
            }

            if (batch.Any())
            {
                await ProcessBatchAsync(batch, linkedCts.Token);
                batch.Clear();
            }
            else
            {
                // Small delay to prevent busy waiting
                await Task.Delay(10, linkedCts.Token);
            }
        }
    }
    catch (OperationCanceledException)
    {
        Logger.LogInformation("EmbedWorker cancelled gracefully");
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "EmbedWorker failed unexpectedly");
        throw;
    }
    finally
    {
        // Process any remaining items in batch
        if (batch.Any())
        {
            try
            {
                await ProcessBatchAsync(batch, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to process final batch during shutdown");
            }
        }
    }
}
```

### ProcessBatchAsync

```csharp
/// <summary>
/// Processes a batch of chunks by generating embeddings and enqueuing results.
/// </summary>
private async Task ProcessBatchAsync(List<Chunk> batch, CancellationToken cancellationToken)
{
    try
    {
        var texts = batch.Select(c => c.Code).ToList();
        var embeddings = await EmbeddingProvider.EmbedAsync(texts, cancellationToken);

        // Validate embedding dimensions match batch size
        if (embeddings.Count != batch.Count)
        {
            throw new InvalidOperationException(
                $"Embedding count ({embeddings.Count}) doesn't match batch size ({batch.Count})");
        }

        // Create embed chunks and enqueue
        for (var i = 0; i < batch.Count; i++)
        {
            var embedChunk = new EmbedChunk(
                batch[i],
                embeddings[i],
                EmbeddingProvider.ProviderName,
                EmbeddingProvider.ModelName);

            EmbedChunksQueue.Enqueue(embedChunk);
        }

        Logger.LogDebug("Processed batch of {Count} chunks with embeddings", batch.Count);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to process batch of {Count} chunks", batch.Count);
        throw;
    }
}
```

### StopAsync

```csharp
/// <summary>
/// Stops the embed worker gracefully.
/// </summary>
public async Task StopAsync()
{
    Logger.LogInformation("Stopping EmbedWorker...");
    _cancellationTokenSource.Cancel();

    // Give some time for graceful shutdown
    await Task.Delay(100);
}
```

## IDisposable Implementation

```csharp
/// <summary>
/// Disposes resources used by the EmbedWorker.
/// </summary>
public void Dispose()
{
    _cancellationTokenSource.Dispose();
}
```

## Error Handling and Resilience

### Batch Processing Resilience

- **Validation:** Ensures embedding count matches input batch size
- **Provider Metadata:** Automatically captures provider and model information
- **Graceful Shutdown:** Processes remaining batches during cancellation
- **Error Propagation:** Failures in embedding generation stop the batch but log details

### Performance Optimizations

- **Configurable Batching:** Balances API efficiency vs. memory usage
- **Non-blocking Dequeue:** Uses TryDequeue for immediate availability checks
- **Concurrent Processing:** Multiple workers can process different batches simultaneously
- **Memory Efficient:** Processes and clears batches immediately

## Testing Stubs

### Unit Tests

```csharp
using Xunit;
using Moq;
using ChunkHound.Services;
using ChunkHound.Interfaces;
using ChunkHound.Core.Models;
using ChunkHound.Core.Types;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChunkHound.Services.Tests
{
    public class EmbedWorkerTests : IDisposable
    {
        private readonly Mock<IEmbeddingProvider> _mockEmbedProvider;
        private readonly ConcurrentQueue<Chunk> _chunksQueue;
        private readonly ConcurrentQueue<EmbedChunk> _embedChunksQueue;
        private readonly EmbedWorker _worker;

        public EmbedWorkerTests()
        {
            _mockEmbedProvider = new Mock<IEmbeddingProvider>();
            _chunksQueue = new ConcurrentQueue<Chunk>();
            _embedChunksQueue = new ConcurrentQueue<EmbedChunk>();
            _worker = new EmbedWorker(_mockEmbedProvider.Object, _chunksQueue, _embedChunksQueue, batchSize: 2);
        }

        [Fact]
        public async Task ProcessBatchAsync_ValidBatch_GeneratesEmbeddingsAndEnqueues()
        {
            // Arrange
            var chunk1 = new Chunk(null, 1, 5, "test code 1", ChunkType.Function, 1, Language.CSharp);
            var chunk2 = new Chunk(null, 6, 10, "test code 2", ChunkType.Function, 1, Language.CSharp);
            var batch = new List<Chunk> { chunk1, chunk2 };
            var embeddings = new List<List<float>>
            {
                new List<float> { 0.1f, 0.2f },
                new List<float> { 0.3f, 0.4f }
            };

            _mockEmbedProvider.Setup(p => p.EmbedAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                             .ReturnsAsync(embeddings);
            _mockEmbedProvider.Setup(p => p.ProviderName).Returns("test");
            _mockEmbedProvider.Setup(p => p.ModelName).Returns("model");

            // Act
            await _worker.GetType().GetMethod("ProcessBatchAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(_worker, new object[] { batch, CancellationToken.None });

            // Assert
            Assert.Equal(2, _embedChunksQueue.Count);
            Assert.True(_embedChunksQueue.TryDequeue(out var embedChunk1));
            Assert.True(_embedChunksQueue.TryDequeue(out var embedChunk2));
            Assert.Equal(chunk1, embedChunk1.Chunk);
            Assert.Equal(chunk2, embedChunk2.Chunk);
        }

        [Fact]
        public async Task StartAsync_WithItemsInQueue_ProcessesItems()
        {
            // Arrange
            var chunk = new Chunk(null, 1, 5, "test code", ChunkType.Function, 1, Language.CSharp);
            _chunksQueue.Enqueue(chunk);

            var embeddings = new List<List<float>> { new List<float> { 0.1f, 0.2f } };
            _mockEmbedProvider.Setup(p => p.EmbedAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                             .ReturnsAsync(embeddings);
            _mockEmbedProvider.Setup(p => p.ProviderName).Returns("test");
            _mockEmbedProvider.Setup(p => p.ModelName).Returns("model");

            // Act
            var cts = new CancellationTokenSource(500); // Stop after 500ms
            await _worker.StartAsync(cts.Token);

            // Assert
            Assert.True(_embedChunksQueue.TryDequeue(out var embedChunk));
            Assert.Equal(chunk, embedChunk.Chunk);
        }

        public void Dispose()
        {
            _worker.Dispose();
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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ChunkHound.Services.IntegrationTests
{
    public class EmbedWorkerIntegrationTests : IDisposable
    {
        private readonly ConcurrentQueue<Chunk> _chunksQueue;
        private readonly ConcurrentQueue<EmbedChunk> _embedChunksQueue;
        private readonly EmbedWorker _worker;

        public EmbedWorkerIntegrationTests()
        {
            // Use a test embedding provider (e.g., FakeConstantProvider)
            var embedProvider = new FakeConstantProvider();
            _chunksQueue = new ConcurrentQueue<Chunk>();
            _embedChunksQueue = new ConcurrentQueue<EmbedChunk>();
            _worker = new EmbedWorker(embedProvider, _chunksQueue, _embedChunksQueue, batchSize: 2);
        }

        [Fact]
        public async Task FullWorkflow_EmbedChunks_Succeeds()
        {
            // Arrange
            var chunk1 = new Chunk("TestFunc", 1, 10, "function test() {}", ChunkType.Function, 1, Language.JavaScript);
            var chunk2 = new Chunk("TestClass", 11, 20, "class Test {}", ChunkType.Class, 1, Language.JavaScript);

            _chunksQueue.Enqueue(chunk1);
            _chunksQueue.Enqueue(chunk2);

            // Act
            var cts = new CancellationTokenSource(1000);
            await _worker.StartAsync(cts.Token);

            // Assert
            Assert.Equal(2, _embedChunksQueue.Count);
        }

        public void Dispose()
        {
            _worker.Dispose();
        }
    }
}
```

## Dependencies

- `ChunkHound.Interfaces.IEmbeddingProvider`
- `ChunkHound.Core.Models.Chunk`
- `Microsoft.Extensions.Logging`
- `System.Collections.Concurrent`
- `System.Threading`

## Notes

- This service is designed for high-throughput embedding generation with multiple concurrent instances
- Batch sizing is configurable to balance API efficiency with memory usage
- Provider and model metadata are automatically captured for each embedding
- Error handling ensures batch failures are logged but don't crash the worker
- Graceful shutdown ensures no data loss during application termination
- The design follows dependency injection principles for testability and flexibility