namespace ChunkHound.Core;

/// <summary>
/// Configuration options for indexing operations.
/// </summary>
public sealed record IndexingOptions
{
    /// <summary>
    /// Path to temporary directory for processing.
    /// </summary>
    public string TempPath { get; init; } = System.IO.Path.GetTempPath();
    // future: int MaxWorkers etc.

    /// <summary>
    /// Creates IndexingOptions from dictionary.
    /// </summary>
    /// <param name="dict">Configuration dictionary.</param>
    /// <returns>IndexingOptions instance.</returns>
    public static IndexingOptions FromDict(Dictionary<string, object> dict) => throw new NotImplementedException();
}