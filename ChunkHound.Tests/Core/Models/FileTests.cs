using Xunit;
using ChunkHound.Core;
using ChunkHound.Core.Exceptions;

namespace ChunkHound.Core.Tests.Models
{
    public class FileTests
    {
        /// <summary>
        /// Tests that the File constructor creates a valid File instance when provided with valid parameters including path, modification time, language, and size.
        /// </summary>
        [Fact]
        public void Constructor_ValidParameters_CreatesFile()
        {
            // Arrange
            var path = "src/main.cs";
            var mtime = 1640995200.0; // 2022-01-01
            var language = Language.CSharp;
            var sizeBytes = 1024L;

            // Act
            var file = new File(path, mtime, language, sizeBytes);

            // Assert
            Assert.Equal(path, file.Path);
            Assert.Equal(mtime, file.Mtime);
            Assert.Equal(language, file.Language);
            Assert.Equal(sizeBytes, file.SizeBytes);
        }

        /// <summary>
        /// Tests that the File constructor throws a ValidationException when an invalid path (empty string) is provided.
        /// </summary>
        [Fact]
        public void Constructor_InvalidPath_ThrowsValidationException()
        {
            // Arrange
            var language = Language.CSharp;

            // Act & Assert
            Assert.Throws<ValidationException>(() =>
                new File("", 1640995200.0, language, 1024));
        }

        /// <summary>
        /// Tests that the Name property correctly extracts and returns the filename from the file path.
        /// </summary>
        [Fact]
        public void Name_ReturnsFileName()
        {
            // Arrange
            var file = new File("src/main.cs", 1640995200.0, Language.CSharp, 1024);

            // Act
            var name = file.Name;

            // Assert
            Assert.Equal("main.cs", name);
        }

        /// <summary>
        /// Tests that the Extension property correctly extracts and returns the file extension from the file path.
        /// </summary>
        [Fact]
        public void Extension_ReturnsFileExtension()
        {
            // Arrange
            var file = new File("src/main.cs", 1640995200.0, Language.CSharp, 1024);

            // Act
            var extension = file.Extension;

            // Assert
            Assert.Equal(".cs", extension);
        }

        /// <summary>
        /// Tests that IsSupportedLanguage returns false for files with unknown or unsupported language types.
        /// </summary>
        [Fact]
        public void IsSupportedLanguage_UnknownLanguage_ReturnsFalse()
        {
            // Arrange
            var file = new File("unknown.xyz", 1640995200.0, Language.Unknown, 1024);

            // Act
            var isSupported = file.IsSupportedLanguage();

            // Assert
            Assert.False(isSupported);
        }

        /// <summary>
        /// Tests that FromDict correctly creates a File instance from a valid dictionary containing all required fields.
        /// </summary>
        [Fact]
        public void FromDict_ValidData_CreatesFile()
        {
            // Arrange
            var data = new Dictionary<string, object>
            {
                ["path"] = "src/main.cs",
                ["mtime"] = 1640995200.0,
                ["language"] = "csharp",
                ["size_bytes"] = 1024L
            };

            // Act
            var file = File.FromDict(data);

            // Assert
            Assert.Equal("src/main.cs", file.Path);
            Assert.Equal(1640995200.0, file.Mtime);
            Assert.Equal(Language.CSharp, file.Language);
            Assert.Equal(1024L, file.SizeBytes);
        }

        /// <summary>
        /// Tests that ToDict correctly converts a File instance to a dictionary with the appropriate field mappings.
        /// </summary>
        [Fact]
        public void ToDict_ConvertsCorrectly()
        {
            // Arrange
            var file = new File("src/main.cs", 1640995200.0, Language.CSharp, 1024);

            // Act
            var dict = file.ToDict();

            // Assert
            Assert.Equal("src/main.cs", dict["path"]);
            Assert.Equal(1640995200.0, dict["mtime"]);
            Assert.Equal("csharp", dict["language"]);
            Assert.Equal(1024L, dict["size_bytes"]);
        }

        /// <summary>
        /// Tests that WithId creates a new File instance with the specified ID while preserving other properties.
        /// </summary>
        [Fact]
        public void WithId_CreatesNewInstanceWithId()
        {
            // Arrange
            var file = new File("src/main.cs", 1640995200.0, Language.CSharp, 1024);

            // Act
            var fileWithId = file.WithId(123);

            // Assert
            Assert.Equal(123, fileWithId.Id);
            Assert.Equal(file.Path, fileWithId.Path); // Other properties unchanged
        }
    }
}