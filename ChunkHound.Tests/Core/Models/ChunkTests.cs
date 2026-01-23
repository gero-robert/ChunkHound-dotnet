using Xunit;
using ChunkHound.Core;
using ChunkHound.Core.Exceptions;

namespace ChunkHound.Core.Tests.Models
{
    public class ChunkTests
    {
        /// <summary>
        /// Tests that the Chunk constructor throws a ValidationException when start line is invalid.
        /// </summary>
        [Fact]
        public void Constructor_InvalidStartLine_ThrowsValidationException()
        {
            // Arrange
            var language = Language.CSharp;

            // Act & Assert
            Assert.Throws<ValidationException>(() =>
                new Chunk("Main", 0, 10, "code", ChunkType.Function, 1, language));
        }

        /// <summary>
        /// Tests that the Chunk constructor throws a ValidationException when end line is invalid.
        /// </summary>
        [Fact]
        public void Constructor_InvalidEndLine_ThrowsValidationException()
        {
            // Arrange
            var language = Language.CSharp;

            // Act & Assert
            Assert.Throws<ValidationException>(() =>
                new Chunk("Main", 1, 0, "code", ChunkType.Function, 1, language));
        }

        /// <summary>
        /// Tests that the Chunk constructor throws a ValidationException when code is empty.
        /// </summary>
        [Fact]
        public void Constructor_EmptyCode_ThrowsValidationException()
        {
            // Arrange
            var language = Language.CSharp;

            // Act & Assert
            Assert.Throws<ValidationException>(() =>
                new Chunk("Main", 1, 10, "", ChunkType.Function, 1, language));
        }

        /// <summary>
        /// Tests that FromDict correctly creates a Chunk instance from a valid dictionary.
        /// </summary>
        [Fact]
        public void FromDict_ValidData_CreatesChunk()
        {
            // Arrange
            var data = new Dictionary<string, object>
            {
                ["symbol"] = "Main",
                ["start_line"] = 1,
                ["end_line"] = 10,
                ["code"] = "public static void Main() { }",
                ["chunk_type"] = "function",
                ["file_id"] = 1,
                ["language"] = "csharp"
            };

            // Act
            var chunk = Chunk.FromDict(data);

            // Assert
            Assert.Equal("Main", chunk.Symbol);
            Assert.Equal(1, chunk.StartLine);
            Assert.Equal(10, chunk.EndLine);
            Assert.Equal("public static void Main() { }", chunk.Code);
            Assert.Equal(ChunkType.Function, chunk.ChunkType);
            Assert.Equal(1, chunk.FileId);
            Assert.Equal(Language.CSharp, chunk.Language);
        }

        /// <summary>
        /// Tests that ToDict correctly converts a Chunk instance to a dictionary.
        /// </summary>
        [Fact]
        public void ToDict_ConvertsCorrectly()
        {
            // Arrange
            var chunk = new Chunk("Main", 1, 10, "code", ChunkType.Function, 1, Language.CSharp);

            // Act
            var dict = chunk.ToDict();

            // Assert
            Assert.Equal("Main", dict["symbol"]);
            Assert.Equal(1, dict["start_line"]);
            Assert.Equal(10, dict["end_line"]);
            Assert.Equal("code", dict["code"]);
            Assert.Equal("function", dict["chunk_type"]);
            Assert.Equal(1, dict["file_id"]);
            Assert.Equal("csharp", dict["language"]);
        }

        /// <summary>
        /// Tests that WithId creates a new Chunk instance with the specified ID.
        /// </summary>
        [Fact]
        public void WithId_CreatesNewInstanceWithId()
        {
            // Arrange
            var chunk = new Chunk("Main", 1, 10, "code", ChunkType.Function, 1, Language.CSharp);

            // Act
            var chunkWithId = chunk.WithId(123);

            // Assert
            Assert.Equal(123, chunkWithId.Id);
            Assert.Equal(chunk.Symbol, chunkWithId.Symbol); // Other properties unchanged
        }
    }
}