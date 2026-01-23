using ChunkHound.Core;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Services;

/// <summary>
/// Stub implementation of the embedding provider.
/// </summary>
public class EmbeddingProvider : IEmbeddingProvider
{
    private readonly ILogger<EmbeddingProvider> _logger;

    public EmbeddingProvider(ILogger<EmbeddingProvider> logger)
    {
        _logger = logger;
    }

    public Task<List<List<float>>> EmbedAsync(List<string> texts)
    {
        _logger.LogInformation("Embedding {Count} texts", texts.Count);
        // Stub implementation - returns empty embeddings
        return Task.FromResult(new List<List<float>>());
    }
}