namespace ChunkHound.Core;

/// <summary>
/// Interface for database providers that store and retrieve code chunks.
/// </summary>
// **** ALL DB OPERATIONS MUST BE BATCH OPERATIONS, NEVER SINGLE ITEM.
public interface IDatabaseProvider
{
    /// <summary>
    /// Initializes the database schema.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Stores a batch of chunks in the database.
    /// </summary>
    /// <param name="chunks">The chunks to store.</param>
    /// <returns>The IDs of the stored chunks.</returns>
    Task<List<int>> StoreChunksAsync(List<Chunk> chunks);

    /// <summary>
    /// Retrieves chunks by their content hashes.
    /// </summary>
    /// <param name="hashes">The content hashes to search for.</param>
    /// <returns>The matching chunks.</returns>
    Task<List<Chunk>> GetChunksByHashesAsync(List<string> hashes);

    /// <summary>
    /// Inserts a batch of chunks into the database.
    /// </summary>
    /// <param name="chunks">The chunks to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The IDs of the inserted chunks.</returns>
    Task<List<int>> InsertChunksBatchAsync(List<Chunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a batch of embeddings associated with chunk IDs.
    /// </summary>
    /// <param name="chunkIds">The chunk IDs.</param>
    /// <param name="embeddings">The embeddings to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of embeddings inserted.</returns>
    Task<int> InsertEmbeddingsBatchAsync(List<int> chunkIds, List<List<float>> embeddings, CancellationToken cancellationToken = default);
}