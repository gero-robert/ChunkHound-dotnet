using ChunkHound.Core;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Services;

/// <summary>
/// Stub implementation of the universal parser.
/// </summary>
public class UniversalParser : IUniversalParser
{
    private readonly ILogger<UniversalParser> _logger;

    public UniversalParser(ILogger<UniversalParser> logger)
    {
        _logger = logger;
    }

    public Task<List<Chunk>> ParseAsync(ChunkHound.Core.File file)
    {
        _logger.LogInformation("Parsing file: {FilePath}", file.Path);
        // Stub implementation - returns empty list
        return Task.FromResult(new List<Chunk>());
    }
}