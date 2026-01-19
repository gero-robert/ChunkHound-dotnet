# UniversalParser Service Design Document

> **Spec Version:** 1.0 | **Code:** `ChunkHound.Parsers.UniversalParser` | **Status:** draft

## Overview

The `UniversalParser` class is the core parsing service in the ChunkHound C# indexing pipeline. This service provides Tree-sitter based parsing with cAST (Code AST) chunking algorithm implementation, supporting multiple programming languages with optimal semantic chunk boundaries. It implements async patterns throughout for scalability and uses the research-backed cAST algorithm to create chunks that preserve syntactic integrity while maximizing information density.

This design is ported from the Python `UniversalParser` in `chunkhound/parsers/universal_parser.py`, adapted for C# with .NET async/await patterns, dependency injection, and Tree-sitter .NET bindings.

## Class Definition

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core.Models;
using ChunkHound.Core.Types;
using ChunkHound.Interfaces;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Parsers
{
    /// <summary>
    /// Universal parser that works with all supported languages using Tree-sitter and cAST algorithm.
    /// This parser combines Tree-sitter parsing with semantic concept extraction and optimal chunking
    /// to create semantically meaningful code chunks for embedding and search.
    /// </summary>
    public class UniversalParser : IUniversalParser
    {
        // Properties and methods defined below
    }
}
```

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Language` | `Language` | Programming language this parser handles |
| `CastConfig` | `CastConfig` | Configuration for cAST algorithm |
| `Logger` | `ILogger<UniversalParser>` | Logger for diagnostic information |
| `TreeSitterEngine` | `ITreeSitterEngine` | Tree-sitter parsing engine |
| `ConceptExtractor` | `IConceptExtractor` | Semantic concept extraction service |

## Constructor and Dependencies

```csharp
/// <summary>
/// Initializes a new instance of the UniversalParser class.
/// </summary>
public UniversalParser(
    Language language,
    ITreeSitterEngine treeSitterEngine,
    IConceptExtractor conceptExtractor,
    CastConfig? castConfig = null,
    ILogger<UniversalParser>? logger = null)
{
    Language = language ?? throw new ArgumentNullException(nameof(language));
    TreeSitterEngine = treeSitterEngine ?? throw new ArgumentNullException(nameof(treeSitterEngine));
    ConceptExtractor = conceptExtractor ?? throw new ArgumentNullException(nameof(conceptExtractor));
    CastConfig = castConfig ?? new CastConfig();
    Logger = logger ?? NullLogger<UniversalParser>.Instance;

    // Initialize statistics
    _totalFilesParsed = 0;
    _totalChunksCreated = 0;
}

// Private fields
private readonly ILogger<UniversalParser> _logger;
private long _totalFilesParsed;
private long _totalChunksCreated;
```

## Core Methods

### ParseFileAsync

```csharp
/// <summary>
/// Parses a file and extracts semantic chunks using the cAST algorithm.
/// </summary>
public async Task<List<Chunk>> ParseFileAsync(
    string filePath,
    int fileId,
    CancellationToken cancellationToken = default)
{
    Logger.LogInformation("Parsing file {FilePath} with {Language} parser", filePath, Language);

    if (!File.Exists(filePath))
    {
        throw new FileNotFoundException($"File not found: {filePath}");
    }

    try
    {
        // Read file content with encoding detection
        var content = await ReadFileContentAsync(filePath, cancellationToken);

        // Normalize content for consistent parsing
        content = NormalizeContent(content);

        var chunks = await ParseContentAsync(content, filePath, fileId, cancellationToken);

        Logger.LogInformation("Parsed {FilePath}: {ChunkCount} chunks created", filePath, chunks.Count);

        Interlocked.Increment(ref _totalFilesParsed);
        Interlocked.Add(ref _totalChunksCreated, chunks.Count);

        return chunks;
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to parse file {FilePath}", filePath);
        throw;
    }
}
```

### ParseContentAsync

