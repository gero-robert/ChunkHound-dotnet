# Chunk Model Design Document

> **Spec Version:** 1.0 | **Code:** `ChunkHound.Core.Models.Chunk` | **Status:** draft

## Overview

The `Chunk` class represents a semantic unit of code extracted from a source file in the ChunkHound system. This immutable model encapsulates all information about a code chunk, including its location, content, metadata, and provides methods for working with chunk data in a type-safe manner.

The `EmbedChunk` record represents a chunk paired with its vector embedding for storage and retrieval. It combines a processed code chunk with its semantic vector representation, along with metadata about the embedding provider and model used.

This design is ported from the Python `Chunk` dataclass in `chunkhound/core/models/chunk.py`, adapted for C# with .NET conventions.

## Class Definition

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ChunkHound.Core.Types;

namespace ChunkHound.Core.Models
{
    /// <summary>
    /// Domain model representing a semantic code chunk.
    /// This immutable model encapsulates all information about a semantic unit of code
    /// that has been extracted from a source file, including its location, content,
    /// and metadata.
    /// </summary>
    public record Chunk
    {
        // Properties defined below
    }

    /// <summary>
    /// Represents a chunk paired with its vector embedding for storage.
    /// </summary>
    public record EmbedChunk
    {
        /// <summary>
        /// The processed code chunk.
        /// </summary>
        public Chunk Chunk { get; init; }

        /// <summary>
        /// The vector embedding for semantic search.
        /// </summary>
        public List<float> Embedding { get; init; }

        /// <summary>
        /// Provider name that generated the embedding.
        /// </summary>
        public string Provider { get; init; }

        /// <summary>
        /// Model name/version used for embedding generation.
        /// </summary>
        public string Model { get; init; }

        /// <summary>
        /// Initializes a new EmbedChunk.
        /// </summary>
        public EmbedChunk(Chunk chunk, List<float> embedding, string provider, string model)
        {
            Chunk = chunk ?? throw new ArgumentNullException(nameof(chunk));
            Embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }
    }
}
```

## Properties

| Property | Type | Description | Required | Default |
|----------|------|-------------|----------|---------|
| `Symbol` | `string?` | Function, class, or element name | No | `null` |
| `StartLine` | `int` | Starting line number (1-based) | Yes | - |
| `EndLine` | `int` | Ending line number (1-based, inclusive) | Yes | - |
| `Code` | `string` | Raw code content | Yes | - |
| `ChunkType` | `ChunkType` | Type of semantic chunk | Yes | - |
| `FileId` | `int` | Reference to the parent file | Yes | - |
| `Language` | `Language` | Programming language of the chunk | Yes | - |
| `Id` | `int?` | Unique chunk identifier | No | `null` |
| `FilePath` | `string?` | Path to the source file | No | `null` |
| `ParentHeader` | `string?` | Parent header for nested content (markdown) | No | `null` |
| `StartByte` | `long?` | Starting byte offset | No | `null` |
| `EndByte` | `long?` | Ending byte offset | No | `null` |
| `CreatedAt` | `DateTime?` | When the chunk was first indexed | No | `null` |
| `UpdatedAt` | `DateTime?` | When the chunk was last updated | No | `null` |
| `Metadata` | `Dictionary<string, object>?` | Language-specific metadata | No | `null` |

## Constructor and Validation

```csharp
/// <summary>
/// Initializes a new instance of the Chunk class.
/// </summary>
public Chunk(
    string? symbol,
    int startLine,
    int endLine,
    string code,
    ChunkType chunkType,
    int fileId,
    Language language,
    int? id = null,
    string? filePath = null,
    string? parentHeader = null,
    long? startByte = null,
    long? endByte = null,
    DateTime? createdAt = null,
    DateTime? updatedAt = null,
    Dictionary<string, object>? metadata = null)
{
    Symbol = symbol;
    StartLine = startLine;
    EndLine = endLine;
    Code = code ?? throw new ArgumentNullException(nameof(code));
    ChunkType = chunkType;
    FileId = fileId;
    Language = language;
    Id = id;
    FilePath = filePath;
    ParentHeader = parentHeader;
    StartByte = startByte;
    EndByte = endByte;
    CreatedAt = createdAt;
    UpdatedAt = updatedAt;
    Metadata = metadata;

    Validate();
}

