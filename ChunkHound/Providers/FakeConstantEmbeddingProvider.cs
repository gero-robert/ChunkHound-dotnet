using System.Collections.Concurrent;

namespace ChunkHound.Providers;

/// <summary>
/// Fake embedding provider that returns constant vectors for all inputs.
/// Used for cost-free testing and benchmarking.
/// </summary>
public class FakeConstantEmbeddingProvider : Core.IEmbeddingProvider
{
    private readonly float[] _constantVector;

    /// <summary>
    /// Gets the name of the embedding provider.
    /// </summary>
    public string ProviderName => "FakeConstant";

    /// <summary>
    /// Gets the model name/version used by the provider.
    /// </summary>
    public string ModelName => "constant-v1";

    /// <summary>
    /// Initializes a new instance of the FakeConstantEmbeddingProvider class.
    /// </summary>
    /// <param name="dimensions">The number of dimensions for the embedding vectors. Default is 1536 (OpenAI default).</param>
    /// <param name="vectorValue">The constant value to use for all vector components. Default is 0.1f.</param>
    public FakeConstantEmbeddingProvider(int dimensions = 1536, float vectorValue = 0.1f)
    {
        _constantVector = Enumerable.Repeat(vectorValue, dimensions).ToArray();
    }

    /// <summary>
    /// Generates constant embeddings for a list of texts.
    /// </summary>
    /// <param name="texts">The texts to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of constant embeddings, one for each input text.</returns>
    public async Task<List<List<float>>> EmbedAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken); // Simulate minimal latency
        return texts.Select(_ => _constantVector.ToList()).ToList();
    }

    /// <summary>
    /// Gets the maximum number of tokens per batch.
    /// </summary>
    public int GetMaxTokensPerBatch() => 8192;

    /// <summary>
    /// Gets the maximum number of documents per batch.
    /// </summary>
    public int GetMaxDocumentsPerBatch() => 100;

    /// <summary>
    /// Gets the recommended concurrency level.
    /// </summary>
    public int GetRecommendedConcurrency() => 8;
}