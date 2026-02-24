using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core;
using ChunkHound.Core.Workers;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Workers;

/// <summary>
/// Worker service that dequeues chunks, generates embeddings in batches,
/// and enqueues chunks with embeddings for storage. This service implements the embedding
/// generation stage of the indexing pipeline with provider-aware batching.
/// </summary>
public class EmbedWorker : PipelineWorker<Chunk, Chunk>
{
    private readonly IEmbeddingProvider _embeddingProvider;

    /// <summary>
    /// Initializes a new instance of the EmbedWorker class.
    /// </summary>
    public EmbedWorker(
        IEmbeddingProvider embeddingProvider,
        ILogger<EmbedWorker>? logger = null,
        WorkerConfig? config = null)
        : base(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EmbedWorker>.Instance, config ?? new WorkerConfig())
    {
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
    }

    /// <summary>
    /// Processes a batch of chunks by generating embeddings.
    /// </summary>
    protected override async Task<IReadOnlyList<Chunk>> ProcessBatchAsync(IReadOnlyList<Chunk> batch, CancellationToken ct)
    {
        var contents = batch.Select(c => c.Content).ToList();
        var embeddings = await _embeddingProvider.EmbedAsync(contents, ct);
        var result = new List<Chunk>();
        for (int i = 0; i < batch.Count; i++)
        {
            var memory = new ReadOnlyMemory<float>(embeddings[i].ToArray());
            var updated = batch[i].WithEmbedding(memory);
            result.Add(updated);
        }
        return result;
    }
}