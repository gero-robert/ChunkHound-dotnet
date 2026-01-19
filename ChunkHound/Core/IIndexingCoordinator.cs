namespace ChunkHound.Core;

/// <summary>
/// Interface for the indexing coordinator that orchestrates the indexing pipeline.
/// </summary>
public interface IIndexingCoordinator
{
    /// <summary>
    /// Runs the indexing process for the specified directory.
    /// </summary>
    /// <param name="directoryPath">The directory to index.</param>
    Task IndexAsync(string directoryPath);
}