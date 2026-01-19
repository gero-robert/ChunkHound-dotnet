# BatchProcessor Service Design Document

> **Spec Version:** 1.0 | **Code:** `ChunkHound.Services.BatchProcessor` | **Status:** draft

## Overview

The `BatchProcessor` class handles the complex batch processing loop for parallel file processing in the ChunkHound indexing pipeline. This service manages dynamic batch sizing, error handling, progress tracking, and concurrent processing of files with intelligent retry logic and performance optimization.

This design is inspired by the Python `EmbeddingBatchProcessor` in `chunkhound/services/embedding_service.py`, adapted for C# with async patterns and .NET concurrency primitives for file batch processing instead of embedding generation.

## Class Definition

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core.Models;
using ChunkHound.Interfaces;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Services
{
    /// <summary>
    /// Handles the complex batch processing loop for parallel file processing with dynamic sizing and error handling.
    /// </summary>
    public class BatchProcessor
    {
        // Properties and methods defined below
    }
}
```

## BatchProcessingResult Record

```csharp
/// <summary>
/// Result of batch processing operations.
/// </summary>
public record BatchProcessingResult
{
    /// <summary>
    /// Total files attempted for processing.
    /// </summary>
    public int TotalAttempted { get; init; }

    /// <summary>
    /// Total files successfully processed.
    /// </summary>
    public int TotalProcessed { get; init; }

    /// <summary>
    /// Total files that failed processing.
    /// </summary>
    public int TotalFailed { get; init; }

    /// <summary>
    /// Total permanent failures.
    /// </summary>
    public int TotalPermanentFailures { get; init; }

    /// <summary>
    /// Number of batches processed.
    /// </summary>
    public int BatchCount { get; init; }

    /// <summary>
    /// Error statistics by type.
    /// </summary>
    public Dictionary<string, int> ErrorStats { get; init; } = new();
}
```

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `FileProcessor` | `IFileProcessor` | Service for processing individual files |
| `ErrorHandler` | `IErrorHandler` | Error handler for tracking failures and retries |
| `ProgressManager` | `IProgressManager` | Progress manager for UI updates |
| `Logger` | `ILogger<BatchProcessor>` | Logger for diagnostic information |

## Constructor and Dependencies

```csharp
/// <summary>
/// Initializes a new instance of the BatchProcessor class.
/// </summary>
public BatchProcessor(
    IFileProcessor fileProcessor,
    IErrorHandler errorHandler,
    IProgressManager progressManager,
    ILogger<BatchProcessor>? logger = null)
{
    FileProcessor = fileProcessor ?? throw new ArgumentNullException(nameof(fileProcessor));
    ErrorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
    ProgressManager = progressManager ?? throw new ArgumentNullException(nameof(progressManager));
    Logger = logger ?? NullLogger<BatchProcessor>.Instance;
}

