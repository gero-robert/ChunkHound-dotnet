# ChunkCacheService Service Design Document

> **Spec Version:** 1.0 | **Code:** `ChunkHound.Services.ChunkCacheService` | **Status:** draft

## Overview

The `ChunkCacheService` class provides content-based chunk comparison functionality to minimize unnecessary embedding regeneration. This service compares chunks by normalized content to identify changes, supporting efficient incremental indexing.

This design is ported from the Python `ChunkCacheService` in `chunkhound/services/chunk_cache_service.py`, adapted for C# with async patterns and .NET conventions.

## Class Definition

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using ChunkHound.Core.Models;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Services
{
    /// <summary>
    /// Service for comparing chunks based on direct content comparison to minimize embedding regeneration.
    /// </summary>
    public class ChunkCacheService
    {
        // Properties and methods defined below
    }
}
```

## ChunkDiff Record

```csharp
/// <summary>
/// Represents differences between new and existing chunks for smart updates.
/// </summary>
public record ChunkDiff
{
    /// <summary>
    /// Chunks with matching content.
    /// </summary>
    public List<Chunk> Unchanged { get; init; } = new();

    /// <summary>
    /// Chunks with different content.
    /// </summary>
    public List<Chunk> Modified { get; init; } = new();

    /// <summary>
    /// New chunks not in existing set.
    /// </summary>
    public List<Chunk> Added { get; init; } = new();

    /// <summary>
    /// Existing chunks not in new set.
    /// </summary>
    public List<Chunk> Deleted { get; init; } = new();
}
```

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Logger` | `ILogger<ChunkCacheService>` | Logger for diagnostic information |

## Constructor and Dependencies

```csharp
/// <summary>
/// Initializes a new instance of the ChunkCacheService class.
/// </summary>
public ChunkCacheService(ILogger<ChunkCacheService>? logger = null)
{
    Logger = logger ?? NullLogger<ChunkCacheService>.Instance;
}

// Private fields
private readonly ILogger<ChunkCacheService> _logger;
```

## Core Methods

### DiffChunks

```csharp
/// <summary>
/// Compare chunks by normalized content comparison to identify changes.
/// </summary>
public ChunkDiff DiffChunks(List<Chunk> newChunks, List<Chunk> existingChunks)
{
    Logger.LogInformation("Comparing {NewCount} new chunks with {ExistingCount} existing chunks",
        newChunks.Count, existingChunks.Count);

    // Build content lookup for existing chunks using normalized comparison
    var existingByContent = new Dictionary<string, List<Chunk>>();
    foreach (var chunk in existingChunks)
    {
        var normalizedCode = NormalizeCodeForComparison(chunk.Code);
        if (!existingByContent.ContainsKey(normalizedCode))
        {
            existingByContent[normalizedCode] = new List<Chunk>();
        }
        existingByContent[normalizedCode].Add(chunk);
    }

    // Build content lookup for new chunks
    var newByContent = new Dictionary<string, List<Chunk>>();
    foreach (var chunk in newChunks)
    {
        var normalizedCode = NormalizeCodeForComparison(chunk.Code);
        if (!newByContent.ContainsKey(normalizedCode))
        {
            newByContent[normalizedCode] = new List<Chunk>();
        }
        newByContent[normalizedCode].Add(chunk);
    }

    // Find intersections and differences using content strings
    var existingContent = new HashSet<string>(existingByContent.Keys);
    var newContent = new HashSet<string>(newByContent.Keys);

    var unchangedContent = existingContent.Intersect(newContent);
    var deletedContent = existingContent.Except(newContent);
    var addedContent = newContent.Except(existingContent);

    // Flatten lists for result
    var unchangedChunks = unchangedContent.SelectMany(content => existingByContent[content]).ToList();
    var deletedChunks = deletedContent.SelectMany(content => existingByContent[content]).ToList();
    var addedChunks = addedContent.SelectMany(content => newByContent[content]).ToList();

    var result = new ChunkDiff
    {
        Unchanged = unchangedChunks,
        Modified = new List<Chunk>(), // Still using add/delete approach for simplicity
        Added = addedChunks,
        Deleted = deletedChunks
    };

    Logger.LogInformation("Diff result: {Unchanged} unchanged, {Added} added, {Deleted} deleted",
        result.Unchanged.Count, result.Added.Count, result.Deleted.Count);

    return result;
}
```

