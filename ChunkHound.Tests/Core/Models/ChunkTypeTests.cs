using Xunit;
using ChunkHound.Core;

namespace ChunkHound.Core.Tests.Models
{
    public class ChunkTypeTests
    {
        /// <summary>
        /// Tests that ChunkType.Value() returns the correct string representation for all valid chunk types.
        /// This ensures proper serialization and display of chunk type information.
        /// </summary>
        [Theory]
        [InlineData(ChunkType.Function, "function")]
        [InlineData(ChunkType.Class, "class")]
        [InlineData(ChunkType.Interface, "interface")]
        [InlineData(ChunkType.Struct, "struct")]
        [InlineData(ChunkType.Enum, "enum")]
        [InlineData(ChunkType.Module, "module")]
        [InlineData(ChunkType.Documentation, "documentation")]
        [InlineData(ChunkType.Import, "import")]
        [InlineData(ChunkType.Unknown, "unknown")]
        public void Value_ReturnsCorrectString(ChunkType chunkType, string expected)
        {
            // Act
            var result = chunkType.Value();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ChunkType.Value() throws ArgumentOutOfRangeException for invalid enum values.
        /// This ensures robust error handling when dealing with corrupted or unknown chunk types.
        /// </summary>
        [Fact]
        public void Value_InvalidChunkType_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var invalidChunkType = (ChunkType)999;

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => invalidChunkType.Value());
        }

        /// <summary>
        /// Tests that ChunkType.IsCode() correctly identifies code-related chunk types.
        /// This ensures proper categorization for code analysis and processing workflows.
        /// </summary>
        [Theory]
        [InlineData(ChunkType.Function, true)]
        [InlineData(ChunkType.Class, true)]
        [InlineData(ChunkType.Interface, true)]
        [InlineData(ChunkType.Struct, true)]
        [InlineData(ChunkType.Enum, true)]
        [InlineData(ChunkType.Module, true)]
        [InlineData(ChunkType.Documentation, false)]
        [InlineData(ChunkType.Import, false)]
        [InlineData(ChunkType.Unknown, false)]
        public void IsCode_ReturnsCorrectValue(ChunkType chunkType, bool expected)
        {
            // Act
            var result = chunkType.IsCode();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ChunkType.IsDocumentation() correctly identifies documentation chunk types.
        /// This ensures proper handling of documentation content in processing pipelines.
        /// </summary>
        [Theory]
        [InlineData(ChunkType.Documentation, true)]
        [InlineData(ChunkType.Function, false)]
        [InlineData(ChunkType.Class, false)]
        [InlineData(ChunkType.Unknown, false)]
        public void IsDocumentation_ReturnsCorrectValue(ChunkType chunkType, bool expected)
        {
            // Act
            var result = chunkType.IsDocumentation();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ChunkTypeExtensions.FromString() correctly parses string representations to ChunkType enum values.
        /// This includes case-insensitive parsing and handling of unknown values.
        /// </summary>
        [Theory]
        [InlineData("function", ChunkType.Function)]
        [InlineData("class", ChunkType.Class)]
        [InlineData("interface", ChunkType.Interface)]
        [InlineData("struct", ChunkType.Struct)]
        [InlineData("enum", ChunkType.Enum)]
        [InlineData("module", ChunkType.Module)]
        [InlineData("documentation", ChunkType.Documentation)]
        [InlineData("import", ChunkType.Import)]
        [InlineData("unknown", ChunkType.Unknown)]
        [InlineData("FUNCTION", ChunkType.Function)] // Test case insensitivity
        [InlineData("invalid", ChunkType.Unknown)]
        public void FromString_ReturnsCorrectChunkType(string value, ChunkType expected)
        {
            // Act
            var result = ChunkTypeExtensions.FromString(value);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}