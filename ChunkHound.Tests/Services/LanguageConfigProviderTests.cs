using Xunit;
using ChunkHound.Core;
using ChunkHound.Services;
using System.Collections.Generic;

namespace ChunkHound.Core.Tests.Services
{
    public class LanguageConfigProviderTests
    {
        private readonly LanguageConfigProvider _provider;

        public LanguageConfigProviderTests()
        {
            _provider = new LanguageConfigProvider();
        }

        [Theory]
        [InlineData(Language.CSharp, 1200, 50, 6000)]
        [InlineData(Language.Python, 1000, 40, 5500)]
        [InlineData(Language.JavaScript, 1100, 45, 5800)]
        [InlineData(Language.TypeScript, 1150, 48, 5900)]
        [InlineData(Language.Java, 1250, 55, 6200)]
        [InlineData(Language.Go, 1050, 42, 5700)]
        [InlineData(Language.Rust, 1180, 52, 6000)]
        public void GetConfig_SupportedLanguages_ReturnsCorrectConfig(Language language, int expectedMaxChunkSize, int expectedMinChunkSize, int expectedSafeTokenLimit)
        {
            // Act
            var config = _provider.GetConfig(language);

            // Assert
            Assert.NotNull(config);
            Assert.Equal(expectedMaxChunkSize, config.MaxChunkSize);
            Assert.Equal(expectedMinChunkSize, config.MinChunkSize);
            Assert.Equal(expectedSafeTokenLimit, config.SafeTokenLimit);
            Assert.NotNull(config.ChunkStartKeywords);
            Assert.NotNull(config.TypePatterns);
            Assert.NotNull(config.SymbolPatterns);
        }

        [Fact]
        public void GetConfig_UnknownLanguage_ReturnsDefaultConfig()
        {
            // Act
            var config = _provider.GetConfig(Language.Unknown);

            // Assert
            Assert.NotNull(config);
            Assert.Equal(1200, config.MaxChunkSize);
            Assert.Equal(50, config.MinChunkSize);
            Assert.Equal(6000, config.SafeTokenLimit);
            Assert.NotNull(config.ChunkStartKeywords);
            Assert.NotNull(config.TypePatterns);
            Assert.NotNull(config.SymbolPatterns);
            Assert.Empty(config.ChunkStartKeywords);
            Assert.Empty(config.TypePatterns);
            Assert.Empty(config.SymbolPatterns);
        }

        [Fact]
        public void GetConfig_CSharp_HasCorrectChunkStartKeywords()
        {
            // Act
            var config = _provider.GetConfig(Language.CSharp);

            // Assert
            Assert.Contains("using ", config.ChunkStartKeywords);
            Assert.Contains("namespace ", config.ChunkStartKeywords);
            Assert.Contains("public class ", config.ChunkStartKeywords);
            Assert.Contains("public interface ", config.ChunkStartKeywords);
            Assert.Contains("public enum ", config.ChunkStartKeywords);
        }

        [Fact]
        public void GetConfig_CSharp_HasCorrectTypePatterns()
        {
            // Act
            var config = _provider.GetConfig(Language.CSharp);

            // Assert
            Assert.Equal(ChunkType.Import, config.TypePatterns["using "]);
            Assert.Equal(ChunkType.Module, config.TypePatterns["namespace "]);
            Assert.Equal(ChunkType.Class, config.TypePatterns["class "]);
            Assert.Equal(ChunkType.Interface, config.TypePatterns["interface "]);
            Assert.Equal(ChunkType.Enum, config.TypePatterns["enum "]);
            Assert.Equal(ChunkType.Function, config.TypePatterns["void "]);
        }

        [Fact]
        public void GetConfig_Python_HasCorrectChunkStartKeywords()
        {
            // Act
            var config = _provider.GetConfig(Language.Python);

            // Assert
            Assert.Contains("import ", config.ChunkStartKeywords);
            Assert.Contains("from ", config.ChunkStartKeywords);
            Assert.Contains("class ", config.ChunkStartKeywords);
            Assert.Contains("def ", config.ChunkStartKeywords);
        }

        [Fact]
        public void GetConfig_JavaScript_HasCorrectTypePatterns()
        {
            // Act
            var config = _provider.GetConfig(Language.JavaScript);

            // Assert
            Assert.Equal(ChunkType.Import, config.TypePatterns["import "]);
            Assert.Equal(ChunkType.Import, config.TypePatterns["export "]);
            Assert.Equal(ChunkType.Function, config.TypePatterns["function "]);
            Assert.Equal(ChunkType.Class, config.TypePatterns["class "]);
        }

        [Fact]
        public void GetConfig_TypeScript_IncludesInterfaceSupport()
        {
            // Act
            var config = _provider.GetConfig(Language.TypeScript);

            // Assert
            Assert.Contains("interface ", config.ChunkStartKeywords);
            Assert.Equal(ChunkType.Interface, config.TypePatterns["interface "]);
        }

        [Fact]
        public void GetConfig_Java_IncludesPackageSupport()
        {
            // Act
            var config = _provider.GetConfig(Language.Java);

            // Assert
            Assert.Contains("package ", config.ChunkStartKeywords);
            Assert.Equal(ChunkType.Module, config.TypePatterns["package "]);
        }

        [Fact]
        public void GetConfig_Go_IncludesPackageAndTypeSupport()
        {
            // Act
            var config = _provider.GetConfig(Language.Go);

            // Assert
            Assert.Contains("package ", config.ChunkStartKeywords);
            Assert.Contains("type ", config.ChunkStartKeywords);
            Assert.Equal(ChunkType.Module, config.TypePatterns["package "]);
            Assert.Equal(ChunkType.Class, config.TypePatterns["type "]);
        }

        [Fact]
        public void GetConfig_Rust_IncludesTraitSupport()
        {
            // Act
            var config = _provider.GetConfig(Language.Rust);

            // Assert
            Assert.Contains("trait ", config.ChunkStartKeywords);
            Assert.Equal(ChunkType.Interface, config.TypePatterns["trait "]);
        }
    }
}