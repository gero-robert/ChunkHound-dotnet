using System.Collections.Concurrent;

namespace ChunkHound.Providers;

/// <summary>
/// Fake database provider using in-memory storage for isolated testing.
/// Implements IDatabaseProvider with ConcurrentDictionary for thread-safe operations.
/// </summary>
public class FakeDatabaseProvider : Core.IDatabaseProvider
{
    private readonly ConcurrentDictionary<string, Core.Chunk> _chunksByHash = new();
    private readonly ConcurrentDictionary<int, Core.Chunk> _chunksById = new();
    private int _nextId = 1;

    /// <summary>
    /// Initializes the database schema (no-op for in-memory provider).
    /// </summary>
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stores a batch of chunks in the in-memory database.
    /// Assigns IDs to chunks that don't have them.
    /// </summary>
    /// <param name="chunks">The chunks to store.</param>
    /// <returns>The IDs of the stored chunks.</returns>
    public async Task<List<int>> StoreChunksAsync(List<Core.Chunk> chunks)
    {
        var ids = new List<int>();

        foreach (var chunk in chunks)
        {
            var hash = Core.Utilities.HashUtility.ComputeContentHash(chunk.Code);
            var chunkWithId = chunk.Id.HasValue ? chunk : chunk with { Id = Interlocked.Increment(ref _nextId) - 1 };

            _chunksByHash[hash] = chunkWithId;
            _chunksById[chunkWithId.Id!.Value] = chunkWithId;
            ids.Add(chunkWithId.Id!.Value);
        }

        await Task.Delay(1); // Simulate minimal latency
        return ids;
    }

    /// <summary>
    /// Retrieves chunks by their content hashes.
    /// </summary>
    /// <param name="hashes">The content hashes to search for.</param>
    /// <returns>The matching chunks.</returns>
    public async Task<List<Core.Chunk>> GetChunksByHashesAsync(List<string> hashes)
    {
        var result = new List<Core.Chunk>();

        foreach (var hash in hashes)
        {
            if (_chunksByHash.TryGetValue(hash, out var chunk))
            {
                result.Add(chunk);
            }
        }

        await Task.Delay(1); // Simulate minimal latency
        return result;
    }


}