using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Services
{
    /// <summary>
    /// Handles the complex batch processing loop for parallel file processing with dynamic sizing and error handling.
    /// </summary>
    public class BatchProcessor
    {
        private readonly IFileProcessor _fileProcessor;
        private readonly IErrorHandler _errorHandler;
        private readonly IProgressManager _progressManager;
        private readonly ILogger<BatchProcessor> _logger;

        /// <summary>
        /// Initializes a new instance of the BatchProcessor class.
        /// </summary>
        public BatchProcessor(
            IFileProcessor fileProcessor,
            IErrorHandler errorHandler,
            IProgressManager progressManager,
            ILogger<BatchProcessor>? logger = null)
        {
            _fileProcessor = fileProcessor ?? throw new ArgumentNullException(nameof(fileProcessor));
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            _progressManager = progressManager ?? throw new ArgumentNullException(nameof(progressManager));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BatchProcessor>.Instance;
        }

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

        /// <summary>
        /// Result of processing a single batch.
        /// </summary>
        private record BatchResult
        {
            public int ProcessedCount { get; init; }
            public int FailedCount { get; init; }
            public int PermanentFailureCount { get; init; }
        }
    }
}