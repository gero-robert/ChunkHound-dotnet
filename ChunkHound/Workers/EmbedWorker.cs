using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core;
using ChunkHound.Core.Workers;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Workers;

/// <summary>
/// Worker service that dequeues chunks, generates embeddings in batches,
/// and enqueues embed chunks for storage. This service implements the embedding
/// generation stage of the indexing pipeline with provider-aware batching.
/// </summary>
public class EmbedWorker : WorkerBase, IEmbedWorker, IDisposable
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ConcurrentQueue<Chunk> _chunksQueue;
    private readonly ConcurrentQueue<EmbedChunk> _embedChunksQueue;
    private readonly WorkerConfig _config;

    /// <summary>
    /// Initializes a new instance of the EmbedWorker class.
    /// </summary>
    public EmbedWorker(
        IEmbeddingProvider embeddingProvider,
        ConcurrentQueue<Chunk> chunksQueue,
        ConcurrentQueue<EmbedChunk> embedChunksQueue,
        ILogger<EmbedWorker>? logger = null,
        WorkerConfig? config = null)
        : base(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EmbedWorker>.Instance)
    {
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _chunksQueue = chunksQueue ?? throw new ArgumentNullException(nameof(chunksQueue));
        _embedChunksQueue = embedChunksQueue ?? throw new ArgumentNullException(nameof(embedChunksQueue));
        _config = config ?? new WorkerConfig();
    }

    /// <summary>
    /// Executes the main processing loop for the embed worker.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var batch = new List<Chunk>();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Collect batch
                while (batch.Count < _config.BatchSize && _chunksQueue.TryDequeue(out var chunk))
                {
                    batch.Add(chunk);
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
    /// Processes a batch of chunks by generating embeddings and enqueuing results.
    /// </summary>
    private async Task ProcessBatchAsync(List<Chunk> batch, CancellationToken cancellationToken)
    {
        try
        {
            var texts = batch.Select(c => c.Code).ToList();
            var embeddings = await _embeddingProvider.EmbedAsync(texts, cancellationToken);

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
                    _embeddingProvider.ProviderName,
                    _embeddingProvider.ModelName);

                _embedChunksQueue.Enqueue(embedChunk);
            }

            _logger.LogDebug("Processed batch of {Count} chunks with embeddings", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process batch of {Count} chunks", batch.Count);
            throw;
        }
    }


}