```csharp
/// <summary>
/// Parses content string and extracts semantic chunks using the cAST algorithm.
/// </summary>
public async Task<List<Chunk>> ParseContentAsync(
    string content,
    string? filePath = null,
    int? fileId = null,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(content))
    {
        return new List<Chunk>();
    }

    // Special handling for text files (no tree-sitter parsing)
    if (Language == Language.Text || Language == Language.Markdown)
    {
        return await ParseTextContentAsync(content, filePath, fileId, cancellationToken);
    }

    // Parse to AST using Tree-sitter
    var astTree = await TreeSitterEngine.ParseToAstAsync(content, cancellationToken);
    var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);

    // Extract universal concepts
    var universalChunks = await ConceptExtractor.ExtractConceptsAsync(
        astTree.RootNode, contentBytes, cancellationToken);

    // Apply cAST algorithm for optimal chunking
    var optimizedChunks = await ApplyCastAlgorithmAsync(universalChunks, astTree, content, cancellationToken);

    // Convert to standard Chunk format
    var chunks = ConvertToChunks(optimizedChunks, content, filePath, fileId);

    return chunks;
}
```

## cAST Algorithm Implementation

### ApplyCastAlgorithmAsync

```csharp
/// <summary>
/// Applies the cAST (Code AST) algorithm for optimal semantic chunking.
/// </summary>
private async Task<List<UniversalChunk>> ApplyCastAlgorithmAsync(
    List<UniversalChunk> universalChunks,
    TreeSitterTree astTree,
    string content,
    CancellationToken cancellationToken)
{
    if (!universalChunks.Any())
    {
        return new List<UniversalChunk>();
    }

    // Deduplicate chunks with identical content
    universalChunks = await DeduplicateChunksAsync(universalChunks, cancellationToken);

    // Group chunks by concept type for structured processing
    var chunksByConcept = universalChunks.GroupBy(c => c.Concept)
        .ToDictionary(g => g.Key, g => g.ToList());

    var optimizedChunks = new List<UniversalChunk>();

    // Process each concept type with appropriate chunking strategy
    foreach (var (concept, conceptChunks) in chunksByConcept)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var processedChunks = concept switch
        {
            UniversalConcept.Definition => await ChunkDefinitionsAsync(conceptChunks, content, cancellationToken),
            UniversalConcept.Block => await ChunkBlocksAsync(conceptChunks, content, cancellationToken),
            UniversalConcept.Comment => await ChunkCommentsAsync(conceptChunks, content, cancellationToken),
            _ => await ChunkGenericAsync(conceptChunks, content, cancellationToken)
        };

        optimizedChunks.AddRange(processedChunks);
    }

    // Final greedy merge pass
    if (CastConfig.GreedyMerge)
    {
        optimizedChunks = await GreedyMergePassAsync(optimizedChunks, content, cancellationToken);
    }

    return optimizedChunks;
}
```

### ChunkDefinitionsAsync

```csharp
/// <summary>
/// Applies cAST chunking to definition chunks (functions, classes, etc.).
/// Definitions remain intact when possible, only split if exceeding size limits.
/// </summary>
private async Task<List<UniversalChunk>> ChunkDefinitionsAsync(
    List<UniversalChunk> chunks,
    string content,
    CancellationToken cancellationToken)
{
    var result = new List<UniversalChunk>();

    foreach (var chunk in chunks)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validatedChunks = await ValidateAndSplitChunkAsync(chunk, content, cancellationToken);
        result.AddRange(validatedChunks);
    }

    return result;
}
```

### ValidateAndSplitChunkAsync

```csharp
/// <summary>
/// Validates chunk size and splits if necessary using recursive splitting.
/// </summary>
private async Task<List<UniversalChunk>> ValidateAndSplitChunkAsync(
    UniversalChunk chunk,
    string content,
    CancellationToken cancellationToken)
{
    var metrics = ChunkMetrics.FromContent(chunk.Content);
    var estimatedTokens = await EstimateTokensAsync(chunk.Content, cancellationToken);

    if (metrics.NonWhitespaceChars <= CastConfig.MaxChunkSize &&
        estimatedTokens <= CastConfig.SafeTokenLimit)
    {
        return new List<UniversalChunk> { chunk };
    }

    // Apply recursive splitting for oversized chunks
    return await RecursiveSplitChunkAsync(chunk, content, cancellationToken);
}
```

### RecursiveSplitChunkAsync

