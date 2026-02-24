using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Services;

/// <summary>
/// Service for managing embedding generation, caching, and optimization.
/// Handles batched generation with error recovery and dynamic batch sizing.
/// </summary>
public class EmbeddingService
{
    private readonly IDatabaseProvider _databaseProvider;
    private readonly IEmbeddingProvider? _embeddingProvider;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly EmbeddingErrorHandler _errorHandler;
    private readonly EmbeddingProgressManager _progressManager;
    private readonly EmbeddingBatchProcessor _batchProcessor;

    // Circuit breaker state
    private CircuitBreakerState _circuitBreakerState = CircuitBreakerState.Closed;
    private int _consecutiveFailures = 0;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private readonly int _circuitBreakerFailureThreshold = 5;
    private readonly TimeSpan _circuitBreakerTimeout = TimeSpan.FromMinutes(5);

    // Rate limiting
    private int _requestsThisMinute = 0;
    private DateTime _rateLimitWindowStart = DateTime.UtcNow;
    private readonly int _maxRequestsPerMinute = 60; // Configurable

    /// <summary>
    /// Gets the database provider.
    /// </summary>
    public IDatabaseProvider DatabaseProvider => _databaseProvider;

    /// <summary>
    /// Gets the embedding provider.
    /// </summary>
    public IEmbeddingProvider? EmbeddingProvider => _embeddingProvider;

    /// <summary>
    /// Gets the logger.
    /// </summary>
    public ILogger<EmbeddingService> Logger => _logger;

    /// <summary>
    /// Gets the embedding batch size.
    /// </summary>
    public int EmbeddingBatchSize { get; }

    /// <summary>
    /// Gets the database batch size.
    /// </summary>
    public int DatabaseBatchSize { get; }

    /// <summary>
    /// Gets the maximum concurrent batches.
    /// </summary>
    public int MaxConcurrentBatches { get; }

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
        _databaseProvider = databaseProvider ?? throw new ArgumentNullException(nameof(databaseProvider));
        _embeddingProvider = embeddingProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EmbeddingService>.Instance;

        EmbeddingBatchSize = embeddingBatchSize;
        DatabaseBatchSize = databaseBatchSize;

        // Auto-detect optimal concurrency from provider
        MaxConcurrentBatches = maxConcurrentBatches ??
            (embeddingProvider as dynamic)?.GetRecommendedConcurrency() ?? 8;

