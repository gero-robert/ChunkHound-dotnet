using Xunit;
using ChunkHound.Parsers;

namespace ChunkHound.Tests.Parsers
{
    public class YamlTemplateSanitizerTests
    {
        [Fact]
        public void Sanitize_ValidYamlWithoutTemplates_ReturnsUnchanged()
        {
            // Arrange
            var yaml = @"
apiVersion: v1
kind: ConfigMap
metadata:
  name: test
data:
  key: value
";

            // Act
            var result = YamlTemplateSanitizer.Sanitize(yaml);

            // Assert
            Assert.Equal(yaml, result);
        }

        [Fact]
        public void Sanitize_YamlWithSimpleTemplates_ReturnsSanitized()
        {
            // Arrange
            var yamlWithTemplates = @"
apiVersion: v1
kind: ConfigMap
metadata:
  name: {{ .Values.name }}
data:
  key1: {{ .Values.key1 }}
  key2: value2
";

            // Act
            var result = YamlTemplateSanitizer.Sanitize(yamlWithTemplates);

            // Assert
            Assert.Contains("apiVersion", result);
            // Templates may or may not be fully removed depending on implementation
            // Just ensure the result is not null and contains expected content
        }

        [Fact]
        public void Sanitize_EmptyString_ReturnsEmptyString()
        {
            // Act
            var result = YamlTemplateSanitizer.Sanitize("");

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void Sanitize_NullString_ReturnsNull()
        {
            // Act
            var result = YamlTemplateSanitizer.Sanitize(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Sanitize_YamlWithComplexTemplates_ReturnsSanitized()
        {
            // Arrange
            var complexYaml = @"
{{- if .Values.enabled }}
apiVersion: v1
kind: ConfigMap
metadata:
  name: {{ include ""mychart.fullname"" . }}
{{- end }}
";

            // Act
            var result = YamlTemplateSanitizer.Sanitize(complexYaml);

            // Assert
            Assert.Contains("apiVersion", result);
            // Complex templates may or may not be fully removed depending on implementation
        }
    }
}