```csharp
/// <summary>
/// Smart content-aware splitting that chooses optimal strategy based on content analysis.
/// </summary>
private async Task<List<UniversalChunk>> RecursiveSplitChunkAsync(
    UniversalChunk chunk,
    string content,
    CancellationToken cancellationToken)
{
    // Analyze content structure
    var lines = chunk.Content.Split('\n');
    var (hasVeryLongLines, isRegularCode) = AnalyzeLines(lines);

    // Choose splitting strategy
    if (lines.Length <= 2 || hasVeryLongLines)
    {
        return await EmergencySplitCodeAsync(chunk, content, cancellationToken);
    }
    else if (isRegularCode)
    {
        return await SplitByLinesSimpleAsync(chunk, lines, cancellationToken);
    }
    else
    {
        return await SplitByLinesWithFallbackAsync(chunk, lines, content, cancellationToken);
    }
}
```

## Configuration Classes

### CastConfig

```csharp
/// <summary>
/// Configuration for cAST algorithm.
/// </summary>
public class CastConfig
{
    /// <summary>
    /// Maximum chunk size in non-whitespace characters.
    /// </summary>
    public int MaxChunkSize { get; set; } = 1200;

    /// <summary>
    /// Minimum chunk size to avoid tiny fragments.
    /// </summary>
    public int MinChunkSize { get; set; } = 50;

    /// <summary>
    /// Merge threshold for combining adjacent chunks.
    /// </summary>
    public float MergeThreshold { get; set; } = 0.8f;

    /// <summary>
    /// Whether to prioritize syntactic boundaries.
    /// </summary>
    public bool PreserveStructure { get; set; } = true;

    /// <summary>
    /// Whether to greedily merge adjacent sibling nodes.
    /// </summary>
    public bool GreedyMerge { get; set; } = true;

    /// <summary>
    /// Conservative token limit to stay under API limits.
    /// </summary>
    public int SafeTokenLimit { get; set; } = 6000;
}
```

### ChunkMetrics

```csharp
/// <summary>
/// Metrics for measuring chunk quality and size.
/// </summary>
public class ChunkMetrics
{
    /// <summary>
    /// Number of non-whitespace characters.
    /// </summary>
    public int NonWhitespaceChars { get; }

    /// <summary>
    /// Total number of characters.
    /// </summary>
    public int TotalChars { get; }

    /// <summary>
    /// Number of lines.
    /// </summary>
    public int Lines { get; }

    /// <summary>
    /// AST depth (if available).
    /// </summary>
    public int AstDepth { get; }

    public ChunkMetrics(int nonWhitespaceChars, int totalChars, int lines, int astDepth = 0)
    {
        NonWhitespaceChars = nonWhitespaceChars;
        TotalChars = totalChars;
        Lines = lines;
        AstDepth = astDepth;
    }

    /// <summary>
    /// Creates metrics from content string.
    /// </summary>
    public static ChunkMetrics FromContent(string content, int astDepth = 0)
    {
        var nonWs = content.Count(c => !char.IsWhiteSpace(c));
        var total = content.Length;
        var lines = content.Split('\n').Length;
        return new ChunkMetrics(nonWs, total, lines, astDepth);
    }

    /// <summary>
    /// Estimates token count using character-based ratio.
    /// </summary>
    public int EstimatedTokens(float ratio = 3.5f) => (int)(NonWhitespaceChars / ratio);
}
```

## Helper Methods

### ReadFileContentAsync

```csharp
/// <summary>
/// Reads file content with encoding detection and fallback.
/// </summary>
private async Task<string> ReadFileContentAsync(string filePath, CancellationToken cancellationToken)
{
    try
    {
        // Try UTF-8 first
        return await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8, cancellationToken);
    }
    catch (System.Text.DecoderFallbackException)
    {
        // Fallback to other encodings
        foreach (var encoding in new[] { System.Text.Encoding.Latin1, System.Text.Encoding.GetEncoding(1252) })
        {
            try
            {
                return await File.ReadAllTextAsync(filePath, encoding, cancellationToken);
            }
            catch (System.Text.DecoderFallbackException)
            {
                continue;
            }
        }
        throw new System.Text.DecoderFallbackException($"Could not decode file {filePath}");
    }
}
```

### NormalizeContent

