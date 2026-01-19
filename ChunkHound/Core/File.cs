namespace ChunkHound.Core;

/// <summary>
/// Represents a file to be indexed.
/// </summary>
public record File(
    string Id,
    string Path,
    long Mtime,
    Language Language,
    long SizeBytes,
    string ContentHash
);