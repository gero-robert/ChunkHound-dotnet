using ChunkHound.Core;

namespace ChunkHound.Parsers;

public interface IChunkParser
{
    bool CanHandle(string fileExtension);
    Task<IReadOnlyList<Chunk>> ParseAsync(string content, string filePath);
}