// Private fields
private readonly IFileProcessor _fileProcessor;
private readonly IErrorHandler _errorHandler;
private readonly IProgressManager _progressManager;
private readonly ILogger<BatchProcessor> _logger;
```

## Core Methods

### ProcessAllBatchesAsync

```csharp
/// <summary>
/// Process all batches of files with dynamic batch sizing and error handling.
/// </summary>
public async Task<BatchProcessingResult> ProcessAllBatchesAsync(
    List<string> filePaths,
    int initialBatchSize = 10,
    int minBatchSize = 1,
    int maxBatchSize = 100,
    TimeSpan targetBatchTime = default,
    TimeSpan slowThreshold = default,
    TimeSpan fastThreshold = default,
    CancellationToken cancellationToken = default)
{
    if (targetBatchTime == default) targetBatchTime = TimeSpan.FromSeconds(15);
    if (slowThreshold == default) slowThreshold = TimeSpan.FromSeconds(25);
    if (fastThreshold == default) fastThreshold = TimeSpan.FromSeconds(5);

    _logger.LogInformation("Starting batch processing for {FileCount} files", filePaths.Count);

    var currentBatchSize = initialBatchSize;
    var totalAttempted = 0;
    var totalProcessed = 0;
    var totalFailed = 0;
    var totalPermanentFailures = 0;
    var batchCount = 0;
    var fileIndex = 0;

    var stopwatch = Stopwatch.StartNew();

    while (fileIndex < filePaths.Count && !cancellationToken.IsCancellationRequested)
    {
        batchCount++;

        // Measure time for batch retrieval
        var batchStartTime = stopwatch.Elapsed;

        try
        {
            // Get next batch of files
            var batchSize = Math.Min(currentBatchSize, filePaths.Count - fileIndex);
            var batchFiles = filePaths.GetRange(fileIndex, batchSize);

            _logger.LogDebug("Processing batch {BatchNum}: size={BatchSize}, files={FileRange}",
                batchCount, batchSize, $"{fileIndex}-{fileIndex + batchSize - 1}");

            var batchRetrievalTime = stopwatch.Elapsed - batchStartTime;

            // Update progress total
            await _progressManager.IncrementTotalAsync(batchSize, cancellationToken);

            totalAttempted += batchSize;

            // Process batch with parallel processing
            var batchResult = await ProcessBatchAsync(batchFiles, cancellationToken);

            // Track results
            totalProcessed += batchResult.ProcessedCount;
            totalFailed += batchResult.FailedCount;
            totalPermanentFailures += batchResult.PermanentFailureCount;

            // Update progress
            await _progressManager.AdvanceAsync(batchResult.ProcessedCount, cancellationToken);

            var info = $"success: {totalProcessed}, failed: {totalFailed}, permanent: {totalPermanentFailures}";
            await _progressManager.UpdateInfoAsync(info, cancellationToken);

            // Check for abort condition
            if (_errorHandler.ShouldAbort())
            {
                _logger.LogWarning("Aborting batch processing due to excessive failures");
                break;
            }

            // Adjust batch size based on performance
            if (targetBatchTime != TimeSpan.Zero)
            {
                var batchProcessingTime = stopwatch.Elapsed - batchStartTime;

                if (batchProcessingTime > slowThreshold && currentBatchSize > minBatchSize)
                {
                    var oldSize = currentBatchSize;
                    currentBatchSize = Math.Max(minBatchSize, currentBatchSize / 2);
                    _logger.LogInformation("Batch too slow ({ProcessingTime} > {Threshold}s), reducing batch size: {OldSize} -> {NewSize}",
                        batchProcessingTime.TotalSeconds, slowThreshold.TotalSeconds, oldSize, currentBatchSize);
                }
                else if (batchProcessingTime < fastThreshold && currentBatchSize < maxBatchSize)
                {
                    var oldSize = currentBatchSize;
                    currentBatchSize = Math.Min(maxBatchSize, currentBatchSize * 2);
                    _logger.LogDebug("Batch very fast ({ProcessingTime}s < {Threshold}s), increasing batch size: {OldSize} -> {NewSize}",
                        batchProcessingTime.TotalSeconds, fastThreshold.TotalSeconds, oldSize, currentBatchSize);
                }
                else if (batchProcessingTime < targetBatchTime && currentBatchSize < maxBatchSize)
                {
                    var oldSize = currentBatchSize;
                    currentBatchSize = Math.Min(maxBatchSize, (int)(currentBatchSize * 1.5));
                    _logger.LogDebug("Batch reasonably fast ({ProcessingTime}s), small increase: {OldSize} -> {NewSize}",
                        batchProcessingTime.TotalSeconds, oldSize, currentBatchSize);
                }
            }

            fileIndex += batchSize;

            _logger.LogInformation("Batch {BatchNum} completed: processed={Processed}, failed={Failed}, permanent={Permanent}, current_batch_size={CurrentSize}",
                batchCount, batchResult.ProcessedCount, batchResult.FailedCount, batchResult.PermanentFailureCount, currentBatchSize);
        }
        catch (Exception ex)
        {
            // If batch fails, reduce batch size and retry once
            if (currentBatchSize > minBatchSize)
            {
                var oldSize = currentBatchSize;
                currentBatchSize = Math.Max(minBatchSize, currentBatchSize / 2);
                _logger.LogWarning(ex, "Batch retrieval/processing failed (size={OldSize}), reducing to {NewSize}", oldSize, currentBatchSize);
                continue;
            }
            else
            {
                _logger.LogError(ex, "Batch processing failed permanently after reducing batch size");
                throw;
            }
        }
    }

    _logger.LogInformation("Completed batch processing: attempted={Attempted}, processed={Processed}, failed={Failed}, permanent={Permanent}",
        totalAttempted, totalProcessed, totalFailed, totalPermanentFailures);

    return new BatchProcessingResult
    {
        TotalAttempted = totalAttempted,
        TotalProcessed = totalProcessed,
        TotalFailed = totalFailed,
        TotalPermanentFailures = totalPermanentFailures,
        BatchCount = batchCount,
        ErrorStats = _errorHandler.GetErrorStats()
    };
}
```

### ProcessBatchAsync

```csharp
/// <summary>
/// Process a single batch of files with parallel processing and error handling.
/// </summary>
private async Task<BatchResult> ProcessBatchAsync(List<string> filePaths, CancellationToken cancellationToken)
{
    var semaphore = new SemaphoreSlim(Environment.ProcessorCount); // Limit concurrent file processing
    var tasks = new List<Task<FileProcessingResult>>();
    var results = new List<FileProcessingResult>();

    foreach (var filePath in filePaths)
    {
        if (cancellationToken.IsCancellationRequested) break;

        var task = Task.Run(async () =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await _fileProcessor.ProcessFileAsync(filePath, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }, cancellationToken);

        tasks.Add(task);
    }

    // Wait for all tasks to complete
    var completedTasks = await Task.WhenAll(tasks);

    var processedCount = 0;
    var failedCount = 0;
    var permanentFailureCount = 0;

    foreach (var result in completedTasks)
    {
        if (result.Status == FileProcessingStatus.Success)
        {
            processedCount++;
        }
        else
        {
            failedCount++;
            if (result.Status == FileProcessingStatus.PermanentFailure)
            {
                permanentFailureCount++;
            }
        }

        // Track errors for the error handler
        if (result.Status != FileProcessingStatus.Success && !string.IsNullOrEmpty(result.Error))
        {
            await _errorHandler.TrackErrorAsync(result.Error, result.Status == FileProcessingStatus.PermanentFailure, cancellationToken);
        }
    }

    return new BatchResult
    {
        ProcessedCount = processedCount,
        FailedCount = failedCount,
        PermanentFailureCount = permanentFailureCount
    };
}
```

## Supporting Types

### BatchResult Record

```csharp
/// <summary>
/// Result of processing a single batch.
/// </summary>
private record BatchResult
{
    public int ProcessedCount { get; init; }
    public int FailedCount { get; init; }
    public int PermanentFailureCount { get; init; }
}
```

## Interfaces

### IFileProcessor

```csharp
/// <summary>
/// Interface for processing individual files.
/// </summary>
public interface IFileProcessor
{
    Task<FileProcessingResult> ProcessFileAsync(string filePath, CancellationToken cancellationToken = default);
}
```

### IErrorHandler

```csharp
/// <summary>
/// Interface for handling errors and determining retry/abort conditions.
/// </summary>
public interface IErrorHandler
{
    Task TrackErrorAsync(string error, bool isPermanent, CancellationToken cancellationToken = default);
    bool ShouldAbort();
    Dictionary<string, int> GetErrorStats();
}
```

### IProgressManager

```csharp
/// <summary>
/// Interface for managing progress reporting.
/// </summary>
public interface IProgressManager
{
    Task IncrementTotalAsync(int increment, CancellationToken cancellationToken = default);
    Task AdvanceAsync(int advance, CancellationToken cancellationToken = default);
    Task UpdateInfoAsync(string info, CancellationToken cancellationToken = default);
}
```

## Testing Stubs

### Unit Tests

```csharp
using Xunit;
using Moq;
using ChunkHound.Services;
using ChunkHound.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChunkHound.Services.Tests
{
    public class BatchProcessorTests
    {
        private readonly Mock<IFileProcessor> _mockFileProcessor;
        private readonly Mock<IErrorHandler> _mockErrorHandler;
        private readonly Mock<IProgressManager> _mockProgressManager;
        private readonly BatchProcessor _processor;

        public BatchProcessorTests()
        {
            _mockFileProcessor = new Mock<IFileProcessor>();
            _mockErrorHandler = new Mock<IErrorHandler>();
            _mockProgressManager = new Mock<IProgressManager>();
            _processor = new BatchProcessor(
                _mockFileProcessor.Object,
                _mockErrorHandler.Object,
                _mockProgressManager.Object);
        }

        [Fact]
        public async Task ProcessAllBatchesAsync_EmptyFileList_ReturnsEmptyResult()
        {
            // Arrange
            var filePaths = new List<string>();

            // Act
            var result = await _processor.ProcessAllBatchesAsync(filePaths);

            // Assert
            Assert.Equal(0, result.TotalAttempted);
            Assert.Equal(0, result.TotalProcessed);
            Assert.Equal(0, result.TotalFailed);
        }

        [Fact]
        public async Task ProcessAllBatchesAsync_AllFilesProcessed_ReturnsSuccess()
        {
            // Arrange
            var filePaths = new List<string> { "file1.txt", "file2.txt" };
            _mockFileProcessor.Setup(p => p.ProcessFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FileProcessingResult { Status = FileProcessingStatus.Success });

            // Act
            var result = await _processor.ProcessAllBatchesAsync(filePaths);

            // Assert
            Assert.Equal(2, result.TotalAttempted);
            Assert.Equal(2, result.TotalProcessed);
            Assert.Equal(0, result.TotalFailed);
        }

        [Fact]
        public async Task ProcessAllBatchesAsync_SomeFilesFail_TracksFailures()
        {
            // Arrange
            var filePaths = new List<string> { "file1.txt", "file2.txt" };
            _mockFileProcessor.Setup(p => p.ProcessFileAsync("file1.txt", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FileProcessingResult { Status = FileProcessingStatus.Success });
            _mockFileProcessor.Setup(p => p.ProcessFileAsync("file2.txt", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FileProcessingResult
                {
                    Status = FileProcessingStatus.Error,
                    Error = "Processing failed"
                });

            // Act
            var result = await _processor.ProcessAllBatchesAsync(filePaths);

            // Assert
            Assert.Equal(2, result.TotalAttempted);
            Assert.Equal(1, result.TotalProcessed);
            Assert.Equal(1, result.TotalFailed);
        }
    }
}
```

### Integration Tests

```csharp
using Xunit;
using ChunkHound.Services;
using System.IO;
using System.Threading.Tasks;

namespace ChunkHound.Services.IntegrationTests
{
    public class BatchProcessorIntegrationTests : IDisposable
    {
        private readonly string _testDir;
        private readonly BatchProcessor _processor;

        public BatchProcessorIntegrationTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "BatchProcessorTest");
            Directory.CreateDirectory(_testDir);

            // Setup with test implementations
            var fileProcessor = new TestFileProcessor();
            var errorHandler = new TestErrorHandler();
            var progressManager = new TestProgressManager();
            _processor = new BatchProcessor(fileProcessor, errorHandler, progressManager);
        }

        [Fact]
        public async Task ProcessAllBatchesAsync_WithTestFiles_ProcessesSuccessfully()
        {
            // Arrange
            var testFile1 = Path.Combine(_testDir, "test1.txt");
            var testFile2 = Path.Combine(_testDir, "test2.txt");
            await File.WriteAllTextAsync(testFile1, "content1");
            await File.WriteAllTextAsync(testFile2, "content2");

            var filePaths = new List<string> { testFile1, testFile2 };

            // Act
            var result = await _processor.ProcessAllBatchesAsync(filePaths);

            // Assert
            Assert.Equal(2, result.TotalAttempted);
            Assert.Equal(2, result.TotalProcessed);
        }

        public void Dispose()
        {
            Directory.Delete(_testDir, true);
        }
    }

    // Test implementations
    public class TestFileProcessor : IFileProcessor
    {
        public async Task<FileProcessingResult> ProcessFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            // Simulate file processing
            await Task.Delay(10, cancellationToken);
            return new FileProcessingResult { Status = FileProcessingStatus.Success };
        }
    }

    public class TestErrorHandler : IErrorHandler
    {
        public Task TrackErrorAsync(string error, bool isPermanent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public bool ShouldAbort() => false;

        public Dictionary<string, int> GetErrorStats() => new();
    }

    public class TestProgressManager : IProgressManager
    {
        public Task IncrementTotalAsync(int increment, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AdvanceAsync(int advance, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateInfoAsync(string info, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
```

## Dependencies

- `ChunkHound.Interfaces.IFileProcessor`
- `ChunkHound.Interfaces.IErrorHandler`
- `ChunkHound.Interfaces.IProgressManager`
- `ChunkHound.Core.Models.FileProcessingResult`
- `Microsoft.Extensions.Logging`
- `System.Threading`
- `System.Diagnostics`

## Notes

- The service uses dynamic batch sizing based on processing time to optimize throughput.
- Parallel processing is controlled by a semaphore to prevent overwhelming system resources.
- Error handling includes both transient and permanent failure classification.
- Progress tracking provides real-time feedback on processing status.
- Cancellation tokens are propagated throughout for cooperative cancellation.
- The design follows dependency injection principles for testability.
- Batch processing adapts to system performance with configurable thresholds.