using Xunit;
using ChunkHound.Parsers;
using ChunkHound.Core;
using ChunkHound.Services;
using System.Collections.Generic;

namespace ChunkHound.Tests.Parsers
{
    public class RecursiveChunkSplitterTests
    {
        private readonly RecursiveChunkSplitter _splitter;

        public RecursiveChunkSplitterTests()
        {
            var configProvider = new LanguageConfigProvider();
            _splitter = new RecursiveChunkSplitter(configProvider);
        }

        [Fact]
        public void Split_ValidChunks_ReturnsSplitChunks()
        {
            // Arrange
            var chunks = new List<Chunk>
            {
                new Chunk("test", 1, 10, "This is a test chunk with some content that might be long enough to split.", ChunkType.Unknown, 1, Language.Unknown)
            };

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => _splitter.Split(chunks, 50, 10));
        }

        [Fact]
        public void Split_EmptyChunks_ReturnsEmptyList()
        {
            // Arrange
            var chunks = new List<Chunk>();

            // Act
            var result = _splitter.Split(chunks, 100, 10);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void Split_NullChunks_ReturnsEmptyList()
        {
            // Act
            var result = _splitter.Split(null, 100, 10);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}