/// <summary>
/// Validates chunk model attributes.
/// </summary>
private void Validate()
{
    // Symbol validation - allow null or empty for structural chunks
    if (Symbol != null && string.IsNullOrWhiteSpace(Symbol))
    {
        Symbol = null;
    }

    // Line number validation
    if (StartLine < 1)
        throw new ValidationException("StartLine", StartLine, "Start line must be positive");

    if (EndLine < 1)
        throw new ValidationException("EndLine", EndLine, "End line must be positive");

    if (StartLine > EndLine)
        throw new ValidationException("LineRange", $"{StartLine}-{EndLine}", "Start line cannot be greater than end line");

    // Code validation
    if (string.IsNullOrEmpty(Code))
        throw new ValidationException("Code", Code, "Code content cannot be empty");

    // Byte offset validation (if provided)
    if (StartByte.HasValue && StartByte < 0)
        throw new ValidationException("StartByte", StartByte, "Start byte cannot be negative");

    if (EndByte.HasValue && EndByte < 0)
        throw new ValidationException("EndByte", EndByte, "End byte cannot be negative");

    if (StartByte.HasValue && EndByte.HasValue && StartByte > EndByte)
        throw new ValidationException("ByteRange", $"{StartByte}-{EndByte}", "Start byte cannot be greater than end byte");
}
```

## Computed Properties

```csharp
/// <summary>
/// Gets the number of lines in this chunk.
/// </summary>
public int LineCount => EndLine - StartLine + 1;

/// <summary>
/// Gets the number of characters in the code content.
/// </summary>
public int CharCount => Code.Length;

/// <summary>
/// Gets the number of bytes in this chunk (if byte offsets are available).
/// </summary>
public long? ByteCount => StartByte.HasValue && EndByte.HasValue ? EndByte - StartByte + 1 : null;

/// <summary>
/// Gets a human-readable display name for this chunk.
/// </summary>
public string DisplayName => ChunkType.IsCode
    ? $"{ChunkType.Value}: {Symbol}"
    : $"{ChunkType.Value}: {Code[..Math.Min(50, Code.Length)].Replace("\n", " ").Trim()}{(Code.Length > 50 ? "..." : "")}";

/// <summary>
/// Gets relative file path (if available).
/// </summary>
public string? RelativePath
{
    get
    {
        if (FilePath == null) return null;
        try
        {
            return Path.GetRelativePath(Directory.GetCurrentDirectory(), FilePath);
        }
        catch
        {
            return FilePath;
        }
    }
}
```

## Methods

### Utility Methods

```csharp
/// <summary>
/// Checks if this chunk represents code structure.
/// </summary>
public bool IsCodeChunk() => ChunkType.IsCode;

/// <summary>
/// Checks if this chunk represents documentation.
/// </summary>
public bool IsDocumentationChunk() => ChunkType.IsDocumentation;

/// <summary>
/// Checks if this chunk is considered small.
/// </summary>
public bool IsSmallChunk(int minLines = 3) => LineCount < minLines;

/// <summary>
/// Checks if this chunk is considered large.
/// </summary>
public bool IsLargeChunk(int maxLines = 500) => LineCount > maxLines;

/// <summary>
/// Checks if the given line number is within this chunk.
/// </summary>
public bool ContainsLine(int lineNumber) => StartLine <= lineNumber && lineNumber <= EndLine;

