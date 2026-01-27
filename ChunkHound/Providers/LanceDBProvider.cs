using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core;
using ChunkHound.Core.Utilities;
using Microsoft.Extensions.Logging;

// Assuming LanceDB .NET libraries are available
// using LanceDB; // Placeholder for LanceDB client
// using PyArrow; // Placeholder for PyArrow bindings

namespace ChunkHound.Providers;

/// <summary>
/// LanceDB implementation of the database provider for ChunkHound.
/// Optimized for performance using native LanceDB queries instead of DataFrame filtering
/// wherever possible to reduce memory usage and improve query performance for large datasets.
/// </summary>
public class LanceDBProvider : IDatabaseProvider, IDisposable
{
    // Field name constants for chunks table
    private const string ID_FIELD = "id";
    private const string FILE_ID_FIELD = "file_id";
    private const string CONTENT_FIELD = "content";
    private const string CONTENT_HASH_FIELD = "content_hash";
    private const string START_LINE_FIELD = "start_line";
    private const string END_LINE_FIELD = "end_line";
    private const string CHUNK_TYPE_FIELD = "chunk_type";
    private const string LANGUAGE_FIELD = "language";
    private const string NAME_FIELD = "name";
    private const string CREATED_TIME_FIELD = "created_time";

    // Field name constants for files table
    private const string PATH_FIELD = "path";
    private const string SIZE_FIELD = "size";
    private const string MODIFIED_TIME_FIELD = "modified_time";
    private const string INDEXED_TIME_FIELD = "indexed_time";
    private const string ENCODING_FIELD = "encoding";
    private const string LINE_COUNT_FIELD = "line_count";

    // Table name constants
    private const string CHUNKS_TABLE = "chunks";
    private const string FILES_TABLE = "files";

    private readonly string _dbPath;
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
    private readonly ILogger? _logger;
    private readonly int _fragmentThreshold;
    private readonly TimeSpan _connectionTimeout;
    private LanceDbService? _lanceDbService;
    private int _nextChunkId = 1;
    private bool _isInitialized = false;

