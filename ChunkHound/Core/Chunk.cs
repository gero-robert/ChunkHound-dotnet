namespace ChunkHound.Core;

/// <summary>
/// Represents a code chunk with its metadata.
/// </summary>
public record Chunk(
    string Id,
    string FilePath,
    Language Language,
    string Content,
    string ContentHash,
    int StartLine,
    int EndLine,
    float[]? Embedding
);