/// <summary>
/// Checks if this chunk overlaps with another chunk.
/// </summary>
public bool OverlapsWith(Chunk other) => !(EndLine < other.StartLine || other.EndLine < StartLine);
```

### Factory Methods

```csharp
/// <summary>
/// Creates a new Chunk instance with the specified ID.
/// </summary>
public Chunk WithId(int chunkId) => this with { Id = chunkId };

/// <summary>
/// Creates a new Chunk instance with the specified file path.
/// </summary>
public Chunk WithFilePath(string filePath) => this with { FilePath = filePath };
```

## Serialization

### JSON Serialization

The class uses `System.Text.Json` for serialization with custom converters for enums and nullable types.

```csharp
/// <summary>
/// Creates a Chunk model from a dictionary.
/// </summary>
public static Chunk FromDict(Dictionary<string, object> data)
{
    // Implementation similar to Python from_dict
    var symbol = data.GetValueOrDefault("symbol") as string;
    var startLine = Convert.ToInt32(data.GetValueOrDefault("start_line") ?? throw new ValidationException("start_line", null, "Start line is required"));
    var endLine = Convert.ToInt32(data.GetValueOrDefault("end_line") ?? throw new ValidationException("end_line", null, "End line is required"));
    var code = data.GetValueOrDefault("code") as string ?? throw new ValidationException("code", null, "Code content is required");
    var fileId = Convert.ToInt32(data.GetValueOrDefault("file_id") ?? throw new ValidationException("file_id", null, "File ID is required"));

    var chunkTypeValue = data.GetValueOrDefault("chunk_type") ?? data.GetValueOrDefault("type");
    var chunkType = chunkTypeValue is ChunkType ct ? ct :
                   chunkTypeValue is string s ? ChunkTypeExtensions.FromString(s) :
                   ChunkType.Unknown;

    var languageValue = data.GetValueOrDefault("language") ?? data.GetValueOrDefault("language_info");
    var language = languageValue is Language l ? l :
                  languageValue is string ls ? LanguageExtensions.FromString(ls) :
                  Language.Unknown;

    var id = data.GetValueOrDefault("id") is int i ? (int?)i : null;
    var filePath = data.GetValueOrDefault("file_path") ?? data.GetValueOrDefault("path") as string;
    var parentHeader = data.GetValueOrDefault("parent_header") as string;
    var startByte = data.GetValueOrDefault("start_byte") is long sb ? (long?)sb : null;
    var endByte = data.GetValueOrDefault("end_byte") is long eb ? (long?)eb : null;

    DateTime? createdAt = null;
    if (data.GetValueOrDefault("created_at") is string cas)
        createdAt = DateTime.Parse(cas);

    DateTime? updatedAt = null;
    if (data.GetValueOrDefault("updated_at") is string uas)
        updatedAt = DateTime.Parse(uas);

    var metadata = data.GetValueOrDefault("metadata") as Dictionary<string, object>;

    return new Chunk(
        symbol, startLine, endLine, code, chunkType, fileId, language,
        id, filePath, parentHeader, startByte, endByte, createdAt, updatedAt, metadata);
}

/// <summary>
/// Converts Chunk model to dictionary.
/// </summary>
public Dictionary<string, object> ToDict()
{
    var result = new Dictionary<string, object>
    {
        ["symbol"] = Symbol,
        ["start_line"] = StartLine,
        ["end_line"] = EndLine,
        ["code"] = Code,
        ["chunk_type"] = ChunkType.Value,
        ["file_id"] = FileId,
        ["language"] = Language.Value
    };

    if (Id.HasValue) result["id"] = Id.Value;
    if (FilePath != null) result["file_path"] = FilePath;
    if (ParentHeader != null) result["parent_header"] = ParentHeader;
    if (StartByte.HasValue) result["start_byte"] = StartByte.Value;
    if (EndByte.HasValue) result["end_byte"] = EndByte.Value;
    if (CreatedAt.HasValue) result["created_at"] = CreatedAt.Value.ToString("O");
    if (UpdatedAt.HasValue) result["updated_at"] = UpdatedAt.Value.ToString("O");
    if (Metadata != null) result["metadata"] = Metadata;

    return result;
}
```

## Testing Stubs

### Unit Tests

```csharp
using Xunit;
using ChunkHound.Core.Models;
using ChunkHound.Core.Types;

