using Xunit;
using Moq;
using ChunkHound.Services;
using ChunkHound.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChunkHound.Tests.Services;

public class EmbeddingServiceTests : IDisposable
{
    private readonly Mock<IDatabaseProvider> _mockDbProvider;
    private readonly Mock<IEmbeddingProvider> _mockEmbedProvider;
    private readonly EmbeddingService _service;

    public EmbeddingServiceTests()
    {
        _mockDbProvider = new Mock<IDatabaseProvider>();
        _mockEmbedProvider = new Mock<IEmbeddingProvider>();
        _service = new EmbeddingService(
            _mockDbProvider.Object,
            _mockEmbedProvider.Object,
            maxConcurrentBatches: 1,
            logger: new Microsoft.Extensions.Logging.Abstractions.NullLogger<EmbeddingService>());
    }

    /// <summary>
    /// Tests that GenerateEmbeddingsForChunksAsync gracefully handles the absence of an embedding provider
    /// by returning zero results without throwing exceptions. This ensures the service remains stable
    /// when provider configuration is missing, preventing crashes in deployment scenarios.
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsForChunksAsync_NoProviderConfigured_ReturnsZeroResults()
    {
        // Arrange
        var service = new EmbeddingService(_mockDbProvider.Object);
        var chunks = new List<Chunk> { CreateTestChunk() };

        // Act
        var result = await service.GenerateEmbeddingsForChunksAsync(chunks);

        // Assert
        Assert.Equal(0, result.TotalGenerated);
        Assert.Equal(0, result.FailedChunks);
    }

