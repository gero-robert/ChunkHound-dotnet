using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using ChunkHound.Core;
using ChunkHound.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace ChunkHound.Core.Tests.Services
{
    public class EmbeddingProviderTests
    {
        private readonly Mock<ILogger<EmbeddingProvider>> _loggerMock;
        private readonly EmbeddingProvider _provider;

        public EmbeddingProviderTests()
        {
            _loggerMock = new Mock<ILogger<EmbeddingProvider>>();
            _provider = new EmbeddingProvider(_loggerMock.Object);
        }

        [Fact]
        public void ProviderName_ReturnsStub()
        {
            // Act
            var name = _provider.ProviderName;

            // Assert
            Assert.Equal("Stub", name);
        }

        [Fact]
        public void ModelName_ReturnsStubV1()
        {
            // Act
            var model = _provider.ModelName;

            // Assert
            Assert.Equal("stub-v1", model);
        }

        [Fact]
        public async Task EmbedAsync_ReturnsEmptyList()
        {
            // Arrange
            var texts = new List<string> { "text1", "text2", "text3" };

            // Act
            var embeddings = await _provider.EmbedAsync(texts);

            // Assert
            Assert.Empty(embeddings);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o != null && o.ToString().Contains("Embedding 3 texts")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task EmbedAsync_WithCancellationToken_RespectsCancellation()
        {
            // Arrange
            var texts = new List<string> { "text1" };
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                _provider.EmbedAsync(texts, cts.Token));
        }

        [Fact]
        public void GetMaxTokensPerBatch_Returns1000()
        {
            // Act
            var maxTokens = _provider.GetMaxTokensPerBatch();

            // Assert
            Assert.Equal(1000, maxTokens);
        }

        [Fact]
        public void GetMaxDocumentsPerBatch_Returns10()
        {
            // Act
            var maxDocs = _provider.GetMaxDocumentsPerBatch();

            // Assert
            Assert.Equal(10, maxDocs);
        }

        [Fact]
        public void GetRecommendedConcurrency_Returns1()
        {
            // Act
            var concurrency = _provider.GetRecommendedConcurrency();

            // Assert
            Assert.Equal(1, concurrency);
        }
    }
}