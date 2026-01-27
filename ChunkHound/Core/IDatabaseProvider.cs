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

    /// <summary>
    /// Filters out chunks that already have embeddings.
    /// </summary>
    /// <param name="chunkIds">The chunk IDs to check.</param>
    /// <param name="providerName">The provider name.</param>
    /// <param name="modelName">The model name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chunk IDs that do not have embeddings.</returns>
    Task<List<long>> FilterExistingEmbeddingsAsync(List<long> chunkIds, string providerName, string modelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a batch of embeddings with metadata.
    /// </summary>
    /// <param name="embeddingsData">The embedding data to insert.</param>
    /// <param name="chunkIdToStatus">Mapping of chunk IDs to status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InsertEmbeddingsBatchAsync(List<EmbeddingData> embeddingsData, Dictionary<long, string> chunkIdToStatus, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes embeddings for specific chunks.
    /// </summary>
    /// <param name="chunkIds">The chunk IDs.</param>
    /// <param name="providerName">The provider name.</param>
    /// <param name="modelName">The model name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteEmbeddingsForChunksAsync(List<long> chunkIds, string providerName, string modelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets chunks by file path.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chunks for the file.</returns>
    Task<List<Chunk>> GetChunksByFilePathAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets chunks by IDs.
    /// </summary>
    /// <param name="chunkIds">The chunk IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chunks.</returns>
    Task<List<Chunk>> GetChunksByIdsAsync(IReadOnlyList<long> chunkIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a file by its path.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file if found, null otherwise.</returns>
    Task<File?> GetFileByPathAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a file record.
    /// </summary>
    /// <param name="file">The file to insert or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file ID.</returns>
    Task<int> UpsertFileAsync(File file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes database tables.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OptimizeTablesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all data by dropping and recreating the chunks and files tables.
    /// </summary>
    Task ClearAllDataAsync();
}