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

    private static Chunk CreateTestChunk() =>
        new Chunk("TestFunc", 1, 10, "function test() {}", ChunkType.Function, 1, Language.JavaScript, 1);

    public void Dispose()
    {
        // Cleanup if needed
    }
}