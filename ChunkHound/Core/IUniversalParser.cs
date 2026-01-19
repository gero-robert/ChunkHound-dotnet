namespace ChunkHound.Core;

/// <summary>
/// Interface for universal parsers that parse code files into chunks.
/// </summary>
public interface IUniversalParser
{
    /// <summary>
    /// Parses a file into code chunks.
    /// </summary>
    /// <param name="file">The file to parse.</param>
    /// <returns>The parsed chunks.</returns>
    Task<List<Chunk>> ParseAsync(File file);
}