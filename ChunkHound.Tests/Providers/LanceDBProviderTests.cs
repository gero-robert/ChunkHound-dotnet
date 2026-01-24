using Xunit;
using ChunkHound.Providers;
using ChunkHound.Core;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace ChunkHound.Core.Tests.Providers
{
    public class LanceDBProviderTests : IDisposable
    {
        private readonly Mock<ILogger<LanceDBProvider>> _loggerMock;
        private readonly string _tempDbPath;
        private LanceDBProvider? _provider;

        public LanceDBProviderTests()
        {
            _loggerMock = new Mock<ILogger<LanceDBProvider>>();
            _tempDbPath = Path.Combine(Path.GetTempPath(), "ChunkHoundTestDB", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDbPath);
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Act
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);

            // Assert
            Assert.NotNull(_provider);
        }

        [Fact]
        public void Constructor_NullDbPath_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new LanceDBProvider(null!, _loggerMock.Object));
        }

        [Fact]
        public async Task InitializeAsync_CompletesSuccessfully()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);

            // Act
            await _provider.InitializeAsync();

            // Assert - No exception thrown, and logger was called
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("LanceDB database initialized successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InitializeAsync_CalledTwice_OnlyInitializesOnce()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);

            // Act
            await _provider.InitializeAsync();
            await _provider.InitializeAsync();

            // Assert - Logger should only be called once
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("LanceDB database initialized successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task StoreChunksAsync_EmptyList_ReturnsEmptyList()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

            // Act
            var ids = await _provider.StoreChunksAsync(new List<Chunk>());

            // Assert
            Assert.Empty(ids);
        }

        [Fact]
        public async Task StoreChunksAsync_WithChunks_ReturnsIds()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

            var chunks = new List<Chunk>
            {
                new Chunk("test1", 1, 10, "code1", ChunkType.Function, 1, Language.CSharp),
                new Chunk("test2", 1, 10, "code2", ChunkType.Function, 1, Language.CSharp)
            };

            // Act
            var ids = await _provider.StoreChunksAsync(chunks);

            // Assert
            Assert.Equal(2, ids.Count);
            Assert.Equal(1, ids[0]);
            Assert.Equal(2, ids[1]);
        }

        [Fact]
        public async Task StoreChunksAsync_WithExistingIds_PreservesIds()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

            var chunk = new Chunk("test", 1, 10, "code", ChunkType.Function, 1, Language.CSharp) with { Id = 999 };

            // Act
            var ids = await _provider.StoreChunksAsync(new List<Chunk> { chunk });

            // Assert
            Assert.Single(ids);
            Assert.Equal(999, ids[0]);
        }

        [Fact]
        public async Task GetChunksByHashesAsync_EmptyList_ReturnsEmptyList()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

            // Act
            var chunks = await _provider.GetChunksByHashesAsync(new List<string>());

            // Assert
            Assert.Empty(chunks);
        }

        [Fact]
        public async Task GetChunksByHashesAsync_NotInitialized_ThrowsInvalidOperationException()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _provider.GetChunksByHashesAsync(new List<string> { "hash" }));
        }

        [Fact]
        public async Task GetFragmentCountAsync_ReturnsDictionary()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

            // Act
            var counts = await _provider.GetFragmentCountAsync();

            // Assert
            Assert.NotNull(counts);
            Assert.Contains("chunks", counts.Keys);
            Assert.Contains("files", counts.Keys);
            Assert.Equal(0, counts["chunks"]);
            Assert.Equal(0, counts["files"]);
        }

        [Fact]
        public async Task InsertChunksBatchAsync_CallsStoreChunksAsync()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

            var chunks = new List<Chunk>
            {
                new Chunk("test", 1, 10, "code", ChunkType.Function, 1, Language.CSharp)
            };

            // Act
            var ids = await _provider.InsertChunksBatchAsync(chunks);

            // Assert
            Assert.Single(ids);
            Assert.Equal(1, ids[0]);
        }

        [Fact]
        public async Task InsertEmbeddingsBatchAsync_ReturnsEmbeddingsCount()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

            var chunkIds = new List<int> { 1, 2 };
            var embeddings = new List<List<float>>
            {
                new List<float> { 0.1f, 0.2f },
                new List<float> { 0.3f, 0.4f }
            };

            // Act
            var result = await _provider.InsertEmbeddingsBatchAsync(chunkIds, embeddings);

            // Assert
            Assert.Equal(2, result);
        }

        [Fact]
        public async Task FilterExistingEmbeddingsAsync_ReturnsEmptyList()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

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
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

            var embeddingsData = new List<EmbeddingData>
            {
                new EmbeddingData(1, "provider", "model", 1536, new List<float> { 0.1f }, "success")
            };
            var chunkIdToStatus = new Dictionary<long, string> { { 1, "success" } };

            // Act
            await _provider.InsertEmbeddingsBatchAsync(embeddingsData, chunkIdToStatus);

            // Assert - No exception thrown
        }

        [Fact]
        public async Task DeleteEmbeddingsForChunksAsync_CompletesSuccessfully()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

            var chunkIds = new List<long> { 1, 2, 3 };

            // Act
            await _provider.DeleteEmbeddingsForChunksAsync(chunkIds, "provider", "model");

            // Assert - No exception thrown
        }

        [Fact]
        public async Task GetChunksByFilePathAsync_ReturnsEmptyList()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

            // Act
            var chunks = await _provider.GetChunksByFilePathAsync("/test/file.cs");

            // Assert
            Assert.Empty(chunks);
        }

        [Fact]
        public async Task GetChunksByIdsAsync_ReturnsEmptyList()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

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
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

            // Act
            var file = await _provider.GetFileByPathAsync("/test/file.cs");

            // Assert
            Assert.Null(file);
        }

        [Fact]
        public async Task UpsertFileAsync_ReturnsFileId()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

            var file = new File("/test/file.cs", 1234567890, Language.CSharp, 100, null, "hash123");

            // Act
            var id = await _provider.UpsertFileAsync(file);

            // Assert
            Assert.Equal(1, id);
        }

        [Fact]
        public async Task UpsertFileAsync_WithExistingId_ReturnsExistingId()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

            var file = new File("/test/file.cs", 1234567890, Language.CSharp, 100, 42, "hash123");

            // Act
            var id = await _provider.UpsertFileAsync(file);

            // Assert
            Assert.Equal(42, id);
        }

        [Fact]
        public async Task OptimizeTablesAsync_CompletesSuccessfully()
        {
            // Arrange
            _provider = new LanceDBProvider(_tempDbPath, _loggerMock.Object);
            await _provider.InitializeAsync();

            // Act
            await _provider.OptimizeTablesAsync();

            // Assert - No exception thrown
        }

        public void Dispose()
        {
            _provider?.Dispose();
            if (Directory.Exists(_tempDbPath))
            {
                Directory.Delete(_tempDbPath, true);
            }
        }
    }
}