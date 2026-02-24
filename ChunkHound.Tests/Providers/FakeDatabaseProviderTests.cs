using Xunit;
using ChunkHound.Providers;
using ChunkHound.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace ChunkHound.Core.Tests.Providers
{
    public class FakeDatabaseProviderTests
    {
        private readonly FakeDatabaseProvider _provider;

        public FakeDatabaseProviderTests()
        {
            _provider = new FakeDatabaseProvider();
        }

        [Fact]
        public async Task InitializeAsync_CompletesSuccessfully()
        {
            // Act
            await _provider.InitializeAsync();

            // Assert - No exception thrown
            Assert.True(true);
        }

        [Fact]
        public async Task StoreChunksAsync_AssignsIdsAndStoresChunks()
        {
            // Arrange
            var chunks = new List<Chunk>
            {
                new Chunk("1", 1, "code1", 1, 10, Language.CSharp, ChunkType.Function),
                new Chunk("2", 1, "code2", 1, 10, Language.CSharp, ChunkType.Function)
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
            var chunk = new Chunk("999", 1, "code", 1, 10, Language.CSharp, ChunkType.Function);

            // Act
            var ids = await _provider.StoreChunksAsync(new List<Chunk> { chunk });

            // Assert
            Assert.Single(ids);
            Assert.Equal(999, ids[0]);
        }

        [Fact]
        public async Task GetChunksByHashesAsync_ReturnsMatchingChunks()
        {
            // Arrange
            var chunk = new Chunk("1", 1, "unique code", 1, 10, Language.CSharp, ChunkType.Function);
            await _provider.StoreChunksAsync(new List<Chunk> { chunk });
            var hash = ChunkHound.Core.Utilities.HashUtility.ComputeContentHash("unique code");

            // Act
            var retrieved = await _provider.GetChunksByHashesAsync(new List<string> { hash });

            // Assert
            Assert.Single(retrieved);
            Assert.Equal("unique code", retrieved[0].Code);
        }

        [Fact]
        public async Task GetChunksByHashesAsync_NonExistentHash_ReturnsEmptyList()
        {
            // Act
            var retrieved = await _provider.GetChunksByHashesAsync(new List<string> { "nonexistent" });

            // Assert
            Assert.Empty(retrieved);
        }

        [Fact]
        public async Task InsertChunksBatchAsync_CallsStoreChunksAsync()
        {
            // Arrange
            var chunks = new List<Chunk>
            {
                new Chunk("1", 1, "code", 1, 10, Language.CSharp, ChunkType.Function)
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
        public async Task GetChunksByFilePathAsync_ReturnsMatchingChunks()
        {
            // Arrange
            var chunk1 = new Chunk("1", 1, "code1", 1, 10, Language.CSharp, ChunkType.Function) with { FilePath = "/test/file.cs" };
            var chunk2 = new Chunk("2", 1, "code2", 1, 10, Language.CSharp, ChunkType.Function) with { FilePath = "/other/file.cs" };
            await _provider.StoreChunksAsync(new List<Chunk> { chunk1, chunk2 });

            // Act
            var retrieved = await _provider.GetChunksByFilePathAsync("/test/file.cs");

            // Assert
            Assert.Single(retrieved);
            Assert.Equal("code1", retrieved[0].Code);
        }

        [Fact]
        public async Task GetChunksByIdsAsync_ReturnsMatchingChunks()
        {
            // Arrange
            var chunk = new Chunk("1", 1, "code", 1, 10, Language.CSharp, ChunkType.Function);
            await _provider.StoreChunksAsync(new List<Chunk> { chunk });

            // Act
            var retrieved = await _provider.GetChunksByIdsAsync(new List<long> { 1 });

            // Assert
            Assert.Single(retrieved);
            Assert.Equal("code", retrieved[0].Code);
        }

        [Fact]
        public async Task GetFileByPathAsync_ReturnsNull()
        {
            // Act
            var file = await _provider.GetFileByPathAsync("/test/file.cs");

            // Assert
            Assert.Null(file);
        }

        [Fact]
        public async Task UpsertFileAsync_ReturnsFileId()
        {
            // Arrange
            var file = new File("/test/file.cs", 1234567890, Language.CSharp, 100, null, "hash123");

            // Act
            var id = await _provider.UpsertFileAsync(file);

            // Assert
            Assert.Equal(1, id); // Since file.Id is null, returns 1
        }

        [Fact]
        public async Task UpsertFileAsync_WithExistingId_ReturnsExistingId()
        {
            // Arrange
            var file = new File("/test/file.cs", 1234567890, Language.CSharp, 100, 42, "hash123");

            // Act
            var id = await _provider.UpsertFileAsync(file);

            // Assert
            Assert.Equal(42, id);
        }

        [Fact]
        public async Task OptimizeTablesAsync_CompletesSuccessfully()
        {
            // Act
            await _provider.OptimizeTablesAsync();

            // Assert - No exception thrown
        }

        [Fact]
        public async Task SequentialOperations_WorkCorrectly()
        {
            // Arrange
            for (int i = 0; i < 10; i++)
            {
                var chunk = new Chunk($"{i + 1}", 1, $"code{i}", 1, 10, Language.CSharp, ChunkType.Function);
                await _provider.StoreChunksAsync(new List<Chunk> { chunk });
            }

            // Act
            var allChunks = await _provider.GetChunksByIdsAsync(new List<long> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

            // Assert
            Assert.Equal(10, allChunks.Count);
        }

        [Fact]
        public async Task UpsertChunksAsync_UpsertsChunks()
        {
            // Arrange
            var chunk1 = new Chunk("1", 1, "code1", 1, 10, Language.CSharp, ChunkType.Function);
            var chunk2 = new Chunk("2", 1, "code2", 1, 10, Language.CSharp, ChunkType.Function);

            // Act
            await _provider.UpsertChunksAsync(new List<Chunk> { chunk1, chunk2 });

            // Assert
            var retrieved = await _provider.GetChunksByIdsAsync(new List<long> { 1, 2 });
            Assert.Equal(2, retrieved.Count);
        }

        [Fact]
        public async Task SearchAsync_ReturnsSimilarChunks()
        {
            // Arrange
            var embeddingArray = new float[1536];
            for (int i = 0; i < embeddingArray.Length; i++) embeddingArray[i] = 1.0f;
            var embedding = new ReadOnlyMemory<float>(embeddingArray);
            var chunk = new Chunk("1", 1, "code", 1, 10, Language.CSharp, ChunkType.Function) with { Embedding = embedding };
            await _provider.UpsertChunksAsync(new List<Chunk> { chunk });

            // Act
            var results = await _provider.SearchAsync(embedding, 0.5f, 10);

            // Assert
            Assert.Single(results);
            Assert.Equal("code", results[0].Code);
        }

        [Fact]
        public async Task DeleteFileChunksAsync_DeletesChunksForFile()
        {
            // Arrange
            var chunk1 = new Chunk("1", 1, "code1", 1, 10, Language.CSharp, ChunkType.Function);
            var chunk2 = new Chunk("2", 2, "code2", 1, 10, Language.CSharp, ChunkType.Function);
            await _provider.UpsertChunksAsync(new List<Chunk> { chunk1, chunk2 });

            // Act
            await _provider.DeleteFileChunksAsync(1);

            // Assert
            var retrieved = await _provider.GetChunksByIdsAsync(new List<long> { 1, 2 });
            Assert.Single(retrieved);
            Assert.Equal("2", retrieved[0].Id);
        }
    }
}