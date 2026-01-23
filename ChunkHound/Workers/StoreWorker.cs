using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core;
using ChunkHound.Core.Workers;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Workers;

/// <summary>
/// Worker service that dequeues embed chunks and stores them in the database with batching and locking.
/// This service implements the final stage of the indexing pipeline, ensuring efficient and thread-safe
/// database operations for high-throughput chunk storage.
/// </summary>
public class StoreWorker : WorkerBase, IStoreWorker, IDisposable
{
    private readonly IDatabaseProvider _databaseProvider;
    private readonly ConcurrentQueue<EmbedChunk> _embedChunksQueue;
    private readonly ReaderWriterLockSlim _dbLock;
    private readonly WorkerConfig _config;

    /// <summary>
    /// Initializes a new instance of the StoreWorker class.
    /// </summary>
    public StoreWorker(
        IDatabaseProvider databaseProvider,
        ConcurrentQueue<EmbedChunk> embedChunksQueue,
        ReaderWriterLockSlim dbLock,
        ILogger<StoreWorker>? logger = null,
        WorkerConfig? config = null)
        : base(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StoreWorker>.Instance)
    {
        _databaseProvider = databaseProvider ?? throw new ArgumentNullException(nameof(databaseProvider));
        _embedChunksQueue = embedChunksQueue ?? throw new ArgumentNullException(nameof(embedChunksQueue));
        _dbLock = dbLock ?? throw new ArgumentNullException(nameof(dbLock));
        _config = config ?? new WorkerConfig();
    }

    /// <summary>
    /// Executes the main processing loop for the store worker.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var batch = new List<EmbedChunk>();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Collect batch
                while (batch.Count < _config.BatchSize && _embedChunksQueue.TryDequeue(out var embedChunk))
                {
                    batch.Add(embedChunk);
                }

                if (batch.Count != 0)
                {
                    await ProcessBatchAsync(batch, cancellationToken);
                    batch.Clear();
                }
                else
                {
                    // Small delay to prevent busy waiting
                    await Task.Delay(_config.BusyWaitDelayMs, cancellationToken);
                }
            }
        }
        finally
        {
            // Process any remaining items in batch
            if (batch.Count != 0)
            {
                try
                {
                    await ProcessBatchAsync(batch, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process final batch during shutdown");
                }
            }
        }
    }

    /// <summary>
    /// Processes a batch of embed chunks with retry logic and database locking.
    /// </summary>
    private async Task ProcessBatchAsync(List<EmbedChunk> batch, CancellationToken cancellationToken)
    {
        var attempt = 0;
        var delayMs = _config.RetryInitialDelayMs;

        while (attempt <= _config.MaxRetries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ExecuteWithLockAsync(async () =>
                {
                    var chunks = batch.Select(ec => ec.Chunk).ToList();
                    var embeddings = batch.Select(ec => ec.Embedding).ToList();

                    // Insert chunks and get their IDs
                    var chunkIds = await _databaseProvider.InsertChunksBatchAsync(chunks, cancellationToken);

                    // Associate embeddings with chunk IDs
                    await _databaseProvider.InsertEmbeddingsBatchAsync(chunkIds, embeddings, cancellationToken);

                    _logger.LogDebug("Stored batch of {Count} chunks with embeddings", batch.Count);
                }, cancellationToken);

                return; // Success
            }
            catch (Exception ex) when (attempt < _config.MaxRetries)
            {
                attempt++;
                _logger.LogWarning(ex, "Batch storage attempt {Attempt}/{MaxRetries} failed, retrying in {Delay}ms",
                    attempt, _config.MaxRetries, delayMs);

                await Task.Delay(delayMs, cancellationToken);
                delayMs = Math.Min(delayMs * 2, _config.MaxRetryDelayMs); // Exponential backoff
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch storage failed permanently after {MaxRetries} attempts", _config.MaxRetries);
                throw;
            }
        }
    }

    /// <summary>
    /// Executes database operations with write lock for thread safety.
    /// </summary>
    private async Task ExecuteWithLockAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        _dbLock.EnterWriteLock();
        try
        {
            await operation();
        }
        finally
        {
            _dbLock.ExitWriteLock();
        }
    }
}