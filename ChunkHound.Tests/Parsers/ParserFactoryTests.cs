using Xunit;
using ChunkHound.Parsers;
using ChunkHound.Parsers.Concrete;
using System;
using System.Collections.Generic;

namespace ChunkHound.Tests.Parsers
{
    public class ParserFactoryTests
    {
        private readonly ParserFactory _factory;

        public ParserFactoryTests()
        {
            var parsers = new List<IChunkParser>
            {
                new RapidYamlParser(),
                new VueChunkParser(),
                new CodeChunkParser(),
                new MarkdownParser(),
                new UniversalTextParser()
            };
            _factory = new ParserFactory(parsers);
        }

        [Theory]
        [InlineData(".yaml", typeof(RapidYamlParser))]
        [InlineData(".yml", typeof(RapidYamlParser))]
        [InlineData(".vue", typeof(VueChunkParser))]
        [InlineData(".cs", typeof(CodeChunkParser))]
        [InlineData(".py", typeof(CodeChunkParser))]
        [InlineData(".js", typeof(CodeChunkParser))]
        [InlineData(".md", typeof(MarkdownParser))]
        [InlineData(".txt", typeof(UniversalTextParser))]
        public void GetParser_ValidExtensions_ReturnsCorrectParser(string extension, Type expectedType)
        {
            // Act
            var parser = _factory.GetParser(extension);

            // Assert
            Assert.IsType(expectedType, parser);
        }

        [Theory]
        [InlineData(".YAML")]
        [InlineData(".VUE")]
        [InlineData(".CS")]
        public void GetParser_CaseInsensitiveExtensions_ReturnsCorrectParser(string extension)
        {
            // Act
            var parser = _factory.GetParser(extension);

            // Assert
            Assert.NotNull(parser);
        }

        [Fact]
        public void GetParser_UnsupportedExtension_ReturnsUniversalTextParser()
        {
            // Act
            var parser = _factory.GetParser(".unsupported");

            // Assert - UniversalTextParser is the fallback
            Assert.IsType<UniversalTextParser>(parser);
        }

        [Fact]
        public void GetParser_EmptyExtension_ThrowsNotSupportedException()
        {
            // Act & Assert
            Assert.Throws<NotSupportedException>(() => _factory.GetParser(""));
        }
    }
}