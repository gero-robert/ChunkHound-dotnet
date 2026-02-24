using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
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
    private readonly Channel<Chunk> _chunksChannel;
    private readonly Channel<EmbedChunk> _embedChunksChannel;
    private readonly WorkerConfig _config;

    /// <summary>
    /// Initializes a new instance of the EmbedWorker class.
    /// </summary>
    public EmbedWorker(
        IEmbeddingProvider embeddingProvider,
        Channel<Chunk> chunksChannel,
        Channel<EmbedChunk> embedChunksChannel,
        ILogger<EmbedWorker>? logger = null,
        WorkerConfig? config = null)
        : base(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EmbedWorker>.Instance)
    {
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _chunksChannel = chunksChannel ?? throw new ArgumentNullException(nameof(chunksChannel));
        _embedChunksChannel = embedChunksChannel ?? throw new ArgumentNullException(nameof(embedChunksChannel));
        _config = config ?? new WorkerConfig();
    }

    /// <summary>
    /// Executes the main processing loop for the embed worker.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var reader = _chunksChannel.Reader;
        var writer = _embedChunksChannel.Writer;
        try
        {
            while (await reader.WaitToReadAsync(cancellationToken))
            {
                var batch = new List<Chunk>();
                while (batch.Count < _config.BatchSize && reader.TryRead(out var chunk))
                {
                    batch.Add(chunk);
                }
                if (batch.Count > 0)
                {
                    var processed = await ProcessBatchAsync(batch, cancellationToken);
                    foreach (var embedChunk in processed)
                    {
                        await writer.WriteAsync(embedChunk, cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not ObjectDisposedException)
        {
            _logger.LogError(ex, "{WorkerName} failed - continuing", nameof(EmbedWorker));
            writer.TryComplete(ex);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    /// <summary>
    /// Processes a batch of chunks by generating embeddings and returning results.
    /// </summary>
    private async Task<List<EmbedChunk>> ProcessBatchAsync(List<Chunk> batch, CancellationToken cancellationToken)
    {
        const int MAX_RETRIES = 3;
        var delay = TimeSpan.FromMilliseconds(100);
        for (int r = 0; r < MAX_RETRIES; r++)
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

                // Create embed chunks
                var embedChunks = new List<EmbedChunk>();
                for (var i = 0; i < batch.Count; i++)
                {
                    var embedChunk = new EmbedChunk(
                        batch[i],
                        embeddings[i],
                        _embeddingProvider.ProviderName,
                        _embeddingProvider.ModelName);
                    embedChunks.Add(embedChunk);
                }

                _logger.LogDebug("Processed batch of {Count} chunks with embeddings", batch.Count);
                return embedChunks;
            }
            catch (Exception ex) when (r < MAX_RETRIES - 1)
            {
                _logger.LogWarning(ex, "Embed API retry {Attempt}/{Max}", r + 1, MAX_RETRIES);
                await Task.Delay(delay * (r + 1), cancellationToken);
            }
        }
        _logger.LogError("Embed API failed after {MaxRetries} retries", MAX_RETRIES);
        return new List<EmbedChunk>(); // skip batch
    }


}