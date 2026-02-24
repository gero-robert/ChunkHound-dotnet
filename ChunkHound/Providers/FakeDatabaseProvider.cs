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
            var chunkWithId = !string.IsNullOrEmpty(chunk.Id) ? chunk : chunk with { Id = (Interlocked.Increment(ref _nextId) - 1).ToString() };

            _chunksByHash[hash] = chunkWithId;
            var idInt = int.Parse(chunkWithId.Id);
            _chunksById[idInt] = chunkWithId;
            ids.Add(idInt);
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

    /// <summary>
    /// Inserts a batch of chunks into the in-memory database.
    /// </summary>
    /// <param name="chunks">The chunks to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The IDs of the inserted chunks.</returns>
    public async Task<List<int>> InsertChunksBatchAsync(List<Core.Chunk> chunks, CancellationToken cancellationToken = default)
    {
        return await StoreChunksAsync(chunks);
    }

    /// <summary>
    /// Inserts a batch of embeddings associated with chunk IDs.
    /// </summary>
    /// <param name="chunkIds">The chunk IDs.</param>
    /// <param name="embeddings">The embeddings to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of embeddings inserted.</returns>
    public async Task<int> InsertEmbeddingsBatchAsync(List<int> chunkIds, List<List<float>> embeddings, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken); // Simulate minimal latency
        return embeddings.Count;
    }

    /// <summary>
    /// Filters out chunks that already have embeddings.
    /// </summary>
    public async Task<List<long>> FilterExistingEmbeddingsAsync(List<long> chunkIds, string providerName, string modelName, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        return new List<long>(); // Assume none exist for fake provider
    }

    /// <summary>
    /// Inserts a batch of embeddings with metadata.
    /// </summary>
    public async Task InsertEmbeddingsBatchAsync(List<Core.EmbeddingData> embeddingsData, Dictionary<long, string> chunkIdToStatus, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
    }

    /// <summary>
    /// Deletes embeddings for specific chunks.
    /// </summary>
    public async Task DeleteEmbeddingsForChunksAsync(List<long> chunkIds, string providerName, string modelName, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
    }

    /// <summary>
    /// Gets chunks by file path.
    /// </summary>
    public async Task<List<Core.Chunk>> GetChunksByFilePathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        return _chunksById.Values.Where(c => c.FilePath == filePath).ToList();
    }

    /// <summary>
    /// Gets chunks by IDs.
    /// </summary>
    public async Task<List<Core.Chunk>> GetChunksByIdsAsync(IReadOnlyList<long> chunkIds, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        return chunkIds.Where(id => _chunksById.ContainsKey((int)id)).Select(id => _chunksById[(int)id]).ToList();
    }

    /// <summary>
    /// Gets a file by its path.
    /// </summary>
    public async Task<Core.File?> GetFileByPathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        return null; // Fake provider doesn't store files
    }

    /// <summary>
    /// Inserts or updates a file record.
    /// </summary>
    public async Task<int> UpsertFileAsync(Core.File file, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        return file.Id ?? 1; // Return existing ID or dummy ID
    }

    /// <summary>
    /// Optimizes database tables.
    /// </summary>
    public async Task OptimizeTablesAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
    }

    /// <summary>
    /// Clears all data by clearing in-memory collections.
    /// </summary>
    public Task ClearAllDataAsync()
    {
        _chunksByHash.Clear();
        _chunksById.Clear();
        _nextId = 1;
        return Task.CompletedTask;
    }
}