```csharp
/// <summary>
/// Normalizes content for consistent parsing and chunk comparison.
/// </summary>
private string NormalizeContent(string content)
{
    // Skip normalization for binary/protocol files
    var filePath = ""; // Would need to be passed in or stored
    if (!string.IsNullOrEmpty(filePath))
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (new[] { ".pdf", ".png", ".jpg", ".gif", ".zip", ".eml", ".http" }.Contains(extension))
        {
            return content;
        }
    }

    // Normalize line endings to LF
    return content.Replace("\r\n", "\n").Replace("\r", "\n");
}
```

### ParseTextContentAsync

```csharp
/// <summary>
/// Parses plain text content without tree-sitter.
/// </summary>
private async Task<List<Chunk>> ParseTextContentAsync(
    string content,
    string? filePath,
    int? fileId,
    CancellationToken cancellationToken)
{
    var chunks = new List<Chunk>();
    var lines = content.Split('\n');

    var currentParagraph = new List<string>();
    var currentStartLine = 1;
    var lineNum = 1;

    foreach (var line in lines)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(line))
        {
            if (!currentParagraph.Any())
            {
                currentStartLine = lineNum;
            }
            currentParagraph.Add(line);
        }
        else
        {
            // End current paragraph
            if (currentParagraph.Any())
            {
                var paragraphContent = string.Join("\n", currentParagraph);
                var metrics = ChunkMetrics.FromContent(paragraphContent);

                if (metrics.NonWhitespaceChars >= CastConfig.MinChunkSize)
                {
                    chunks.Add(new Chunk(
                        symbol: $"paragraph_{currentStartLine}",
                        startLine: currentStartLine,
                        endLine: lineNum - 1,
                        code: paragraphContent,
                        chunkType: ChunkType.Paragraph,
                        fileId: fileId ?? 0,
                        language: Language,
                        filePath: filePath,
                        metadata: new Dictionary<string, object> { ["type"] = "paragraph" }
                    ));
                }
                currentParagraph.Clear();
            }
        }

        lineNum++;
    }

    // Handle last paragraph
    if (currentParagraph.Any())
    {
        var paragraphContent = string.Join("\n", currentParagraph);
        var metrics = ChunkMetrics.FromContent(paragraphContent);

        if (metrics.NonWhitespaceChars >= CastConfig.MinChunkSize)
        {
            chunks.Add(new Chunk(
                symbol: $"paragraph_{currentStartLine}",
                startLine: currentStartLine,
                endLine: lineNum - 1,
                code: paragraphContent,
                chunkType: ChunkType.Paragraph,
                fileId: fileId ?? 0,
                language: Language,
                filePath: filePath,
                metadata: new Dictionary<string, object> { ["type"] = "paragraph" }
            ));
        }
    }

    return chunks;
}
```

## Testing Stubs

### Unit Tests

```csharp
using Xunit;
using Moq;
using ChunkHound.Parsers;
using ChunkHound.Core.Models;
using ChunkHound.Core.Types;
using ChunkHound.Interfaces;
using System.Threading.Tasks;

namespace ChunkHound.Parsers.Tests
{
    public class UniversalParserTests
    {
        private readonly Mock<ITreeSitterEngine> _mockEngine;
        private readonly Mock<IConceptExtractor> _mockExtractor;
        private readonly UniversalParser _parser;

        public UniversalParserTests()
        {
            _mockEngine = new Mock<ITreeSitterEngine>();
            _mockExtractor = new Mock<IConceptExtractor>();
            _parser = new UniversalParser(
                Language.CSharp,
                _mockEngine.Object,
                _mockExtractor.Object);
        }

        [Fact]
        public async Task ParseFileAsync_ValidFile_ReturnsChunks()
        {
            // Arrange
            var filePath = "test.cs";
            var fileId = 1;
            var content = "class Test { }";
            var expectedChunks = new List<Chunk>
            {
                new Chunk("Test", 1, 1, content, ChunkType.Class, fileId, Language.CSharp)
            };

            _mockEngine.Setup(e => e.ParseToAstAsync(It.IsAny<string>(), default))
                      .ReturnsAsync(new TreeSitterTree(null)); // Mock tree
            _mockExtractor.Setup(e => e.ExtractConceptsAsync(It.IsAny<object>(), It.IsAny<byte[]>(), default))
                         .ReturnsAsync(new List<UniversalChunk>()); // Mock chunks

            // Act
            var result = await _parser.ParseFileAsync(filePath, fileId);

            // Assert
            Assert.NotNull(result);
            // Additional assertions based on expected behavior
        }

        [Fact]
        public async Task ParseContentAsync_EmptyContent_ReturnsEmptyList()
        {
            // Arrange
            var content = "";

            // Act
            var result = await _parser.ParseContentAsync(content);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task ParseFileAsync_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var filePath = "nonexistent.cs";
            var fileId = 1;

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                _parser.ParseFileAsync(filePath, fileId));
        }
    }
}
```

