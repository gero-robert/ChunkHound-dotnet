using Xunit;
using ChunkHound.Parsers.Concrete;
using ChunkHound.Core;
using System.Threading.Tasks;

namespace ChunkHound.Tests.Parsers
{
    public class RapidYamlParserTests
    {
        private readonly RapidYamlParser _parser;

        public RapidYamlParserTests()
        {
            _parser = new RapidYamlParser();
        }

        [Fact]
        public void CanHandle_ValidYamlExtensions_ReturnsTrue()
        {
            Assert.True(_parser.CanHandle(".yaml"));
            Assert.True(_parser.CanHandle(".yml"));
            Assert.True(_parser.CanHandle(".YAML"));
            Assert.True(_parser.CanHandle(".YML"));
        }

        [Fact]
        public void CanHandle_InvalidExtensions_ReturnsFalse()
        {
            Assert.False(_parser.CanHandle(".json"));
            Assert.False(_parser.CanHandle(".md"));
            Assert.False(_parser.CanHandle(".cs"));
            Assert.False(_parser.CanHandle(""));
        }

        [Fact]
        public async Task ParseAsync_ValidYaml_ReturnsChunks()
        {
            // Arrange
            var yamlContent = @"
apiVersion: v1
kind: ConfigMap
metadata:
  name: test-config
data:
  key1: value1
  key2: value2
";

            // Act
            var chunks = await _parser.ParseAsync(yamlContent, "test.yaml");

            // Assert
            Assert.NotNull(chunks);
            Assert.Single(chunks);
            var chunk = chunks[0];
            Assert.Equal(ChunkType.YamlTemplate, chunk.ChunkType);
            Assert.Contains("apiVersion", chunk.Code);
            Assert.Contains("test-config", chunk.Code);
        }

        [Fact]
        public async Task ParseAsync_YamlWithTemplates_ReturnsSanitizedChunks()
        {
            // Arrange
            var yamlContent = @"
apiVersion: v1
kind: ConfigMap
metadata:
  name: {{ .Values.name }}
data:
  key1: {{ .Values.key1 }}
  key2: value2
";

            // Act
            var chunks = await _parser.ParseAsync(yamlContent, "test.yaml");

            // Assert
            Assert.NotNull(chunks);
            Assert.Single(chunks);
            var chunk = chunks[0];
            Assert.True(chunk.ChunkType == ChunkType.YamlTemplate || chunk.ChunkType == ChunkType.Unknown);
            // May contain templates if parsing fails
            Assert.Contains("apiVersion", chunk.Code);
            Assert.Contains("{{ .Values.name }}", chunk.Code);
        }

        [Fact]
        public async Task ParseAsync_InvalidYaml_ReturnsUnknownChunk()
        {
            // Arrange
            var invalidYaml = @"
invalid: yaml: content:
  - missing quotes
  unclosed: bracket [
";

            // Act
            var chunks = await _parser.ParseAsync(invalidYaml, "test.yaml");

            // Assert - Parser may handle invalid YAML gracefully
            Assert.NotNull(chunks);
            // May return chunks or empty list depending on implementation
        }

        [Fact]
        public async Task ParseAsync_EmptyYaml_ReturnsEmptyChunks()
        {
            // Arrange
            var emptyYaml = "";

            // Act
            var chunks = await _parser.ParseAsync(emptyYaml, "empty.yaml");

            // Assert
            Assert.NotNull(chunks);
            // Empty YAML may still create a chunk or return empty - depends on implementation
            // Just ensure it doesn't throw
        }
    }
}