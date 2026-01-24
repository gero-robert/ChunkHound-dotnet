namespace ChunkHound.Core;

/// <summary>
/// Interface for embedding providers that generate vector embeddings for text.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Generates embeddings for a list of texts.
    /// </summary>
    /// <param name="texts">The texts to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of embeddings, one for each input text.</returns>
    Task<List<List<float>>> EmbedAsync(List<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name of the embedding provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the model name/version used by the provider.
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Gets the maximum number of tokens per batch for this provider.
    /// </summary>
    int GetMaxTokensPerBatch();

    /// <summary>
    /// Gets the maximum number of documents per batch for this provider.
    /// </summary>
    int GetMaxDocumentsPerBatch();

    /// <summary>
    /// Gets the recommended concurrency level for this provider.
    /// </summary>
    int GetRecommendedConcurrency();
}