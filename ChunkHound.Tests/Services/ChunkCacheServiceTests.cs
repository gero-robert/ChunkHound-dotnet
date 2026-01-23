using Xunit;
using Moq;
using ChunkHound.Services;
using ChunkHound.Core;
using System.Collections.Generic;

namespace ChunkHound.Tests.Services
{
    public class ChunkCacheServiceTests
    {
        private readonly ChunkCacheService _service;

        public ChunkCacheServiceTests()
        {
            _service = new ChunkCacheService();
        }

        /// <summary>
        /// Tests that DiffChunks correctly identifies unchanged chunks when new and existing chunks have identical content.
        /// This validates the core caching logic that prevents unnecessary re-embedding of unchanged code,
        /// ensuring efficient indexing performance and cost savings in production.
        /// </summary>
        [Fact]
        public void DiffChunks_NoChanges_ReturnsUnchanged()
        {
            // Arrange
            var chunk1 = new Chunk("func", 1, 3, "function test() {}", ChunkType.Function, 1, Language.JavaScript);
            var chunk2 = new Chunk("class", 5, 7, "class Test {}", ChunkType.Class, 1, Language.JavaScript);

            var newChunks = new List<Chunk> { chunk1, chunk2 };
            var existingChunks = new List<Chunk> { chunk1, chunk2 };

            // Act
            var result = _service.DiffChunks(newChunks, existingChunks);

            // Assert
            Assert.Equal(2, result.Unchanged.Count);
            Assert.Empty(result.Added);
            Assert.Empty(result.Deleted);
            Assert.Empty(result.Modified);
        }

        /// <summary>
        /// Tests that DiffChunks correctly identifies newly added chunks not present in existing chunks.
        /// This ensures that new code additions are properly detected for embedding and indexing,
        /// maintaining data completeness in the chunk database.
        /// </summary>
        [Fact]
        public void DiffChunks_NewChunk_ReturnsAdded()
        {
            // Arrange
            var existingChunk = new Chunk("func", 1, 3, "function test() {}", ChunkType.Function, 1, Language.JavaScript);
            var newChunk = new Chunk("class", 5, 7, "class Test {}", ChunkType.Class, 1, Language.JavaScript);

            var newChunks = new List<Chunk> { existingChunk, newChunk };
            var existingChunks = new List<Chunk> { existingChunk };

            // Act
            var result = _service.DiffChunks(newChunks, existingChunks);

            // Assert
            Assert.Single(result.Unchanged);
            Assert.Single(result.Added);
            Assert.Empty(result.Deleted);
            Assert.Empty(result.Modified);
        }

        /// <summary>
        /// Tests that DiffChunks correctly identifies chunks that have been removed from the new set.
        /// This validates deletion detection to ensure obsolete chunks are cleaned up from the database,
        /// preventing stale data accumulation and maintaining accurate indexing.
        /// </summary>
        [Fact]
        public void DiffChunks_DeletedChunk_ReturnsDeleted()
        {
            // Arrange
            var existingChunk = new Chunk("func", 1, 3, "function test() {}", ChunkType.Function, 1, Language.JavaScript);
            var newChunk = new Chunk("class", 5, 7, "class Test {}", ChunkType.Class, 1, Language.JavaScript);

            var newChunks = new List<Chunk> { newChunk };
            var existingChunks = new List<Chunk> { existingChunk, newChunk };

            // Act
            var result = _service.DiffChunks(newChunks, existingChunks);

            // Assert
            Assert.Single(result.Unchanged);
            Assert.Empty(result.Added);
            Assert.Single(result.Deleted);
            Assert.Empty(result.Modified);
        }

        /// <summary>
        /// Tests that NormalizeCodeForComparison properly normalizes line endings across different platforms (CRLF to LF).
        /// This ensures consistent content comparison regardless of file origin, preventing false differences
        /// due to line ending variations and maintaining accurate caching behavior.
        /// </summary>
        [Fact]
        public void NormalizeCodeForComparison_NormalizesLineEndings()
        {
            // Arrange
            var code = "function test() {\r\n  return true;\r\n}";

            // Act
            var normalized = _service.GetType()
                .GetMethod("NormalizeCodeForComparison", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(_service, new object[] { code }) as string;

            // Assert
            Assert.Equal("function test() {\n  return true;\n}", normalized);
        }
    }
}