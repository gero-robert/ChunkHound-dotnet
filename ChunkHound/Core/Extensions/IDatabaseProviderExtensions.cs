using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core;

namespace ChunkHound.Core.Extensions;

/// <summary>
/// Extension methods for IDatabaseProvider.
/// </summary>
public static class IDatabaseProviderExtensions
{
    /// <summary>
    /// Batch inserts chunks into the database.
    /// </summary>
    /// <param name="db">The database provider.</param>
    /// <param name="chunks">The chunks to insert.</param>
    /// <param name="batchSize">The batch size.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task BatchInsertChunksAsync(this IDatabaseProvider db, IReadOnlyList<Chunk> chunks, int batchSize = 100, CancellationToken ct = default)
    {
        var batches = chunks.Chunk(batchSize);
        foreach (var b in batches)
        {
            await db.InsertChunksBatchAsync(b.ToList(), ct);
        }
    }
}