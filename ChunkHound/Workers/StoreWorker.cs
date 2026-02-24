using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
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
    private readonly Channel<EmbedChunk> _embedChunksChannel;
    private readonly ReaderWriterLockSlim _dbLock;
    private readonly WorkerConfig _config;

    /// <summary>
    /// Initializes a new instance of the StoreWorker class.
    /// </summary>
    public StoreWorker(
        IDatabaseProvider databaseProvider,
        Channel<EmbedChunk> embedChunksChannel,
        ReaderWriterLockSlim dbLock,
        ILogger<StoreWorker>? logger = null,
        WorkerConfig? config = null)
        : base(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StoreWorker>.Instance)
    {
        _databaseProvider = databaseProvider ?? throw new ArgumentNullException(nameof(databaseProvider));
        _embedChunksChannel = embedChunksChannel ?? throw new ArgumentNullException(nameof(embedChunksChannel));
        _dbLock = dbLock ?? throw new ArgumentNullException(nameof(dbLock));
        _config = config ?? new WorkerConfig();
    }

    /// <summary>
    /// Executes the main processing loop for the store worker.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var reader = _embedChunksChannel.Reader;
        try
        {
            while (await reader.WaitToReadAsync(cancellationToken))
            {
                var batch = new List<EmbedChunk>();
                while (batch.Count < _config.BatchSize && reader.TryRead(out var embedChunk))
                {
                    batch.Add(embedChunk);
                }
                if (batch.Count > 0)
                {
                    await ProcessBatchAsync(batch, cancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not ObjectDisposedException)
        {
            _logger.LogError(ex, "{WorkerName} failed - continuing", nameof(StoreWorker));
        }
    }

    /// <summary>
    /// Processes a batch of embed chunks with retry logic and database locking.
    /// </summary>
    private async Task ProcessBatchAsync(List<EmbedChunk> batch, CancellationToken cancellationToken)
    {
        await UpsertWithRetryAsync(batch, cancellationToken);
    }

    private async Task UpsertWithRetryAsync(IReadOnlyList<EmbedChunk> embedChunks, CancellationToken ct)
    {
        const int MAX_RETRIES = 3;
        var delay = TimeSpan.FromMilliseconds(100);
        for (int r = 0; r < MAX_RETRIES; r++)
        {
            try
            {
                await ExecuteWithLockAsync(async () =>
                {
                    var chunks = embedChunks.Select(ec => ec.Chunk).ToList();
                    var embeddings = embedChunks.Select(ec => ec.Embedding).ToList();

                    // Insert chunks and get their IDs
                    var chunkIds = await _databaseProvider.InsertChunksBatchAsync(chunks, ct);

                    // Associate embeddings with chunk IDs
                    await _databaseProvider.InsertEmbeddingsBatchAsync(chunkIds, embeddings, ct);

                    _logger.LogDebug("Stored batch of {Count} chunks with embeddings", embedChunks.Count);
                }, ct);
                return;
            }
            catch (Exception ex) when (r < MAX_RETRIES - 1)
            {
                _logger.LogWarning(ex, "DB upsert retry {Attempt}/{Max}", r + 1, MAX_RETRIES);
                await Task.Delay(delay * (r + 1), ct);
            }
        }
        _logger.LogError("DB upsert failed after {MaxRetries} retries", MAX_RETRIES);
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