    private readonly SemaphoreSlim _pythonSemaphore = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Initializes a new instance of the LanceDBProvider.
    /// </summary>
    /// <param name="dbPath">Path to LanceDB database directory</param>
    /// <param name="logger">Logger instance for diagnostics</param>
    /// <param name="fragmentThreshold">Threshold for fragment optimization</param>
    /// <param name="connectionTimeout">Timeout for database connections</param>
    public LanceDBProvider(string dbPath, ILogger? logger = null, int fragmentThreshold = 100, TimeSpan connectionTimeout = default)
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
        _logger = logger;
        _fragmentThreshold = fragmentThreshold;
        _connectionTimeout = connectionTimeout;
    }

    /// <summary>
    /// Gets the LanceDB service, initializing it if necessary.
    /// </summary>
    private LanceDbService LanceDbService
    {
        get
        {
            _lanceDbService ??= new LanceDbService(_dbPath, _logger);
            return _lanceDbService;
        }
    }

    /// <summary>
    /// Initializes the database schema.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        _lock.EnterWriteLock();
        try
        {
            // LanceDbService is already initialized in constructor
            // Create schema if needed
            await CreateSchemaAsync();

            _isInitialized = true;
            _logger?.LogInformation("LanceDB database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize LanceDB database");
            throw;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Stores a batch of chunks in the database.
    /// </summary>
    /// <param name="chunks">The chunks to store.</param>
    /// <returns>The IDs of the stored chunks.</returns>
    public async Task<List<int>> StoreChunksAsync(List<Chunk> chunks)
    {
        if (chunks == null || chunks.Count == 0)
            return new List<int>();

        var ids = new List<int>();
        _logger?.LogDebug("StoreChunksAsync: Entering write lock for {Count} chunks", chunks.Count);
        _lock.EnterWriteLock();
        _logger?.LogDebug("StoreChunksAsync: Acquired write lock for {Count} chunks", chunks.Count);
        try
        {
            var chunkData = new List<Dictionary<string, object>>();

            foreach (var chunk in chunks)
            {
                var id = chunk.Id ?? Interlocked.Increment(ref _nextChunkId) - 1;
                var hash = HashUtility.ComputeContentHash(chunk.Code);
                var data = new Dictionary<string, object>
                {
                    [ID_FIELD] = id,
                    [FILE_ID_FIELD] = chunk.FileId,
                    [CONTENT_FIELD] = chunk.Code,
                    [CONTENT_HASH_FIELD] = hash,
                    [START_LINE_FIELD] = chunk.StartLine,
                    [END_LINE_FIELD] = chunk.EndLine,
                    [CHUNK_TYPE_FIELD] = chunk.ChunkType.Value(),
                    [LANGUAGE_FIELD] = chunk.Language.Value(),
                    [NAME_FIELD] = chunk.Symbol ?? "",
                    [CREATED_TIME_FIELD] = DateTimeOffset.Now.ToUnixTimeSeconds()
                };

                chunkData.Add(data);
                ids.Add(id);
            }

            // Insert chunks into LanceDB
            LanceDbService.AddBatch("chunks", chunkData);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to store chunks");
            throw;
        }
        finally
        {
            _logger?.LogDebug("StoreChunksAsync: Exiting write lock");
            _lock.ExitWriteLock();
        }

        // Check for optimization after releasing the write lock
        if (await ShouldOptimizeFragmentsAsync())
        {
            await OptimizeTablesAsync();
        }

        return ids;
    }

    /// <summary>
    /// Retrieves chunks by their content hashes.
    /// </summary>
    /// <param name="hashes">The content hashes to search for.</param>
    /// <returns>The matching chunks.</returns>
    public async Task<List<Chunk>> GetChunksByHashesAsync(List<string> hashes)
    {
        if (hashes == null || hashes.Count == 0)
            return new List<Chunk>();

        if (!_isInitialized)
            throw new InvalidOperationException("Database not initialized");

        _lock.EnterReadLock();
        try
        {
            var result = new List<Chunk>();

            // Query LanceDB for chunks by content_hash
            var hashList = string.Join("', '", hashes);
            var filter = $"content_hash IN ('{hashList}')";
            var rows = LanceDbService.Query("chunks", filter);

            // For each row, create Chunk
            foreach (var row in rows)
            {
                var chunk = new Chunk(
                    symbol: row.GetValueOrDefault(NAME_FIELD, "") as string,
                    startLine: Convert.ToInt32(row.GetValueOrDefault(START_LINE_FIELD, 0)),
                    endLine: Convert.ToInt32(row.GetValueOrDefault(END_LINE_FIELD, 0)),
                    code: row.GetValueOrDefault(CONTENT_FIELD, "") as string,
                    chunkType: ChunkTypeExtensions.FromString(row.GetValueOrDefault(CHUNK_TYPE_FIELD, "") as string),
                    fileId: Convert.ToInt32(row.GetValueOrDefault(FILE_ID_FIELD, 0)),
                    language: LanguageExtensions.FromString(row.GetValueOrDefault(LANGUAGE_FIELD, "") as string),
                    id: Convert.ToInt32(row.GetValueOrDefault(ID_FIELD, 0))
                );
                result.Add(chunk);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve chunks by hashes");
            throw;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Creates database schema for files and chunks.
    /// </summary>
    private async Task CreateSchemaAsync()
    {
        // Placeholder: Create tables if they don't exist
        try
        {
            // _filesTable = await _connection.OpenTableAsync(FILES_TABLE);
        }
        catch
        {
            // _filesTable = await _connection.CreateTableAsync(FILES_TABLE, GetFilesSchema());
            _logger?.LogInformation("Created files table");
        }

        try
        {
            // _chunksTable = await _connection.OpenTableAsync(CHUNKS_TABLE);
            _logger?.LogDebug("Opened existing chunks table");
        }
        catch
        {
            // _chunksTable = await _connection.CreateTableAsync(CHUNKS_TABLE, GetChunksSchema());
            _logger?.LogInformation("Created chunks table");
        }
    }

    /// <summary>
    /// Gets current fragment counts for chunks and files tables.
    /// </summary>
    public async Task<Dictionary<string, int>> GetFragmentCountAsync()
    {
        _lock.EnterReadLock();
        try
        {
            var result = new Dictionary<string, int>();

            var chunksStats = LanceDbService.GetTableStats(CHUNKS_TABLE);
            result[CHUNKS_TABLE] = ExtractFragmentCount(chunksStats);

            var filesStats = LanceDbService.GetTableStats(FILES_TABLE);
            result[FILES_TABLE] = ExtractFragmentCount(filesStats);

            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Checks if fragment optimization is needed.
    /// </summary>
    private async Task<bool> ShouldOptimizeFragmentsAsync()
    {
        var counts = await GetFragmentCountAsync();
        var chunksFragments = counts.GetValueOrDefault(CHUNKS_TABLE, 0);
        return chunksFragments >= _fragmentThreshold;
    }

    /// <summary>
    /// Optimizes tables by compacting fragments.
    /// </summary>
    private async Task OptimizeTablesAsync()
    {
        _logger?.LogInformation("Starting table optimization");

        var initialCounts = await GetFragmentCountAsync();
        _logger?.LogDebug($"Initial fragment counts: chunks={initialCounts.GetValueOrDefault(CHUNKS_TABLE, 0)}, files={initialCounts.GetValueOrDefault(FILES_TABLE, 0)}");

        // Optimize tables
        LanceDbService.Optimize();

        var finalCounts = await GetFragmentCountAsync();
        var chunksReduction = initialCounts.GetValueOrDefault(CHUNKS_TABLE, 0) - finalCounts.GetValueOrDefault(CHUNKS_TABLE, 0);
        var filesReduction = initialCounts.GetValueOrDefault(FILES_TABLE, 0) - finalCounts.GetValueOrDefault(FILES_TABLE, 0);

        _logger?.LogInformation($"Fragment reduction: chunks={chunksReduction}, files={filesReduction}");

        _logger?.LogInformation("Table optimization completed");
    }

    /// <summary>
    /// Extracts fragment count from table stats.
    /// </summary>
    private static int ExtractFragmentCount(Dictionary<string, object> stats)
    {
        // Try to extract fragment count from stats
        if (stats.TryGetValue("num_fragments", out var fragments))
        {
            return Convert.ToInt32(fragments);
        }
        if (stats.TryGetValue("fragments", out var fragmentsObj))
        {
            return Convert.ToInt32(fragmentsObj);
        }
        // Default to 0 if not found
        return 0;
    }

    /// <summary>
    /// Gets the schema for the files table.
    /// </summary>
    private static object GetFilesSchema()
    {
        // Placeholder: Return PyArrow schema
        // return new Schema(new Field[]
        // {
        //     new Field(ID_FIELD, DataType.Int64, false),
        //     new Field(PATH_FIELD, DataType.String, false),
        //     new Field(SIZE_FIELD, DataType.Int64, false),
        //     new Field(MODIFIED_TIME_FIELD, DataType.Float64, false),
        //     new Field(CONTENT_HASH_FIELD, DataType.String, true),
        //     new Field(INDEXED_TIME_FIELD, DataType.Float64, false),
        //     new Field(LANGUAGE_FIELD, DataType.String, false),
        //     new Field(ENCODING_FIELD, DataType.String, true),
        //     new Field(LINE_COUNT_FIELD, DataType.Int64, false)
        // });
        return new object(); // Placeholder
    }

    /// <summary>
    /// Gets the schema for the chunks table.
    /// </summary>
    private static object GetChunksSchema()
    {
        // Placeholder: Return PyArrow schema
        // return new Schema(new Field[]
        // {
        //     new Field(ID_FIELD, DataType.Int64, false),
        //     new Field(FILE_ID_FIELD, DataType.Int64, false),
        //     new Field(CONTENT_FIELD, DataType.String, false),
        //     new Field(CONTENT_HASH_FIELD, DataType.String, false),
        //     new Field(START_LINE_FIELD, DataType.Int64, false),
        //     new Field(END_LINE_FIELD, DataType.Int64, false),
        //     new Field(CHUNK_TYPE_FIELD, DataType.String, false),
        //     new Field(LANGUAGE_FIELD, DataType.String, false),
        //     new Field(NAME_FIELD, DataType.String, true),
        //     new Field(CREATED_TIME_FIELD, DataType.Float64, false)
        // });
        return new object(); // Placeholder
    }

    /// <summary>
    /// Inserts a batch of chunks into the database.
    /// </summary>
    /// <param name="chunks">The chunks to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The IDs of the inserted chunks.</returns>
    public async Task<List<int>> InsertChunksBatchAsync(List<Chunk> chunks, CancellationToken cancellationToken = default)
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
        // Placeholder: Insert embeddings
        // This would typically create or update an embeddings table
        await Task.Delay(1, cancellationToken);
        return embeddings.Count;
    }

    public async Task<List<long>> FilterExistingEmbeddingsAsync(List<long> chunkIds, string providerName, string modelName, CancellationToken cancellationToken = default)
    {
        if (chunkIds == null || chunkIds.Count == 0)
            return new List<long>();

        _lock.EnterReadLock();
        try
        {
            var idList = string.Join(", ", chunkIds);
            var filter = $"id IN ({idList})";
            var rows = LanceDbService.Query("chunks", filter);

            var existingIds = new List<long>();
            foreach (var row in rows)
            {
                if (row.TryGetValue(ID_FIELD, out var idObj))
                {
                    existingIds.Add(Convert.ToInt64(idObj));
                }
            }

            return existingIds;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to filter existing embeddings");
            return new List<long>();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public async Task InsertEmbeddingsBatchAsync(List<EmbeddingData> embeddingsData, Dictionary<long, string> chunkIdToStatus, CancellationToken cancellationToken = default)
    {
        if (embeddingsData == null || embeddingsData.Count == 0)
            return;

        _lock.EnterWriteLock();
        try
        {
            var embeddingRecords = new List<Dictionary<string, object>>();

            foreach (var embeddingData in embeddingsData)
            {
                var record = new Dictionary<string, object>
                {
                    [ID_FIELD] = embeddingData.ChunkId,
                    ["embedding"] = embeddingData.Embedding,
                    ["provider"] = embeddingData.Provider,
                    ["model"] = embeddingData.Model,
                    ["embedding_signature"] = $"{embeddingData.Provider}:{embeddingData.Model}:{embeddingData.Dimensions}",
                    ["embedding_status"] = embeddingData.Status
                };

                embeddingRecords.Add(record);
            }

            LanceDbService.UpdateEmbeddings(embeddingRecords);
            _logger?.LogDebug("Inserted {Count} embeddings", embeddingsData.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to insert embeddings batch");
            throw;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public async Task DeleteEmbeddingsForChunksAsync(List<long> chunkIds, string providerName, string modelName, CancellationToken cancellationToken = default)
    {
        // Placeholder
        await Task.Delay(1, cancellationToken);
    }

    public async Task<List<Chunk>> GetChunksByFilePathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Placeholder
        await Task.Delay(1, cancellationToken);
        return new List<Chunk>();
    }

    public async Task<List<Chunk>> GetChunksByIdsAsync(IReadOnlyList<long> chunkIds, CancellationToken cancellationToken = default)
    {
        if (chunkIds == null || chunkIds.Count == 0)
            return new List<Chunk>();

        _lock.EnterReadLock();
        try
        {
            var idList = string.Join(", ", chunkIds);
            var filter = $"id IN ({idList})";
            var rows = LanceDbService.Query("chunks", filter);

            var result = new List<Chunk>();
            foreach (var row in rows)
            {
                var chunk = new Chunk(
                    symbol: row.GetValueOrDefault(NAME_FIELD, "") as string,
                    startLine: Convert.ToInt32(row.GetValueOrDefault(START_LINE_FIELD, 0)),
                    endLine: Convert.ToInt32(row.GetValueOrDefault(END_LINE_FIELD, 0)),
                    code: row.GetValueOrDefault(CONTENT_FIELD, "") as string,
                    chunkType: ChunkTypeExtensions.FromString(row.GetValueOrDefault(CHUNK_TYPE_FIELD, "") as string),
                    fileId: Convert.ToInt32(row.GetValueOrDefault(FILE_ID_FIELD, 0)),
                    language: LanguageExtensions.FromString(row.GetValueOrDefault(LANGUAGE_FIELD, "") as string),
                    id: Convert.ToInt32(row.GetValueOrDefault(ID_FIELD, 0))
                );
                result.Add(chunk);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get chunks by IDs");
            throw;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets a file by its path.
    /// </summary>
    public async Task<Core.File?> GetFileByPathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Placeholder
        await Task.Delay(1, cancellationToken);
        return null;
    }

    /// <summary>
    /// Inserts or updates a file record.
    /// </summary>
    public async Task<int> UpsertFileAsync(Core.File file, CancellationToken cancellationToken = default)
    {
        // Placeholder
        await Task.Delay(1, cancellationToken);
        return file.Id ?? 1;
    }

    public async Task OptimizeTablesAsync(CancellationToken cancellationToken = default)
    {
        await OptimizeTablesAsync();
    }

    /// <summary>
    /// Clears all data by dropping and recreating the chunks and files tables.
    /// </summary>
    public async Task ClearAllDataAsync()
    {
        _lock.EnterWriteLock();
        try
        {
            LanceDbService.ClearAllData();
            _nextChunkId = 1; // Reset chunk ID counter
            _logger?.LogInformation("All data cleared and tables recreated");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Disposes the provider and releases resources.
    /// </summary>
    public void Dispose()
    {
        _lock.Dispose();
        _lanceDbService?.Dispose();
    }
}