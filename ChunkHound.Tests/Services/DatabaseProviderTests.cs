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
    public class DatabaseProviderTests
    {
        private readonly Mock<ILogger<DatabaseProvider>> _loggerMock;
        private readonly DatabaseProvider _provider;

        public DatabaseProviderTests()
        {
            _loggerMock = new Mock<ILogger<DatabaseProvider>>();
            _provider = new DatabaseProvider(_loggerMock.Object);
        }

        [Fact]
        public async Task InitializeAsync_LogsInitialization()
        {
            // Act
            await _provider.InitializeAsync();

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o != null && o.ToString().Contains("Initializing database")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task StoreChunksAsync_ReturnsSequentialIds()
        {
            // Arrange
            var chunks = new List<Chunk>
            {
                new Chunk("test1", 1, 10, "code1", ChunkType.Function, 1, Language.CSharp),
                new Chunk("test2", 1, 10, "code2", ChunkType.Function, 1, Language.CSharp),
                new Chunk("test3", 1, 10, "code3", ChunkType.Function, 1, Language.CSharp)
            };

            // Act
            var ids = await _provider.StoreChunksAsync(chunks);

            // Assert
            Assert.Equal(3, ids.Count);
            Assert.Equal(new List<int> { 1, 2, 3 }, ids);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o != null && o.ToString().Contains("Storing 3 chunks")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetChunksByHashesAsync_ReturnsEmptyList()
        {
            // Arrange
            var hashes = new List<string> { "hash1", "hash2" };

            // Act
            var chunks = await _provider.GetChunksByHashesAsync(hashes);

            // Assert
            Assert.Empty(chunks);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o != null && o.ToString().Contains("Retrieving chunks for 2 hashes")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InsertChunksBatchAsync_ReturnsSequentialIds()
        {
            // Arrange
            var chunks = new List<Chunk>
            {
                new Chunk("test1", 1, 10, "code1", ChunkType.Function, 1, Language.CSharp),
                new Chunk("test2", 1, 10, "code2", ChunkType.Function, 1, Language.CSharp)
            };

            // Act
            var ids = await _provider.InsertChunksBatchAsync(chunks);

            // Assert
            Assert.Equal(2, ids.Count);
            Assert.Equal(new List<int> { 1, 2 }, ids);
        }

        [Fact]
        public async Task InsertEmbeddingsBatchAsync_ReturnsEmbeddingsCount()
        {
            // Arrange
            var chunkIds = new List<int> { 1, 2, 3 };
            var embeddings = new List<List<float>>
            {
                new List<float> { 0.1f, 0.2f },
                new List<float> { 0.3f, 0.4f },
                new List<float> { 0.5f, 0.6f }
            };

            // Act
            var result = await _provider.InsertEmbeddingsBatchAsync(chunkIds, embeddings);

            // Assert
            Assert.Equal(3, result);
        }

        [Fact]
        public async Task FilterExistingEmbeddingsAsync_ReturnsEmptyList()
        {
            // Arrange
            var chunkIds = new List<long> { 1, 2, 3 };

            // Act
            var result = await _provider.FilterExistingEmbeddingsAsync(chunkIds, "provider", "model");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task InsertEmbeddingsBatchAsync_WithEmbeddingData_CompletesSuccessfully()
        {
            // Arrange
            var embeddingsData = new List<EmbeddingData>
            {
                new EmbeddingData(1, "provider", "model", 1536, new List<float> { 0.1f }, "success")
            };
            var chunkIdToStatus = new Dictionary<long, string> { { 1, "success" } };

            // Act
            await _provider.InsertEmbeddingsBatchAsync(embeddingsData, chunkIdToStatus);

            // Assert - No exception thrown
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o != null && o.ToString().Contains("Inserting batch of 1 embedding data")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteEmbeddingsForChunksAsync_CompletesSuccessfully()
        {
            // Arrange
            var chunkIds = new List<long> { 1, 2, 3 };

            // Act
            await _provider.DeleteEmbeddingsForChunksAsync(chunkIds, "provider", "model");

            // Assert - No exception thrown
        }

        [Fact]
        public async Task GetChunksByFilePathAsync_ReturnsEmptyList()
        {
            // Arrange
            var filePath = "/test/file.cs";

            // Act
            var chunks = await _provider.GetChunksByFilePathAsync(filePath);

            // Assert
            Assert.Empty(chunks);
        }

        [Fact]
        public async Task GetChunksByIdsAsync_ReturnsEmptyList()
        {
            // Arrange
            var chunkIds = new List<long> { 1, 2, 3 };

            // Act
            var chunks = await _provider.GetChunksByIdsAsync(chunkIds);

            // Assert
            Assert.Empty(chunks);
        }

        [Fact]
        public async Task GetFileByPathAsync_ReturnsNull()
        {
            // Arrange
            var filePath = "/test/file.cs";

            // Act
            var file = await _provider.GetFileByPathAsync(filePath);

            // Assert
            Assert.Null(file);
        }

        [Fact]
        public async Task UpsertFileAsync_ReturnsDummyId()
        {
            // Arrange
            var file = new File("/test/file.cs", 1234567890, Language.CSharp, 100, null, "hash123");

            // Act
            var id = await _provider.UpsertFileAsync(file);

            // Assert
            Assert.Equal(1, id);
        }

        [Fact]
        public async Task OptimizeTablesAsync_CompletesSuccessfully()
        {
            // Act
            await _provider.OptimizeTablesAsync();

            // Assert - No exception thrown
        }
    }
}