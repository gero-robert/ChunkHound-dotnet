using Xunit;
using ChunkHound.Providers;
using ChunkHound.Core;
using ChunkHound.Core.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Python.Runtime;

namespace ChunkHound.Core.Tests.Providers
{
    [CollectionDefinition("LanceDB", DisableParallelization = true)]
    public class LanceDBCollectionDefinition { }

    [Collection("LanceDB")]
    public class LanceDBProviderTests
    {
        private readonly Mock<ILogger<LanceDBProvider>> _loggerMock = new();

        private (string dbPath, LanceDBProvider provider) CreateTestProvider()
        {
            string tempDbPath = Path.Combine(Path.GetTempPath(), "ChunkHoundTestDB", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDbPath);
            LanceDBProvider provider = new LanceDBProvider(tempDbPath, _loggerMock.Object);
            return (tempDbPath, provider);
        }

        [Fact]
        public async Task InitializeAsync_CompletesSuccessfully()
        {
            // Skip if Python is not available
            if (!PythonTestHelper.IsPythonAvailable())
            {
                return; // Skip test gracefully
            }

            var (dbPath, provider) = CreateTestProvider();

            try
            {
                // Act
                await provider.InitializeAsync();

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
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task InitializeAsync_CalledTwice_OnlyInitializesOnce()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                // Act
                await provider.InitializeAsync();
                await provider.InitializeAsync();

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
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task StoreChunksAsync_EmptyList_ReturnsEmptyList()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                // Act
                var ids = await provider.StoreChunksAsync(new List<Chunk>());

                // Assert
                Assert.Empty(ids);
            }
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task StoreChunksAsync_WithChunks_ReturnsIds()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                var chunks = new List<Chunk>
                {
                    new Chunk("test1", 1, 10, "code1", ChunkType.Function, 1, Language.CSharp),
                    new Chunk("test2", 1, 10, "code2", ChunkType.Function, 1, Language.CSharp)
                };

                // Act
                var ids = await provider.StoreChunksAsync(chunks);

                // Assert
                Assert.Equal(2, ids.Count);
                Assert.Equal(1, ids[0]);
                Assert.Equal(2, ids[1]);
            }
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task StoreChunksAsync_WithExistingIds_PreservesIds()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                var chunk = new Chunk("test", 1, 10, "code", ChunkType.Function, 1, Language.CSharp) with { Id = 999 };

                // Act
                var ids = await provider.StoreChunksAsync(new List<Chunk> { chunk });

                // Assert
                Assert.Single(ids);
                Assert.Equal(999, ids[0]);
            }
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task GetChunksByHashesAsync_EmptyList_ReturnsEmptyList()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                // Act
                var chunks = await provider.GetChunksByHashesAsync(new List<string>());

                // Assert
                Assert.Empty(chunks);
            }
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task GetChunksByHashesAsync_NotInitialized_ThrowsInvalidOperationException()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    provider.GetChunksByHashesAsync(new List<string> { "hash" }));
            }
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task GetFragmentCountAsync_ReturnsDictionary()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                // Act
                var counts = await provider.GetFragmentCountAsync();

                // Assert
                Assert.NotNull(counts);
                Assert.Contains("chunks", counts.Keys);
                Assert.Contains("files", counts.Keys);
                Assert.Equal(0, counts["chunks"]);
                Assert.Equal(0, counts["files"]);
            }
            finally
            {
                provider.Dispose();
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task InsertChunksBatchAsync_CallsStoreChunksAsync()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                var chunks = new List<Chunk>
                {
                    new Chunk("test", 1, 10, "code", ChunkType.Function, 1, Language.CSharp)
                };

                // Act
                var ids = await provider.InsertChunksBatchAsync(chunks);

                // Assert
                Assert.Single(ids);
                Assert.Equal(1, ids[0]);
            }
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task InsertEmbeddingsBatchAsync_ReturnsEmbeddingsCount()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                var chunkIds = new List<int> { 1, 2 };
                var embeddings = new List<List<float>>
                {
                    new List<float> { 0.1f, 0.2f },
                    new List<float> { 0.3f, 0.4f }
                };

                // Act
                var result = await provider.InsertEmbeddingsBatchAsync(chunkIds, embeddings);

                // Assert
                Assert.Equal(2, result);
            }
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task FilterExistingEmbeddingsAsync_ReturnsEmptyList()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                var chunkIds = new List<long> { 1, 2, 3 };

                // Act
                var result = await provider.FilterExistingEmbeddingsAsync(chunkIds, "provider", "model");

                // Assert
                Assert.Empty(result);
            }
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task InsertEmbeddingsBatchAsync_WithEmbeddingData_CompletesSuccessfully()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                var embeddingsData = new List<EmbeddingData>
                {
                    new EmbeddingData(1, "provider", "model", 1536, new List<float> { 0.1f }, "success")
                };
                var chunkIdToStatus = new Dictionary<long, string> { { 1, "success" } };

                // Act
                await provider.InsertEmbeddingsBatchAsync(embeddingsData, chunkIdToStatus);

                // Assert - No exception thrown
            }
            finally
            {
                provider.Dispose();
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task DeleteEmbeddingsForChunksAsync_CompletesSuccessfully()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                var chunkIds = new List<long> { 1, 2, 3 };

                // Act
                await provider.DeleteEmbeddingsForChunksAsync(chunkIds, "provider", "model");

                // Assert - No exception thrown
            }
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task GetChunksByFilePathAsync_ReturnsEmptyList()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                // Act
                var chunks = await provider.GetChunksByFilePathAsync("/test/file.cs");

                // Assert
                Assert.Empty(chunks);
            }
            finally
            {
                provider.Dispose();
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task GetChunksByIdsAsync_ReturnsEmptyList()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                var chunkIds = new List<long> { 1, 2, 3 };

                // Act
                var chunks = await provider.GetChunksByIdsAsync(chunkIds);

                // Assert
                Assert.Empty(chunks);
            }
            finally
            {
                provider.Dispose();
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task GetFileByPathAsync_ReturnsNull()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                // Act
                var file = await provider.GetFileByPathAsync("/test/file.cs");

                // Assert
                Assert.Null(file);
            }
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task UpsertFileAsync_ReturnsFileId()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                var file = new File("/test/file.cs", 1234567890, Language.CSharp, 100, null, "hash123");

                // Act
                var id = await provider.UpsertFileAsync(file);

                // Assert
                Assert.Equal(1, id);
            }
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task UpsertFileAsync_WithExistingId_ReturnsExistingId()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                var file = new File("/test/file.cs", 1234567890, Language.CSharp, 100, 42, "hash123");

                // Act
                var id = await provider.UpsertFileAsync(file);

                // Assert
                Assert.Equal(42, id);
            }
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        [Fact]
        public async Task OptimizeTablesAsync_CompletesSuccessfully()
        {
            var (dbPath, provider) = CreateTestProvider();

            try
            {
                await provider.InitializeAsync();

                // Act
                await provider.OptimizeTablesAsync();

                // Assert - No exception thrown
            }
            finally
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }
    }
}