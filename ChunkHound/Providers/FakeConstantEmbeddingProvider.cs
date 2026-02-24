using System.Collections.Concurrent;

namespace ChunkHound.Providers;

/// <summary>
/// Fake embedding provider that returns random vectors for all inputs.
/// Used for cost-free testing and benchmarking.
/// </summary>
public class FakeConstantEmbeddingProvider : Core.IEmbeddingProvider
{
    private readonly Random _random = new();

    /// <summary>
    /// Gets the name of the embedding provider.
    /// </summary>
    public string ProviderName => "FakeConstant";

    /// <summary>
    /// Gets the model name/version used by the provider.
    /// </summary>
    public string ModelName => "random-v1";

    /// <summary>
    /// Initializes a new instance of the FakeConstantEmbeddingProvider class.
    /// </summary>
    public FakeConstantEmbeddingProvider()
    {
    }

    /// <summary>
    /// Generates random embeddings for a list of texts.
    /// </summary>
    /// <param name="texts">The texts to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of random embeddings, one for each input text.</returns>
    public async Task<List<List<float>>> EmbedAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken); // Simulate minimal latency
        return texts.Select(_ => GenerateRandomEmbedding()).ToList();
    }

    private List<float> GenerateRandomEmbedding()
    {
        var embedding = new float[1536];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(_random.NextDouble() * 2 - 1); // Random between -1 and 1
        }
        return embedding.ToList();
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

    /// <summary>
    /// Generates embedding for a single text.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The embedding.</returns>
    public async Task<List<float>> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken); // Simulate minimal latency
        return GenerateRandomEmbedding();
    }
}