using Xunit;
using ChunkHound.Core;

namespace ChunkHound.Core.Tests.Models
{
    public class LanguageTests
    {
        [Theory]
        [InlineData(Language.CSharp, ".cs")]
        [InlineData(Language.Python, ".py")]
        [InlineData(Language.JavaScript, ".js")]
        [InlineData(Language.TypeScript, ".ts")]
        [InlineData(Language.Java, ".java")]
        [InlineData(Language.C, ".c")]
        [InlineData(Language.Cpp, ".cpp")]
        [InlineData(Language.Go, ".go")]
        [InlineData(Language.Rust, ".rs")]
        [InlineData(Language.PHP, ".php")]
        [InlineData(Language.Ruby, ".rb")]
        public void GetExtension_ReturnsCorrectExtension(Language language, string expected)
        {
            // Act
            var result = language.GetExtension();

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetExtension_InvalidLanguage_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var invalidLanguage = (Language)999;

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => invalidLanguage.GetExtension());
        }

        [Theory]
        [InlineData(Language.CSharp, "csharp")]
        [InlineData(Language.Python, "python")]
        [InlineData(Language.JavaScript, "javascript")]
        [InlineData(Language.TypeScript, "typescript")]
        [InlineData(Language.Java, "java")]
        [InlineData(Language.C, "c")]
        [InlineData(Language.Cpp, "cpp")]
        [InlineData(Language.Go, "go")]
        [InlineData(Language.Rust, "rust")]
        [InlineData(Language.PHP, "php")]
        [InlineData(Language.Ruby, "ruby")]
        public void Value_ReturnsCorrectString(Language language, string expected)
        {
            // Act
            var result = language.Value();

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Value_InvalidLanguage_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var invalidLanguage = (Language)999;

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => invalidLanguage.Value());
        }

        [Theory]
        [InlineData("csharp", Language.CSharp)]
        [InlineData("c#", Language.CSharp)]
        [InlineData("python", Language.Python)]
        [InlineData("py", Language.Python)]
        [InlineData("javascript", Language.JavaScript)]
        [InlineData("js", Language.JavaScript)]
        [InlineData("typescript", Language.TypeScript)]
        [InlineData("ts", Language.TypeScript)]
        [InlineData("java", Language.Java)]
        [InlineData("c", Language.C)]
        [InlineData("cpp", Language.Cpp)]
        [InlineData("c++", Language.Cpp)]
        [InlineData("go", Language.Go)]
        [InlineData("rust", Language.Rust)]
        [InlineData("rs", Language.Rust)]
        [InlineData("php", Language.PHP)]
        [InlineData("ruby", Language.Ruby)]
        [InlineData("rb", Language.Ruby)]
        [InlineData("CSHARP", Language.CSharp)] // Test case insensitivity
        [InlineData("invalid", Language.Unknown)]
        public void FromString_ReturnsCorrectLanguage(string value, Language expected)
        {
            // Act
            var result = LanguageExtensions.FromString(value);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}