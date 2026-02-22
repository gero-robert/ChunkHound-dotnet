using Xunit;
using ChunkHound.Parsers.Concrete;
using ChunkHound.Core;
using System.Threading.Tasks;

namespace ChunkHound.Tests.Parsers
{
    public class UniversalTextParserTests
    {
        private readonly UniversalTextParser _parser;

        public UniversalTextParserTests()
        {
            _parser = new UniversalTextParser();
        }

        [Theory]
        [InlineData(".txt")]
        [InlineData(".log")]
        [InlineData(".json")]
        [InlineData(".xml")]
        [InlineData(".html")]
        [InlineData(".css")]
        public void CanHandle_SupportedExtensions_ReturnsTrue(string extension)
        {
            Assert.True(_parser.CanHandle(extension));
            Assert.True(_parser.CanHandle(extension.ToUpper()));
        }

        [Fact]
        public void CanHandle_AnyExtension_ReturnsTrue()
        {
            // UniversalTextParser is a fallback, so it handles any extension
            Assert.True(_parser.CanHandle(".unknown"));
            Assert.True(_parser.CanHandle(".xyz"));
        }

        [Fact]
        public async Task ParseAsync_SmallTextFile_ReturnsSingleChunk()
        {
            // Arrange
            var content = @"This is a small text file.
It has multiple lines.
But not too many.";

            // Act
            var chunks = await _parser.ParseAsync(content, "test.txt");

            // Assert
            Assert.NotNull(chunks);
            Assert.Single(chunks);
            var chunk = chunks[0];
            Assert.Equal(ChunkType.Unknown, chunk.ChunkType);
            Assert.Contains("small text file", chunk.Code);
        }

        [Fact]
        public async Task ParseAsync_EmptyTextFile_ReturnsEmptyChunks()
        {
            // Arrange
            var emptyContent = "";

            // Act
            var chunks = await _parser.ParseAsync(emptyContent, "empty.txt");

            // Assert
            Assert.NotNull(chunks);
            Assert.Empty(chunks);
        }

        [Fact]
        public async Task ParseAsync_WhitespaceOnly_ReturnsEmptyChunks()
        {
            // Arrange
            var whitespaceContent = "   \n\t\n  ";

            // Act
            var chunks = await _parser.ParseAsync(whitespaceContent, "whitespace.txt");

            // Assert
            Assert.NotNull(chunks);
            Assert.Empty(chunks);
        }
    }
}