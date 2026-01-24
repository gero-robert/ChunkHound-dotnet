using Xunit;
using ChunkHound.Providers;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace ChunkHound.Core.Tests.Providers
{
    public class FakeConstantEmbeddingProviderTests
    {
        [Fact]
        public void Constructor_DefaultValues_SetsCorrectProperties()
        {
            // Act
            var provider = new FakeConstantEmbeddingProvider();

            // Assert
            Assert.Equal("FakeConstant", provider.ProviderName);
            Assert.Equal("constant-v1", provider.ModelName);
        }

        [Fact]
        public async Task EmbedAsync_ReturnsConstantVectors()
        {
            // Arrange
            var provider = new FakeConstantEmbeddingProvider(dimensions: 5, vectorValue: 0.5f);
            var texts = new List<string> { "text1", "text2", "text3" };

            // Act
            var embeddings = await provider.EmbedAsync(texts);

            // Assert
            Assert.Equal(3, embeddings.Count);
            foreach (var embedding in embeddings)
            {
                Assert.Equal(5, embedding.Count);
                Assert.All(embedding, value => Assert.Equal(0.5f, value));
            }
        }

        [Fact]
        public async Task EmbedAsync_DefaultDimensions_Returns1536Dimensions()
        {
            // Arrange
            var provider = new FakeConstantEmbeddingProvider();
            var texts = new List<string> { "test" };

            // Act
            var embeddings = await provider.EmbedAsync(texts);

            // Assert
            Assert.Single(embeddings);
            Assert.Equal(1536, embeddings[0].Count);
            Assert.All(embeddings[0], value => Assert.Equal(0.1f, value));
        }

        [Fact]
        public async Task EmbedAsync_EmptyList_ReturnsEmptyList()
        {
            // Arrange
            var provider = new FakeConstantEmbeddingProvider();
            var texts = new List<string>();

            // Act
            var embeddings = await provider.EmbedAsync(texts);

            // Assert
            Assert.Empty(embeddings);
        }

        [Fact]
        public async Task EmbedAsync_WithCancellation_RespectsCancellation()
        {
            // Arrange
            var provider = new FakeConstantEmbeddingProvider();
            var texts = new List<string> { "test" };
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                provider.EmbedAsync(texts, cts.Token));
        }

        [Fact]
        public void GetMaxTokensPerBatch_Returns8192()
        {
            // Arrange
            var provider = new FakeConstantEmbeddingProvider();

            // Act
            var maxTokens = provider.GetMaxTokensPerBatch();

            // Assert
            Assert.Equal(8192, maxTokens);
        }

        [Fact]
        public void GetMaxDocumentsPerBatch_Returns100()
        {
            // Arrange
            var provider = new FakeConstantEmbeddingProvider();

            // Act
            var maxDocs = provider.GetMaxDocumentsPerBatch();

            // Assert
            Assert.Equal(100, maxDocs);
        }

        [Fact]
        public void GetRecommendedConcurrency_Returns8()
        {
            // Arrange
            var provider = new FakeConstantEmbeddingProvider();

            // Act
            var concurrency = provider.GetRecommendedConcurrency();

            // Assert
            Assert.Equal(8, concurrency);
        }

        [Fact]
        public async Task EmbedAsync_LargeBatch_HandlesCorrectly()
        {
            // Arrange
            var provider = new FakeConstantEmbeddingProvider(dimensions: 10);
            var texts = new List<string>();
            for (int i = 0; i < 1000; i++)
            {
                texts.Add($"text{i}");
            }

            // Act
            var embeddings = await provider.EmbedAsync(texts);

            // Assert
            Assert.Equal(1000, embeddings.Count);
            foreach (var embedding in embeddings)
            {
                Assert.Equal(10, embedding.Count);
                Assert.All(embedding, value => Assert.Equal(0.1f, value));
            }
        }
    }
}