namespace ChunkHound.Core.Tests.Models
{
    public class ChunkTests
    {
        [Fact]
        public void Constructor_ValidParameters_CreatesChunk()
        {
            // Arrange
            var symbol = "TestFunction";
            var startLine = 1;
            var endLine = 5;
            var code = "function test() { return true; }";
            var chunkType = ChunkType.Function;
            var fileId = 1;
            var language = Language.JavaScript;

            // Act
            var chunk = new Chunk(symbol, startLine, endLine, code, chunkType, fileId, language);

            // Assert
            Assert.Equal(symbol, chunk.Symbol);
            Assert.Equal(startLine, chunk.StartLine);
            Assert.Equal(endLine, chunk.EndLine);
            Assert.Equal(code, chunk.Code);
            Assert.Equal(chunkType, chunk.ChunkType);
            Assert.Equal(fileId, chunk.FileId);
            Assert.Equal(language, chunk.Language);
        }

        [Fact]
        public void Constructor_InvalidStartLine_ThrowsValidationException()
        {
            // Arrange
            var code = "test";
            var chunkType = ChunkType.Function;
            var language = Language.JavaScript;

            // Act & Assert
            Assert.Throws<ValidationException>(() =>
                new Chunk(null, 0, 5, code, chunkType, 1, language));
        }

        [Fact]
        public void LineCount_CalculatesCorrectly()
        {
            // Arrange
            var chunk = new Chunk(null, 1, 5, "test", ChunkType.Function, 1, Language.JavaScript);

            // Act
            var lineCount = chunk.LineCount;

            // Assert
            Assert.Equal(5, lineCount);
        }

        [Fact]
        public void ContainsLine_LineWithinRange_ReturnsTrue()
        {
            // Arrange
            var chunk = new Chunk(null, 1, 5, "test", ChunkType.Function, 1, Language.JavaScript);

            // Act
            var contains = chunk.ContainsLine(3);

            // Assert
            Assert.True(contains);
        }

        [Fact]
        public void FromDict_ValidData_CreatesChunk()
        {
            // Arrange
            var data = new Dictionary<string, object>
            {
                ["symbol"] = "TestFunction",
                ["start_line"] = 1,
                ["end_line"] = 5,
                ["code"] = "function test() { return true; }",
                ["chunk_type"] = "function",
                ["file_id"] = 1,
                ["language"] = "javascript"
            };

            // Act
            var chunk = Chunk.FromDict(data);

            // Assert
            Assert.Equal("TestFunction", chunk.Symbol);
            Assert.Equal(1, chunk.StartLine);
            Assert.Equal(5, chunk.EndLine);
        }

        [Fact]
        public void ToDict_ConvertsCorrectly()
        {
            // Arrange
            var chunk = new Chunk("Test", 1, 5, "code", ChunkType.Function, 1, Language.JavaScript);

            // Act
            var dict = chunk.ToDict();

            // Assert
            Assert.Equal("Test", dict["symbol"]);
            Assert.Equal(1, dict["start_line"]);
            Assert.Equal(5, dict["end_line"]);
            Assert.Equal("code", dict["code"]);
        }
    }
}
```

## Dependencies

- `ChunkHound.Core.Types.ChunkType`
- `ChunkHound.Core.Types.Language`
- `ChunkHound.Core.Exceptions.ValidationException`

## Notes

- This class is designed as a record for immutability, following functional programming principles.
- Validation is performed in the constructor to ensure data integrity.
- Serialization methods provide backward compatibility with existing systems.
- All methods are pure functions where possible to support testability and concurrency.