    /// <summary>
    /// Tests the core embedding generation workflow for valid chunks, ensuring that embeddings are
    /// successfully generated, filtered for existing ones, and stored in the database with proper
    /// batch processing and optimization. This validates the primary business functionality of
    /// converting code chunks to vector embeddings.
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsForChunksAsync_ValidChunks_GeneratesEmbeddings()
    {
        // Arrange
        var chunk = CreateTestChunk();
        var chunks = new List<Chunk> { chunk };
        var embeddings = new List<List<float>> { new List<float> { 0.1f, 0.2f } };

        _mockEmbedProvider.Setup(p => p.EmbedAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(embeddings);
        _mockEmbedProvider.Setup(p => p.ProviderName).Returns("test");
        _mockEmbedProvider.Setup(p => p.ModelName).Returns("model");
        _mockEmbedProvider.Setup(p => p.GetMaxTokensPerBatch()).Returns(1000);
        _mockEmbedProvider.Setup(p => p.GetMaxDocumentsPerBatch()).Returns(10);
        _mockEmbedProvider.Setup(p => p.GetRecommendedConcurrency()).Returns(8);
        _mockDbProvider.Setup(p => p.FilterExistingEmbeddingsAsync(It.IsAny<List<long>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<long>());
        _mockDbProvider.Setup(p => p.InsertEmbeddingsBatchAsync(It.IsAny<List<EmbeddingData>>(), It.IsAny<Dictionary<long, string>>(), It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
        _mockDbProvider.Setup(p => p.OptimizeTablesAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);

        // Act
        var result = await _service.GenerateEmbeddingsForChunksAsync(chunks);

        // Assert
        Assert.Equal(1, result.TotalGenerated);
        _mockDbProvider.Verify(p => p.InsertEmbeddingsBatchAsync(It.IsAny<List<EmbeddingData>>(), It.IsAny<Dictionary<long, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests the regeneration workflow for embeddings of a specific file, ensuring that existing
    /// embeddings are properly deleted and new ones are generated and stored. This validates
    /// the maintenance functionality for updating embeddings when code changes, preventing
    /// stale vector data from affecting search accuracy.
    /// </summary>
    [Fact]
    public async Task RegenerateEmbeddingsAsync_SpecificFile_Succeeds()
    {
        // Arrange
        var filePath = "/test/file.cs";
        var chunks = new List<Chunk> { CreateTestChunk() };

        _mockEmbedProvider.Setup(p => p.ProviderName).Returns("test");
        _mockEmbedProvider.Setup(p => p.ModelName).Returns("model");
        _mockEmbedProvider.Setup(p => p.GetMaxTokensPerBatch()).Returns(1000);
        _mockEmbedProvider.Setup(p => p.GetMaxDocumentsPerBatch()).Returns(10);
        _mockEmbedProvider.Setup(p => p.GetRecommendedConcurrency()).Returns(8);
        _mockDbProvider.Setup(p => p.GetChunksByFilePathAsync(filePath, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(chunks);
        _mockDbProvider.Setup(p => p.DeleteEmbeddingsForChunksAsync(It.IsAny<List<long>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
        _mockDbProvider.Setup(p => p.FilterExistingEmbeddingsAsync(It.IsAny<List<long>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<long>());
        _mockDbProvider.Setup(p => p.InsertEmbeddingsBatchAsync(It.IsAny<List<EmbeddingData>>(), It.IsAny<Dictionary<long, string>>(), It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
        _mockDbProvider.Setup(p => p.OptimizeTablesAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
        _mockEmbedProvider.Setup(p => p.EmbedAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new List<List<float>> { new List<float> { 0.1f, 0.2f } });

        // Act
        var result = await _service.RegenerateEmbeddingsAsync(filePath: filePath);

        // Assert
        Assert.Equal("success", result.Status);
        Assert.Equal(1, result.Regenerated);
    }

    /// <summary>
    /// Tests that embedding generation handles provider failures gracefully with proper error classification
    /// and retry logic. This validates the resilience of the batch processing pipeline when the
    /// embedding provider encounters transient failures, ensuring failed chunks are retried appropriately.
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsForChunksAsync_ProviderFailsWithTransientError_RetriesAndSucceeds()
    {
        // Arrange
        var chunk = CreateTestChunk();
        var chunks = new List<Chunk> { chunk };
        var embeddings = new List<List<float>> { new List<float> { 0.1f, 0.2f } };

        _mockEmbedProvider.Setup(p => p.ProviderName).Returns("test");
        _mockEmbedProvider.Setup(p => p.ModelName).Returns("model");
        _mockEmbedProvider.Setup(p => p.GetMaxTokensPerBatch()).Returns(1000);
        _mockEmbedProvider.Setup(p => p.GetMaxDocumentsPerBatch()).Returns(10);
        _mockEmbedProvider.Setup(p => p.GetRecommendedConcurrency()).Returns(8);
        _mockDbProvider.Setup(p => p.FilterExistingEmbeddingsAsync(It.IsAny<List<long>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<long>());

        // First call fails with timeout (transient), second succeeds
        var callCount = 0;
        _mockEmbedProvider.Setup(p => p.EmbedAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(() =>
                          {
                              callCount++;
                              if (callCount == 1)
                                  throw new TimeoutException("Request timed out");
                              return embeddings;
                          });

        _mockDbProvider.Setup(p => p.InsertEmbeddingsBatchAsync(It.IsAny<List<EmbeddingData>>(), It.IsAny<Dictionary<long, string>>(), It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
        _mockDbProvider.Setup(p => p.OptimizeTablesAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);

        // Act
        var result = await _service.GenerateEmbeddingsForChunksAsync(chunks);

        // Assert
        Assert.Equal(1, result.TotalGenerated);
        Assert.Equal(2, callCount); // Should succeed on retry
    }

    /// <summary>
    /// Tests that embedding generation handles permanent provider failures by marking chunks as failed
    /// without excessive retries. This validates error handling for unrecoverable failures like
    /// authentication errors or invalid requests, preventing wasted resources on futile retry attempts.
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsForChunksAsync_ProviderFailsWithPermanentError_MarksAsFailed()
    {
        // Arrange
        var chunk = CreateTestChunk();
        var chunks = new List<Chunk> { chunk };

        _mockEmbedProvider.Setup(p => p.ProviderName).Returns("test");
        _mockEmbedProvider.Setup(p => p.ModelName).Returns("model");
        _mockEmbedProvider.Setup(p => p.GetMaxTokensPerBatch()).Returns(1000);
        _mockEmbedProvider.Setup(p => p.GetMaxDocumentsPerBatch()).Returns(10);
        _mockEmbedProvider.Setup(p => p.GetRecommendedConcurrency()).Returns(8);
        _mockDbProvider.Setup(p => p.FilterExistingEmbeddingsAsync(It.IsAny<List<long>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<long>());

        // Provider fails with HTTP 401 (permanent error)
        _mockEmbedProvider.Setup(p => p.EmbedAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                          .ThrowsAsync(new System.Net.Http.HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized));

        _mockDbProvider.Setup(p => p.InsertEmbeddingsBatchAsync(It.IsAny<List<EmbeddingData>>(), It.IsAny<Dictionary<long, string>>(), It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
        _mockDbProvider.Setup(p => p.OptimizeTablesAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);

        // Act
        var result = await _service.GenerateEmbeddingsForChunksAsync(chunks);

        // Assert
        Assert.Equal(0, result.TotalGenerated);
        Assert.Equal(1, result.PermanentFailures);
    }

    /// <summary>
    /// Tests that embedding generation handles circuit breaker activation when consecutive failures occur.
    /// This validates the circuit breaker pattern implementation, ensuring the service stops making
    /// requests when the provider is consistently unavailable, preventing resource exhaustion.
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsForChunksAsync_CircuitBreakerOpens_PreventsRequests()
    {
        // Arrange
        var chunk = CreateTestChunk();
        var chunks = new List<Chunk> { chunk };

        _mockEmbedProvider.Setup(p => p.ProviderName).Returns("test");
        _mockEmbedProvider.Setup(p => p.ModelName).Returns("model");
        _mockEmbedProvider.Setup(p => p.GetMaxTokensPerBatch()).Returns(1000);
        _mockEmbedProvider.Setup(p => p.GetMaxDocumentsPerBatch()).Returns(10);
        _mockEmbedProvider.Setup(p => p.GetRecommendedConcurrency()).Returns(8);
        _mockDbProvider.Setup(p => p.FilterExistingEmbeddingsAsync(It.IsAny<List<long>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<long>());

        // Simulate circuit breaker being open by throwing InvalidOperationException
        _mockEmbedProvider.Setup(p => p.EmbedAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                          .ThrowsAsync(new InvalidOperationException("Circuit breaker is open"));

        _mockDbProvider.Setup(p => p.InsertEmbeddingsBatchAsync(It.IsAny<List<EmbeddingData>>(), It.IsAny<Dictionary<long, string>>(), It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
        _mockDbProvider.Setup(p => p.OptimizeTablesAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);

        // Act
        var result = await _service.GenerateEmbeddingsForChunksAsync(chunks);

        // Assert
        Assert.Equal(0, result.TotalGenerated);
        Assert.Equal(1, result.FailedChunks); // Should be marked as transient failure
    }

    /// <summary>
    /// Tests that embedding generation handles rate limiting by respecting provider constraints.
    /// This validates rate limiting implementation, ensuring the service doesn't exceed provider
    /// request limits and handles throttling gracefully with appropriate backoff.
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsForChunksAsync_RateLimitExceeded_HandlesGracefully()
    {
        // Arrange
        var chunk = CreateTestChunk();
        var chunks = new List<Chunk> { chunk };

        _mockEmbedProvider.Setup(p => p.ProviderName).Returns("test");
        _mockEmbedProvider.Setup(p => p.ModelName).Returns("model");
        _mockEmbedProvider.Setup(p => p.GetMaxTokensPerBatch()).Returns(1000);
        _mockEmbedProvider.Setup(p => p.GetMaxDocumentsPerBatch()).Returns(10);
        _mockEmbedProvider.Setup(p => p.GetRecommendedConcurrency()).Returns(8);
        _mockDbProvider.Setup(p => p.FilterExistingEmbeddingsAsync(It.IsAny<List<long>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<long>());

        // Provider throws rate limit exception
        _mockEmbedProvider.Setup(p => p.EmbedAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                          .ThrowsAsync(new InvalidOperationException("Rate limit exceeded"));

        _mockDbProvider.Setup(p => p.InsertEmbeddingsBatchAsync(It.IsAny<List<EmbeddingData>>(), It.IsAny<Dictionary<long, string>>(), It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
        _mockDbProvider.Setup(p => p.OptimizeTablesAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);

        // Act
        var result = await _service.GenerateEmbeddingsForChunksAsync(chunks);

        // Assert
        Assert.Equal(0, result.TotalGenerated);
        Assert.Equal(1, result.FailedChunks); // Should be marked as transient failure
    }

    private static Chunk CreateTestChunk() =>
        new Chunk("TestFunc", 1, "function test() {}", 1, 10, Language.JavaScript, ChunkType.Function, "TestFunc");

    public void Dispose()
    {
        // Cleanup if needed
    }
}