### NormalizeCodeForComparison

```csharp
/// <summary>
/// Normalize code content for comparison while preserving semantic meaning.
/// </summary>
private string NormalizeCodeForComparison(string code)
{
    // Normalize line endings: Windows CRLF (\r\n) and Mac CR (\r) to Unix LF (\n)
    var normalized = code.Replace("\r\n", "\n").Replace("\r", "\n");

    // Strip leading and trailing whitespace for consistent comparison
    normalized = normalized.Trim();

    return normalized;
}
```

## Testing Stubs

### Unit Tests

```csharp
using Xunit;
using Moq;
using ChunkHound.Services;
using ChunkHound.Core.Models;
using ChunkHound.Core.Types;
using System.Collections.Generic;

namespace ChunkHound.Services.Tests
{
    public class ChunkCacheServiceTests
    {
        private readonly ChunkCacheService _service;

        public ChunkCacheServiceTests()
        {
            _service = new ChunkCacheService();
        }

        [Fact]
        public void DiffChunks_NoChanges_ReturnsUnchanged()
        {
            // Arrange
            var chunk1 = new Chunk("func", 1, 3, "function test() {}", ChunkType.Function, 1, Language.JavaScript);
            var chunk2 = new Chunk("class", 5, 7, "class Test {}", ChunkType.Class, 1, Language.JavaScript);
            
            var newChunks = new List<Chunk> { chunk1, chunk2 };
            var existingChunks = new List<Chunk> { chunk1, chunk2 };

            // Act
            var result = _service.DiffChunks(newChunks, existingChunks);

            // Assert
            Assert.Equal(2, result.Unchanged.Count);
            Assert.Empty(result.Added);
            Assert.Empty(result.Deleted);
            Assert.Empty(result.Modified);
        }

        [Fact]
        public void DiffChunks_NewChunk_ReturnsAdded()
        {
            // Arrange
            var existingChunk = new Chunk("func", 1, 3, "function test() {}", ChunkType.Function, 1, Language.JavaScript);
            var newChunk = new Chunk("class", 5, 7, "class Test {}", ChunkType.Class, 1, Language.JavaScript);
            
            var newChunks = new List<Chunk> { existingChunk, newChunk };
            var existingChunks = new List<Chunk> { existingChunk };

            // Act
            var result = _service.DiffChunks(newChunks, existingChunks);

            // Assert
            Assert.Equal(1, result.Unchanged.Count);
            Assert.Equal(1, result.Added.Count);
            Assert.Empty(result.Deleted);
            Assert.Empty(result.Modified);
        }

        [Fact]
        public void DiffChunks_DeletedChunk_ReturnsDeleted()
        {
            // Arrange
            var existingChunk = new Chunk("func", 1, 3, "function test() {}", ChunkType.Function, 1, Language.JavaScript);
            var newChunk = new Chunk("class", 5, 7, "class Test {}", ChunkType.Class, 1, Language.JavaScript);
            
            var newChunks = new List<Chunk> { newChunk };
            var existingChunks = new List<Chunk> { existingChunk, newChunk };

            // Act
            var result = _service.DiffChunks(newChunks, existingChunks);

            // Assert
            Assert.Equal(1, result.Unchanged.Count);
            Assert.Empty(result.Added);
            Assert.Equal(1, result.Deleted.Count);
            Assert.Empty(result.Modified);
        }

        [Fact]
        public void NormalizeCodeForComparison_NormalizesLineEndings()
        {
            // Arrange
            var code = "function test() {\r\n  return true;\r\n}";

            // Act
            var normalized = _service.GetType()
                .GetMethod("NormalizeCodeForComparison", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(_service, new object[] { code }) as string;

            // Assert
            Assert.Equal("function test() {\n  return true;\n}", normalized);
        }
    }
}
```

## Dependencies

- `ChunkHound.Core.Models.Chunk`
- `Microsoft.Extensions.Logging`

## Notes

- Content normalization ensures consistent comparison across different line ending formats.
- The service uses normalized content as keys for efficient lookup and comparison.
- Modified chunks are currently handled as add/delete operations for simplicity.
- Thread-safe for concurrent operations as it has no mutable state.