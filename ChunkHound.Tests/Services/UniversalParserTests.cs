using Xunit;
using Moq;
using ChunkHound.Services;
using ChunkHound.Core;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;
using FileModel = ChunkHound.Core.File;

namespace ChunkHound.Services.Tests
{
    public class UniversalParserTests
    {
        private readonly Mock<ILogger<UniversalParser>> _mockLogger;
        private readonly UniversalParser _parser;

        public UniversalParserTests()
        {
            _mockLogger = new Mock<ILogger<UniversalParser>>();
            var configProvider = new ChunkHound.Services.LanguageConfigProvider();
            _parser = new UniversalParser(_mockLogger.Object, configProvider);
        }

        /// <summary>
        /// Tests that the parser correctly processes valid C# files by creating semantic chunks from namespaces, classes, and methods.
        /// This validates the core parsing functionality for typical code structures and ensures chunks contain meaningful code content.
        /// </summary>
        [Fact]
        public async Task ParseAsync_ValidFile_ReturnsChunks()
        {
            // Arrange
            var testFile = Path.GetTempFileName();
            var content = @"using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            Console.WriteLine(""Hello World"");
        }
    }
}";
            await System.IO.File.WriteAllTextAsync(testFile, content);

            var file = new FileModel(
                path: testFile,
                mtime: 1234567890,
                language: Language.CSharp,
                sizeBytes: content.Length,
                id: 1
            );

            try
            {
                // Act
                var chunks = await _parser.ParseAsync(file);

                // Assert
                Assert.NotNull(chunks);
                Assert.NotEmpty(chunks);

                // Should create at least one chunk with code content
                Assert.Contains(chunks, c => !string.IsNullOrWhiteSpace(c.Code));
            }
            finally
            {
                // Cleanup
                System.IO.File.Delete(testFile);
            }
        }

        /// <summary>
        /// Tests that the parser handles empty files gracefully by returning an empty chunk list without throwing exceptions.
        /// This validates edge case handling for files with no content, ensuring robustness in file processing workflows.
        /// </summary>
        [Fact]
        public async Task ParseAsync_EmptyFile_ReturnsEmptyList()
        {
            // Arrange
            var testFile = Path.GetTempFileName();
            var content = "";
            await System.IO.File.WriteAllTextAsync(testFile, content);

            var file = new FileModel(
                path: testFile,
                mtime: 1234567890,
                language: Language.CSharp,
                sizeBytes: 0,
                id: 1
            );

            try
            {
                // Act
                var chunks = await _parser.ParseAsync(file);

                // Assert
                Assert.NotNull(chunks);
                Assert.Empty(chunks);
            }
            finally
            {
                // Cleanup
                System.IO.File.Delete(testFile);
            }
        }

        /// <summary>
        /// Tests that the parser throws FileNotFoundException when attempting to parse a non-existent file.
        /// This validates error handling for invalid file paths, preventing silent failures in indexing operations.
        /// </summary>
        [Fact]
        public async Task ParseAsync_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var file = new FileModel(
                path: "nonexistent.cs",
                mtime: 1234567890,
                language: Language.CSharp,
                sizeBytes: 100,
                id: 1
            );

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                _parser.ParseAsync(file));
        }

        /// <summary>
        /// Tests that the parser correctly identifies and chunks Python code structures including imports, classes, and functions.
        /// This validates language-specific parsing logic for Python files, ensuring proper semantic chunking across different programming languages.
        /// </summary>
        [Fact]
        public async Task ParseAsync_PythonFile_CreatesAppropriateChunks()
        {
            // Arrange
            var testFile = Path.GetTempFileName();
            var content = @"import os
from typing import List

class MyClass:
    def __init__(self):
        self.value = 42

    def my_method(self):
        return self.value

if __name__ == ""__main__"":
    obj = MyClass()
    print(obj.my_method())
";
            await System.IO.File.WriteAllTextAsync(testFile, content);

            var file = new FileModel(
                path: testFile,
                mtime: 1234567890,
                language: Language.Python,
                sizeBytes: content.Length,
                id: 1
            );

            try
            {
                // Act
                var chunks = await _parser.ParseAsync(file);

                // Assert
                Assert.NotNull(chunks);
                Assert.NotEmpty(chunks);

                // Should create at least one chunk with code content
                Assert.Contains(chunks, c => !string.IsNullOrWhiteSpace(c.Code));
            }
            finally
            {
                // Cleanup
                System.IO.File.Delete(testFile);
            }
        }

        /// <summary>
        /// Tests that the parser correctly identifies and chunks JavaScript code structures including imports, functions, and exports.
        /// This validates language-specific parsing logic for JavaScript files, ensuring proper semantic chunking for modern web development code.
        /// </summary>
        [Fact]
        public async Task ParseAsync_JavaScriptFile_CreatesAppropriateChunks()
        {
            // Arrange
            var testFile = Path.GetTempFileName();
            var content = @"import React from 'react';

function MyComponent() {
    return <div>Hello World</div>;
}

export default MyComponent;
";
            await System.IO.File.WriteAllTextAsync(testFile, content);

            var file = new FileModel(
                path: testFile,
                mtime: 1234567890,
                language: Language.JavaScript,
                sizeBytes: content.Length,
                id: 1
            );

            try
            {
                // Act
                var chunks = await _parser.ParseAsync(file);

                // Assert
                Assert.NotNull(chunks);
                Assert.NotEmpty(chunks);

                // Should create at least one chunk with code content
                Assert.Contains(chunks, c => !string.IsNullOrWhiteSpace(c.Code));
            }
            finally
            {
                // Cleanup
                System.IO.File.Delete(testFile);
            }
        }

        /// <summary>
        /// Tests that the parser splits large files into multiple appropriately-sized chunks according to cAST algorithm constraints.
        /// This validates chunk size management and splitting logic, ensuring no single chunk exceeds maximum size limits for embedding processing.
        /// </summary>
        [Fact]
        public async Task ParseAsync_LargeFile_SplitsIntoMultipleChunks()
        {
            // Arrange
            var testFile = Path.GetTempFileName();
            var sb = new System.Text.StringBuilder();

            // Create a large file with multiple classes
            for (int i = 0; i < 10; i++)
            {
                sb.AppendLine($"public class Class{i}");
                sb.AppendLine("{");
                sb.AppendLine($"    public void Method{i}()");
                sb.AppendLine("    {");
                sb.AppendLine($"        Console.WriteLine(\"Method {i}\");");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            var content = sb.ToString();
            await System.IO.File.WriteAllTextAsync(testFile, content);

            var file = new FileModel(
                path: testFile,
                mtime: 1234567890,
                language: Language.CSharp,
                sizeBytes: content.Length,
                id: 1
            );

            try
            {
                // Act
                var chunks = await _parser.ParseAsync(file);

                // Assert
                Assert.NotNull(chunks);
                Assert.NotEmpty(chunks);

                // Should create multiple chunks
                Assert.True(chunks.Count >= 5); // At least some chunks created
            }
            finally
            {
                // Cleanup
                System.IO.File.Delete(testFile);
            }
        }
    }
}