        // Initialize internal components
        _errorHandler = new EmbeddingErrorHandler(maxConsecutiveTransientFailures, transientErrorWindow ?? TimeSpan.FromSeconds(300));
        _progressManager = new EmbeddingProgressManager(progress);
        _batchProcessor = new EmbeddingBatchProcessor(this, _errorHandler, _progressManager);
    }

    /// <summary>
    /// Generates embeddings for a list of chunks with progress tracking.
    /// </summary>
    public async Task<EmbeddingResult> GenerateEmbeddingsForChunksAsync(
        IReadOnlyList<Chunk> chunks,
        IProgress<EmbeddingProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_embeddingProvider == null)
        {
            _logger.LogWarning("No embedding provider configured");
            return new EmbeddingResult { TotalGenerated = 0, FailedChunks = 0, PermanentFailures = 0 };
        }

        if (chunks.Count == 0)
        {
            return new EmbeddingResult { TotalGenerated = 0, FailedChunks = 0, PermanentFailures = 0 };
        }

        try
        {
            _logger.LogDebug("Generating embeddings for {Count} chunks", chunks.Count);

            // Filter out chunks that already have embeddings
            var filteredChunks = await FilterExistingEmbeddingsAsync(chunks, cancellationToken);

            if (filteredChunks.Count == 0)
            {
                _logger.LogDebug("All chunks already have embeddings");
                return new EmbeddingResult { TotalGenerated = 0, FailedChunks = 0, PermanentFailures = 0 };
            }

            // Generate embeddings in batches with error recovery
            var result = await GenerateEmbeddingsInBatchesAsync(
                filteredChunks, progress, cancellationToken);

            _logger.LogDebug(
                "Successfully generated {Generated} embeddings (processed: {Processed}, failed: {Failed}, permanent: {Permanent})",
                result.TotalGenerated, result.TotalProcessed, result.FailedChunks, result.PermanentFailures);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings for {Count} chunks", chunks.Count);
            _progressManager.ReportError(ex.Message);
            return new EmbeddingResult
            {
                TotalGenerated = 0,
                FailedChunks = 0,
                PermanentFailures = 0,
                Error = ex.Message
            };
        }
    }

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
            if (_embeddingProvider == null)
            {
                return new RegenerateResult { Status = "error", Error = "No embedding provider configured" };
            }

            // Determine which chunks to regenerate
            var chunksToRegenerate = filePath != null
                ? await _databaseProvider.GetChunksByFilePathAsync(filePath, cancellationToken)
                : await _databaseProvider.GetChunksByIdsAsync(chunkIds ?? Array.Empty<long>(), cancellationToken);

            if (chunksToRegenerate.Count == 0)
            {
                return new RegenerateResult { Status = "complete", Regenerated = 0, Message = "No chunks found" };
            }

            _logger.LogInformation("Regenerating embeddings for {Count} chunks", chunksToRegenerate.Count);

            // Delete existing embeddings
            var providerName = _embeddingProvider.ProviderName;
            var modelName = _embeddingProvider.ModelName;

            await _databaseProvider.DeleteEmbeddingsForChunksAsync(
                chunksToRegenerate.Select(c => long.Parse(c.Id!)).ToList(), providerName, modelName, cancellationToken);

            // Generate new embeddings
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
            _logger.LogError(ex, "Failed to regenerate embeddings");
            return new RegenerateResult { Status = "error", Error = ex.Message };
        }
    }

    private async Task<IReadOnlyList<Chunk>> FilterExistingEmbeddingsAsync(IReadOnlyList<Chunk> chunks, CancellationToken cancellationToken)
    {
        if (_embeddingProvider == null) return chunks;

        var chunkIds = chunks.Where(c => !string.IsNullOrEmpty(c.Id)).Select(c => long.Parse(c.Id!)).ToList();
        if (chunkIds.Count == 0) return chunks;

        var existingIds = await _databaseProvider.FilterExistingEmbeddingsAsync(
            chunkIds, _embeddingProvider.ProviderName, _embeddingProvider.ModelName, cancellationToken);

        return chunks.Where(c => !string.IsNullOrEmpty(c.Id) && !existingIds.Contains(long.Parse(c.Id!))).ToList();
    }

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

        _logger.LogDebug("Processing {Count} token-aware batches", batches.Count);

        // Initialize progress tracking
        _progressManager.Initialize(chunks.Count);

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
                return await _batchProcessor.ProcessBatchWithRetriesAsync(batch, batchNum, errorClassifier, cancellationToken);
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
            allEmbeddingsData.AddRange(batchResult.SuccessfulChunks.Select(c => new EmbeddingData(
                long.Parse(c.Chunk.Id!),
                _embeddingProvider!.ProviderName,
                _embeddingProvider!.ModelName,
                c.Embedding.Count,
                c.Embedding,
                "success"
            )));

            // Track statistics
            var batchSuccessful = batchResult.SuccessfulChunks.Count;
            var batchFailed = batchResult.FailedChunks.Count;
            var batchSize = batchSuccessful + batchFailed;

            totalGenerated += batchSuccessful;
            failedChunks += batchResult.FailedChunks.Count(f => f.Classification == EmbeddingErrorClassification.Transient);
            permanentFailures += batchResult.FailedChunks.Count(f => f.Classification == EmbeddingErrorClassification.Permanent);

            // Report batch progress
            _progressManager.ReportBatchProgress(batchResult.BatchNum, batchSize, batchSuccessful, batchFailed);

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

                allErrorSamples[errorType].AddRange(samples.Take(5 - allErrorSamples[errorType].Count));
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
                chunkIdToStatus[long.Parse(chunk.Id!)] = status;
            }
        }

        // Insert successful embeddings and update failed chunks
        if (allEmbeddingsData.Count != 0 || chunkIdToStatus.Count != 0)
        {
            _logger.LogDebug("Inserting {Embeddings} embeddings and updating status for {Failed} failed chunks",
                allEmbeddingsData.Count, chunkIdToStatus.Count);

            await _databaseProvider.InsertEmbeddingsBatchAsync(allEmbeddingsData, chunkIdToStatus, cancellationToken);

            // Run optimization after bulk insert
            await _databaseProvider.OptimizeTablesAsync(cancellationToken);
        }

        totalProcessed = totalGenerated + failedChunks + permanentFailures;

        _logger.LogDebug(
            "GenerateEmbeddingsInBatches completed: generated={Generated}, processed={Processed}, failed={Failed}, permanent={Permanent}, retries={Retries}",
            totalGenerated, totalProcessed, failedChunks, permanentFailures, retryAttempts);

        // Report completion
        _progressManager.ReportCompletion(totalGenerated, failedChunks + permanentFailures);

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



    /// <summary>
    /// Creates batches that respect provider token limits using provider-agnostic logic.
    /// </summary>
    private List<List<Chunk>> CreateTokenAwareBatches(IReadOnlyList<Chunk> chunks)
    {
        if (!chunks.Any()) return new List<List<Chunk>>();
        if (_embeddingProvider == null)
        {
            // Simple batching without provider
            const int defaultBatchSize = 20;
            var defaultBatches = new List<List<Chunk>>();
            for (var i = 0; i < chunks.Count; i += defaultBatchSize)
            {
                defaultBatches.Add(chunks.Skip(i).Take(defaultBatchSize).ToList());
            }
            return defaultBatches;
        }

        const int maxChunksPerBatch = 300;
        var maxTokens = _embeddingProvider.GetMaxTokensPerBatch();
        var maxDocuments = _embeddingProvider.GetMaxDocumentsPerBatch();
        var safeLimit = (int)(maxTokens * 0.80);

        var batches = new List<List<Chunk>>();
        var currentBatch = new List<Chunk>();
        var currentTokens = 0;

        foreach (var chunk in chunks)
        {
            var textTokens = EstimateTokens(chunk.Code, _embeddingProvider.ProviderName, _embeddingProvider.ModelName);

            // Check if adding this chunk would exceed limits
            if ((currentTokens + textTokens > safeLimit && currentBatch.Count != 0) ||
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

        if (currentBatch.Count != 0) batches.Add(currentBatch);

        _logger.LogInformation(
            "Created {BatchCount} batches for {ChunkCount} chunks (concurrency limit: {Concurrency})",
            batches.Count, chunks.Count, MaxConcurrentBatches);

        return batches;
    }

    private static int EstimateTokens(string text, string provider, string model)
    {
        // Simple estimation: ~4 characters per token
        return text.Length / 4;
    }

    /// <summary>
    /// Checks if the circuit breaker allows requests to proceed.
    /// </summary>
    internal bool IsCircuitBreakerClosed()
    {
        if (_circuitBreakerState == CircuitBreakerState.Closed)
        {
            return true;
        }

        if (_circuitBreakerState == CircuitBreakerState.Open)
        {
            // Check if timeout has passed to transition to half-open
            if (DateTime.UtcNow - _lastFailureTime > _circuitBreakerTimeout)
            {
                _circuitBreakerState = CircuitBreakerState.HalfOpen;
                _logger.LogInformation("Circuit breaker transitioning to Half-Open state");
                return true;
            }
            return false;
        }

        // Half-open: allow one request to test
        return true;
    }

    /// <summary>
    /// Records a successful request, potentially closing the circuit breaker.
    /// </summary>
    internal void RecordSuccess()
    {
        if (_circuitBreakerState == CircuitBreakerState.HalfOpen)
        {
            _circuitBreakerState = CircuitBreakerState.Closed;
            _consecutiveFailures = 0;
            _logger.LogInformation("Circuit breaker closed after successful test request");
        }
    }

    /// <summary>
    /// Records a failure, potentially opening the circuit breaker.
    /// </summary>
    internal void RecordFailure()
    {
        _consecutiveFailures++;
        _lastFailureTime = DateTime.UtcNow;

        if (_circuitBreakerState == CircuitBreakerState.HalfOpen ||
            _consecutiveFailures >= _circuitBreakerFailureThreshold)
        {
            _circuitBreakerState = CircuitBreakerState.Open;
            _logger.LogWarning("Circuit breaker opened due to {ConsecutiveFailures} consecutive failures", _consecutiveFailures);
        }
    }

    /// <summary>
    /// Checks if rate limiting allows the request to proceed.
    /// </summary>
    internal bool CheckRateLimit()
    {
        var now = DateTime.UtcNow;

        // Reset window if minute has passed
        if ((now - _rateLimitWindowStart).TotalMinutes >= 1)
        {
            _requestsThisMinute = 0;
            _rateLimitWindowStart = now;
        }

        if (_requestsThisMinute >= _maxRequestsPerMinute)
        {
            _logger.LogWarning("Rate limit exceeded: {Requests} requests in current minute", _requestsThisMinute);
            return false;
        }

        _requestsThisMinute++;
        return true;
    }
}

// Circuit breaker state enum
internal enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

// Internal classes

/// <summary>
/// Handles error classification for embedding operations.
/// </summary>
internal class EmbeddingErrorClassifier
{
    public EmbeddingErrorClassification ClassifyError(Exception ex)
    {
        // Check exception type first
        if (ex is TimeoutException || ex is OperationCanceledException)
        {
            return EmbeddingErrorClassification.Transient;
        }

        if (ex is System.Net.Http.HttpRequestException httpEx)
        {
            // Check HTTP status code if available
            if (httpEx.StatusCode.HasValue)
            {
                var status = (int)httpEx.StatusCode.Value;
                if (status >= 500) // Server errors
                {
                    return EmbeddingErrorClassification.Transient;
                }
                if (status == 429) // Too Many Requests
                {
                    return EmbeddingErrorClassification.Transient;
                }
                if (status >= 400 && status < 500) // Client errors
                {
                    return EmbeddingErrorClassification.Permanent;
                }
            }
            // Network-related errors are often transient
            return EmbeddingErrorClassification.Transient;
        }

        // Check message content for known transient patterns
        var message = ex.Message.ToLowerInvariant();
        if (message.Contains("timeout") ||
            message.Contains("timed out") ||
            message.Contains("rate limit") ||
            message.Contains("throttle") ||
            message.Contains("service unavailable") ||
            message.Contains("temporarily unavailable") ||
            message.Contains("connection") && (message.Contains("reset") || message.Contains("closed")) ||
            message.Contains("circuit breaker"))
        {
            return EmbeddingErrorClassification.Transient;
        }

        // Check inner exceptions
        if (ex.InnerException != null)
        {
            return ClassifyError(ex.InnerException);
        }

        // Default to permanent for unknown errors
        return EmbeddingErrorClassification.Permanent;
    }
}

/// <summary>
/// Handles error recovery and tracking.
/// </summary>
internal class EmbeddingErrorHandler
{
    private readonly int _maxConsecutiveTransientFailures;
    private readonly TimeSpan _transientErrorWindow;
    private readonly List<DateTime> _transientFailureTimes = new();
    private int _consecutiveTransientFailures = 0;

    public EmbeddingErrorHandler(int maxConsecutiveTransientFailures, TimeSpan transientErrorWindow)
    {
        _maxConsecutiveTransientFailures = maxConsecutiveTransientFailures;
        _transientErrorWindow = transientErrorWindow;
    }

    /// <summary>
    /// Records a transient failure and checks if recovery should be attempted.
    /// </summary>
    public bool ShouldAttemptRecovery(EmbeddingErrorClassification classification)
    {
        if (classification != EmbeddingErrorClassification.Transient)
        {
            return false; // Permanent failures don't get recovery attempts
        }

        var now = DateTime.UtcNow;

        // Clean old failures outside the window
        _transientFailureTimes.RemoveAll(t => (now - t) > _transientErrorWindow);

        _transientFailureTimes.Add(now);
        _consecutiveTransientFailures = _transientFailureTimes.Count;

        return _consecutiveTransientFailures < _maxConsecutiveTransientFailures;
    }

    /// <summary>
    /// Records a successful operation, resetting failure counters.
    /// </summary>
    public void RecordSuccess()
    {
        _consecutiveTransientFailures = 0;
        _transientFailureTimes.Clear();
    }

    /// <summary>
    /// Gets the current count of consecutive transient failures.
    /// </summary>
    public int ConsecutiveTransientFailures => _consecutiveTransientFailures;

    /// <summary>
    /// Checks if the system is in a failure state that requires intervention.
    /// </summary>
    public bool IsInFailureState() => _consecutiveTransientFailures >= _maxConsecutiveTransientFailures;
}

/// <summary>
/// Manages progress reporting.
/// </summary>
internal class EmbeddingProgressManager
{
    private readonly IProgress<EmbeddingProgressInfo>? _progress;
    private int _totalChunks;
    private int _processedChunks;

    public EmbeddingProgressManager(IProgress<EmbeddingProgressInfo>? progress)
    {
        _progress = progress;
    }

    /// <summary>
    /// Initializes progress tracking with total number of chunks.
    /// </summary>
    public void Initialize(int totalChunks)
    {
        _totalChunks = totalChunks;
        _processedChunks = 0;
        ReportProgress("Starting embedding generation", 0.0);
    }

    /// <summary>
    /// Reports progress for a batch completion.
    /// </summary>
    public void ReportBatchProgress(int batchNum, int batchSize, int successful, int failed)
    {
        _processedChunks += batchSize;
        var progress = _totalChunks > 0 ? (double)_processedChunks / _totalChunks : 0.0;
        var message = $"Processed batch {batchNum + 1}: {successful} successful, {failed} failed";
        ReportProgress(message, progress);
    }

    /// <summary>
    /// Reports completion of all processing.
    /// </summary>
    public void ReportCompletion(int totalGenerated, int totalFailed)
    {
        ReportProgress($"Completed: {totalGenerated} generated, {totalFailed} failed", 1.0);
    }

    /// <summary>
    /// Reports an error occurred.
    /// </summary>
    public void ReportError(string errorMessage)
    {
        var progress = _totalChunks > 0 ? (double)_processedChunks / _totalChunks : 0.0;
        ReportProgress($"Error: {errorMessage}", progress);
    }

    private void ReportProgress(string message, double progress)
    {
        _progress?.Report(new EmbeddingProgressInfo(progress, message, _processedChunks, _totalChunks));
    }
}

/// <summary>
/// Handles batch processing logic.
/// </summary>
internal class EmbeddingBatchProcessor
{
    private readonly EmbeddingService _service;
    private readonly EmbeddingErrorHandler _errorHandler;
    private readonly EmbeddingProgressManager _progressManager;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingBatchProcessor(EmbeddingService service, EmbeddingErrorHandler errorHandler, EmbeddingProgressManager progressManager)
    {
        _service = service;
        _errorHandler = errorHandler;
        _progressManager = progressManager;
        _logger = service.Logger;
    }

    /// <summary>
    /// Processes a batch with intelligent retry logic for transient failures.
    /// </summary>
    public async Task<BatchResult> ProcessBatchWithRetriesAsync(
        List<Chunk> batch,
        int batchNum,
        EmbeddingErrorClassifier errorClassifier,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        var result = new BatchResult { BatchNum = batchNum };
        var currentBatch = batch;
        var attempt = 0;

        while (attempt < maxRetries && currentBatch.Count != 0)
        {
            attempt++;
            if (attempt > 1)
            {
                result.RetryAttempts++;
                _logger.LogDebug("Batch {BatchNum} retry attempt {Attempt}/{MaxRetries}", batchNum, attempt, maxRetries);
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
            if (transientFailures.Count != 0 && attempt < maxRetries)
            {
                currentBatch = transientFailures;
                continue;
            }
            else
            {
                // Max retries reached or no transient failures
                result.FailedChunks.AddRange(transientFailures.Select(c => (c, "Transient failure after retries", EmbeddingErrorClassification.Transient)));
                currentBatch = new List<Chunk>(); // Clear to stop retrying
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

    /// <summary>
    /// Processes a single batch of chunks.
    /// </summary>
    private async Task<BatchResult> ProcessSingleBatchAsync(
        List<Chunk> batch,
        int batchNum,
        EmbeddingErrorClassifier errorClassifier,
        CancellationToken cancellationToken)
    {
        var result = new BatchResult { BatchNum = batchNum };
        var texts = batch.Select(c => c.Code).ToList();

        // Check circuit breaker
        if (!_service.IsCircuitBreakerClosed())
        {
            var ex = new InvalidOperationException("Circuit breaker is open - embedding provider temporarily unavailable");
            var classification = EmbeddingErrorClassification.Transient;
            var errorType = ex.GetType().Name;

            result.ErrorStats[errorType] = result.ErrorStats.GetValueOrDefault(errorType) + 1;
            result.ErrorSamples[errorType] = result.ErrorSamples.GetValueOrDefault(errorType, new List<string>());
            result.ErrorSamples[errorType].Add(ex.Message);

            foreach (var chunk in batch)
            {
                result.FailedChunks.Add((chunk, ex.Message, classification));
            }
            return result;
        }

        // Check rate limit
        if (!_service.CheckRateLimit())
        {
            var ex = new InvalidOperationException("Rate limit exceeded - too many requests per minute");
            var classification = EmbeddingErrorClassification.Transient;
            var errorType = ex.GetType().Name;

            result.ErrorStats[errorType] = result.ErrorStats.GetValueOrDefault(errorType) + 1;
            result.ErrorSamples[errorType] = result.ErrorSamples.GetValueOrDefault(errorType, new List<string>());
            result.ErrorSamples[errorType].Add(ex.Message);

            foreach (var chunk in batch)
            {
                result.FailedChunks.Add((chunk, ex.Message, classification));
            }
            return result;
        }

        try
        {
            var embeddings = await _service.EmbeddingProvider!.EmbedAsync(texts, cancellationToken);

            if (embeddings.Count != batch.Count)
            {
                throw new InvalidOperationException($"Embedding count mismatch: expected {batch.Count}, got {embeddings.Count}");
            }

            for (var i = 0; i < batch.Count; i++)
            {
                result.SuccessfulChunks.Add((batch[i], embeddings[i]));
            }

            // Record success for circuit breaker
            _service.RecordSuccess();
        }
        catch (Exception ex)
        {
            // Record failure for circuit breaker
            _service.RecordFailure();

            var classification = errorClassifier.ClassifyError(ex);
            var errorType = ex.GetType().Name;

            result.ErrorStats[errorType] = result.ErrorStats.GetValueOrDefault(errorType) + 1;
            result.ErrorSamples[errorType] = result.ErrorSamples.GetValueOrDefault(errorType, new List<string>());
            result.ErrorSamples[errorType].Add(ex.Message);

            // All chunks in batch failed
            foreach (var chunk in batch)
            {
                result.FailedChunks.Add((chunk, ex.Message, classification));
            }
        }

        return result;
    }
}