using ChunkHound.Core;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Services;

/// <summary>
/// Stub implementation of the indexing coordinator.
/// </summary>
public class IndexingCoordinator : IIndexingCoordinator
{
    private readonly ILogger<IndexingCoordinator> _logger;

    public IndexingCoordinator(ILogger<IndexingCoordinator> logger)
    {
        _logger = logger;
    }

    public Task IndexAsync(string directoryPath)
    {
        _logger.LogInformation("Indexing directory: {DirectoryPath}", directoryPath);
        // Stub implementation - does nothing
        return Task.CompletedTask;
    }
}