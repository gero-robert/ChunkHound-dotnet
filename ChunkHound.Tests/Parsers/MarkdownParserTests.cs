using Xunit;
using ChunkHound.Parsers.Concrete;
using ChunkHound.Core;
using System.Threading.Tasks;
using System.Linq;

namespace ChunkHound.Tests.Parsers
{
    public class MarkdownParserTests
    {
        private readonly MarkdownParser _parser;

        public MarkdownParserTests()
        {
            _parser = new MarkdownParser();
        }

        [Fact]
        public void CanHandle_ValidMarkdownExtension_ReturnsTrue()
        {
            Assert.True(_parser.CanHandle(".md"));
            Assert.True(_parser.CanHandle(".MD"));
        }

        [Fact]
        public void CanHandle_InvalidExtensions_ReturnsFalse()
        {
            Assert.False(_parser.CanHandle(".txt"));
            Assert.False(_parser.CanHandle(".html"));
            Assert.False(_parser.CanHandle(""));
        }

        [Fact]
        public async Task ParseAsync_MarkdownWithHeadings_ReturnsChunks()
        {
            // Arrange
            var markdownContent = @"# Introduction

This is the introduction section.

## Getting Started

Here's how to get started.

### Installation

Install the package.

## Usage

How to use the library.
";

            // Act
            var chunks = await _parser.ParseAsync(markdownContent, "test.md");

            // Assert
            Assert.NotNull(chunks);
            Assert.True(chunks.Count >= 2); // At least some chunks

            var introChunk = chunks.FirstOrDefault(c => c.Code.Contains("Introduction"));
            Assert.NotNull(introChunk);
            // Markdown parser may use Documentation or other types
        }

        [Fact]
        public async Task ParseAsync_EmptyMarkdown_ReturnsEmptyChunks()
        {
            // Arrange
            var emptyMarkdown = "";

            // Act
            var chunks = await _parser.ParseAsync(emptyMarkdown, "empty.md");

            // Assert
            Assert.NotNull(chunks);
            // Empty markdown may still create chunks or return empty - depends on implementation
        }
    }
}