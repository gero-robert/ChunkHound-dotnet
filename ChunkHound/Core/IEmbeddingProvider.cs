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
    /// <returns>A list of embeddings, one for each input text.</returns>
    Task<List<List<float>>> EmbedAsync(List<string> texts);
}