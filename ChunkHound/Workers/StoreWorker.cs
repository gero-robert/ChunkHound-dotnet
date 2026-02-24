using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core;
using ChunkHound.Core.Extensions;
using ChunkHound.Core.Workers;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Workers;

/// <summary>
/// Worker service that dequeues chunks with embeddings and stores them in the database with batching.
/// This service implements the final stage of the indexing pipeline, ensuring efficient
/// database operations for high-throughput chunk storage.
/// </summary>
public class StoreWorker : PipelineWorker<Chunk, object>
{
    private readonly IDatabaseProvider _databaseProvider;

    /// <summary>
    /// Initializes a new instance of the StoreWorker class.
    /// </summary>
    public StoreWorker(
        IDatabaseProvider databaseProvider,
        ILogger<StoreWorker>? logger = null,
        WorkerConfig? config = null)
        : base(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StoreWorker>.Instance, config ?? new WorkerConfig())
    {
        _databaseProvider = databaseProvider ?? throw new ArgumentNullException(nameof(databaseProvider));
    }

    /// <summary>
    /// Processes a batch of chunks by storing them in the database.
    /// </summary>
    protected override async Task<IReadOnlyList<object>> ProcessBatchAsync(IReadOnlyList<Chunk> batch, CancellationToken ct)
    {
        await _databaseProvider.BatchInsertChunksAsync(batch, 100, ct);
        return Array.Empty<object>();
    }
}