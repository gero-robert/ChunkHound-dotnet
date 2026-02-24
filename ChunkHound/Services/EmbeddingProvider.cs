using ChunkHound.Core;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Services;

/// <summary>
/// Stub implementation of the embedding provider.
/// </summary>
public class EmbeddingProvider : IEmbeddingProvider
{
    private readonly ILogger<EmbeddingProvider> _logger;

    /// <summary>
    /// Gets the name of the embedding provider.
    /// </summary>
    public string ProviderName => "Stub";

    /// <summary>
    /// Gets the model name/version used by the provider.
    /// </summary>
    public string ModelName => "stub-v1";

    public EmbeddingProvider(ILogger<EmbeddingProvider> logger)
    {
        _logger = logger;
    }

    public Task<List<List<float>>> EmbedAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            throw new TaskCanceledException();
        }
        _logger.LogInformation("Embedding {Count} texts", texts.Count);
        // Stub implementation - returns empty embeddings
        return Task.FromResult(new List<List<float>>());
    }

    /// <summary>
    /// Gets the maximum number of tokens per batch.
    /// </summary>
    public int GetMaxTokensPerBatch() => 1000;

    /// <summary>
    /// Gets the maximum number of documents per batch.
    /// </summary>
    public int GetMaxDocumentsPerBatch() => 10;

    /// <summary>
    /// Gets the recommended concurrency level.
    /// </summary>
    public int GetRecommendedConcurrency() => 1;

    /// <summary>
    /// Generates embedding for a single text.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The embedding.</returns>
    public async Task<List<float>> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            throw new TaskCanceledException();
        }
        _logger.LogInformation("Embedding single text");
        // Stub implementation - returns empty embedding
        return new List<float>();
    }
}