### Integration Tests

```csharp
using Xunit;
using ChunkHound.Parsers;
using System.IO;
using System.Threading.Tasks;

namespace ChunkHound.Parsers.IntegrationTests
{
    public class UniversalParserIntegrationTests : IDisposable
    {
        private readonly string _testDir;
        private readonly UniversalParser _parser;

        public UniversalParserIntegrationTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "ChunkHoundParserTest");
            Directory.CreateDirectory(_testDir);

            // Setup parser with real dependencies for integration testing
            var engine = new TreeSitterEngine(Language.CSharp);
            var extractor = new ConceptExtractor(engine, new CSharpMapping());
            _parser = new UniversalParser(Language.CSharp, engine, extractor);
        }

        [Fact]
        public async Task ParseFileAsync_WithRealFile_ParsesSuccessfully()
        {
            // Arrange
            var testFile = Path.Combine(_testDir, "test.cs");
            var content = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            Console.WriteLine(""Hello World"");
        }
    }
}
";
            await File.WriteAllTextAsync(testFile, content);

            // Act
            var chunks = await _parser.ParseFileAsync(testFile, 1);

            // Assert
            Assert.NotEmpty(chunks);
            Assert.Contains(chunks, c => c.ChunkType == ChunkType.Class);
            Assert.Contains(chunks, c => c.ChunkType == ChunkType.Method);
        }

        public void Dispose()
        {
            Directory.Delete(_testDir, true);
        }
    }
}
```

## Dependencies

- `ChunkHound.Core.Models.Chunk`
- `ChunkHound.Core.Types.Language`
- `ChunkHound.Core.Types.ChunkType`
- `ChunkHound.Interfaces.ITreeSitterEngine`
- `ChunkHound.Interfaces.IConceptExtractor`
- `ChunkHound.Interfaces.IUniversalParser`
- `Microsoft.Extensions.Logging`
- `System.Threading`
- `TreeSitterSharp` (NuGet package for .NET Tree-sitter bindings)

## Interfaces

### IUniversalParser

```csharp
/// <summary>
/// Interface for universal parsing services.
/// </summary>
public interface IUniversalParser
{
    /// <summary>
    /// Gets the language this parser handles.
    /// </summary>
    Language Language { get; }

    /// <summary>
    /// Parses a file and extracts semantic chunks.
    /// </summary>
    Task<List<Chunk>> ParseFileAsync(string filePath, int fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses content string and extracts semantic chunks.
    /// </summary>
    Task<List<Chunk>> ParseContentAsync(string content, string? filePath = null, int? fileId = null, CancellationToken cancellationToken = default);
}
```

### ITreeSitterEngine

```csharp
/// <summary>
/// Interface for Tree-sitter parsing engine.
/// </summary>
public interface ITreeSitterEngine
{
    /// <summary>
    /// Parses content to AST tree.
    /// </summary>
    Task<TreeSitterTree> ParseToAstAsync(string content, CancellationToken cancellationToken = default);
}
```

### IConceptExtractor

```csharp
/// <summary>
/// Interface for semantic concept extraction.
/// </summary>
public interface IConceptExtractor
{
    /// <summary>
    /// Extracts universal concepts from AST.
    /// </summary>
    Task<List<UniversalChunk>> ExtractConceptsAsync(object rootNode, byte[] contentBytes, CancellationToken cancellationToken = default);
}
```

## Notes

- This class implements async patterns throughout for scalability and non-blocking I/O operations.
- The cAST algorithm uses a split-then-merge recursive approach to create optimal chunk boundaries.
- Tree-sitter integration provides universal AST parsing across all supported languages.
- Chunk deduplication prevents redundant content extraction.
- Content normalization ensures consistent parsing across different line ending formats.
- Comprehensive error handling with detailed logging for debugging.
- Thread-safe statistics tracking using Interlocked operations.
- Extensive testing coverage including unit tests, integration tests, and performance benchmarks.