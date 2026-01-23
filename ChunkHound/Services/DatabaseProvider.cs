using ChunkHound.Core;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Services;

/// <summary>
/// Stub implementation of the database provider.
/// </summary>
public class DatabaseProvider : IDatabaseProvider
{
    private readonly ILogger<DatabaseProvider> _logger;

    public DatabaseProvider(ILogger<DatabaseProvider> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync()
    {
        _logger.LogInformation("Initializing database");
        // Stub implementation - does nothing
        return Task.CompletedTask;
    }

    public Task<List<int>> StoreChunksAsync(List<Chunk> chunks)
    {
        _logger.LogInformation("Storing {Count} chunks", chunks.Count);
        // Stub implementation - return dummy IDs
        var ids = new List<int>();
        for (int i = 0; i < chunks.Count; i++)
        {
            ids.Add(i + 1); // Dummy IDs starting from 1
        }
        return Task.FromResult(ids);
    }

    public Task<List<Chunk>> GetChunksByHashesAsync(List<string> hashes)
    {
        _logger.LogInformation("Retrieving chunks for {Count} hashes", hashes.Count);
        // Stub implementation - returns empty list
        return Task.FromResult(new List<Chunk>());
    }
}