# File Model Design Document

> **Spec Version:** 1.0 | **Code:** `ChunkHound.Core.Models.File` | **Status:** draft

## Overview

The `File` class represents a source code file in the ChunkHound system. This immutable model encapsulates file metadata, state, and provides methods for working with file data in a type-safe manner.

This design is ported from the Python `File` dataclass in `chunkhound/core/models/file.py`, adapted for C# with .NET conventions.

## Class Definition

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using ChunkHound.Core.Types;

namespace ChunkHound.Core.Models
{
    /// <summary>
    /// Domain model representing a source code file.
    /// This immutable model encapsulates all information about a file that has been
    /// indexed by ChunkHound, including its path, metadata, and language information.
    /// </summary>
    public record File
    {
        // Properties defined below
    }
}
```

## Properties

| Property | Type | Description | Required | Default |
|----------|------|-------------|----------|---------|
| `Id` | `int?` | Unique file identifier | No | `null` |
| `Path` | `string` | Relative path to the file (with forward slashes) | Yes | - |
| `Mtime` | `double` | Last modification time as Unix timestamp | Yes | - |
| `Language` | `Language` | Programming language of the file | Yes | - |
| `SizeBytes` | `long` | File size in bytes | Yes | - |
| `ContentHash` | `string?` | Fast checksum for change detection | No | `null` |
| `CreatedAt` | `DateTime?` | When the file was first indexed | No | `null` |
| `UpdatedAt` | `DateTime?` | When the file record was last updated | No | `null` |

## Constructor and Validation

```csharp
/// <summary>
/// Initializes a new instance of the File class.
/// </summary>
public File(
    string path,
    double mtime,
    Language language,
    long sizeBytes,
    int? id = null,
    string? contentHash = null,
    DateTime? createdAt = null,
    DateTime? updatedAt = null)
{
    Path = path ?? throw new ArgumentNullException(nameof(path));
    Mtime = mtime;
    Language = language;
    SizeBytes = sizeBytes;
    Id = id;
    ContentHash = contentHash;
    CreatedAt = createdAt;
    UpdatedAt = updatedAt;

    Validate();
}

/// <summary>
/// Validates file model attributes.
/// </summary>
private void Validate()
{
    // Path validation
    if (string.IsNullOrWhiteSpace(Path))
        throw new ValidationException("Path", Path, "Path cannot be empty");

    // Size validation
    if (SizeBytes < 0)
        throw new ValidationException("SizeBytes", SizeBytes, "File size cannot be negative");

    // mtime validation
    if (Mtime < 0)
        throw new ValidationException("Mtime", Mtime, "Modification time cannot be negative");
}
```

## Computed Properties

```csharp
/// <summary>
/// Gets the file name (without directory path).
/// </summary>
public string Name => System.IO.Path.GetFileName(Path);

/// <summary>
/// Gets the file extension.
/// </summary>
public string Extension => System.IO.Path.GetExtension(Path);

/// <summary>
/// Gets the file name without extension.
/// </summary>
public string Stem => System.IO.Path.GetFileNameWithoutExtension(Path);

/// <summary>
/// Gets the parent directory path.
/// </summary>
public string ParentDir => System.IO.Path.GetDirectoryName(Path) ?? "";

/// <summary>
/// Gets relative path (path is already stored as relative).
/// </summary>
public string RelativePath => Path;
```

## Methods

### Utility Methods

```csharp
/// <summary>
/// Checks if the file's language is supported by ChunkHound.
/// </summary>
public bool IsSupportedLanguage() => Language != Language.Unknown;
```

### Factory Methods

```csharp
/// <summary>
/// Creates a new File instance with the specified ID.
/// </summary>
public File WithId(int fileId) => this with { Id = fileId };

/// <summary>
/// Creates a new File instance with updated modification time.
/// </summary>
public File WithUpdatedMtime(double newMtime) => this with { Mtime = newMtime, UpdatedAt = DateTime.UtcNow };
```

## Serialization

### JSON Serialization

The class uses `System.Text.Json` for serialization with custom converters for enums and nullable types.

```csharp
/// <summary>
/// Creates a File model from a dictionary.
/// </summary>
public static File FromDict(Dictionary<string, object> data)
{
    // Implementation similar to Python from_dict
    var path = data.GetValueOrDefault("path") as string ?? throw new ValidationException("path", null, "Path is required");
    var mtime = Convert.ToDouble(data.GetValueOrDefault("mtime") ?? throw new ValidationException("mtime", null, "Modification time is required"));
    var sizeBytes = Convert.ToInt64(data.GetValueOrDefault("size_bytes") ?? 0);

    var languageValue = data.GetValueOrDefault("language");
    var language = languageValue is Language l ? l :
                   languageValue is string s ? LanguageExtensions.FromString(s) :
                   Language.Unknown;

    var id = data.GetValueOrDefault("id") is int i ? (int?)i : null;
    var contentHash = data.GetValueOrDefault("content_hash") as string;

    DateTime? createdAt = null;
    if (data.GetValueOrDefault("created_at") is string cas)
        createdAt = DateTime.Parse(cas);

    DateTime? updatedAt = null;
    if (data.GetValueOrDefault("updated_at") is string uas)
        updatedAt = DateTime.Parse(uas);

    return new File(
        path, mtime, language, sizeBytes,
        id, contentHash, createdAt, updatedAt);
}

/// <summary>
/// Converts File model to dictionary.
/// </summary>
public Dictionary<string, object> ToDict()
{
    var result = new Dictionary<string, object>
    {
        ["path"] = Path,
        ["mtime"] = Mtime,
        ["language"] = Language.Value,
        ["size_bytes"] = SizeBytes
    };

    if (Id.HasValue) result["id"] = Id.Value;
    if (ContentHash != null) result["content_hash"] = ContentHash;
    if (CreatedAt.HasValue) result["created_at"] = CreatedAt.Value.ToString("O");
    if (UpdatedAt.HasValue) result["updated_at"] = UpdatedAt.Value.ToString("O");

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
    public class FileTests
    {
        [Fact]
        public void Constructor_ValidParameters_CreatesFile()
        {
            // Arrange
            var path = "src/main.cs";
            var mtime = 1640995200.0; // 2022-01-01
            var language = Language.CSharp;
            var sizeBytes = 1024L;

            // Act
            var file = new File(path, mtime, language, sizeBytes);

            // Assert
            Assert.Equal(path, file.Path);
            Assert.Equal(mtime, file.Mtime);
            Assert.Equal(language, file.Language);
            Assert.Equal(sizeBytes, file.SizeBytes);
        }

        [Fact]
        public void Constructor_InvalidPath_ThrowsValidationException()
        {
            // Arrange
            var language = Language.CSharp;

            // Act & Assert
            Assert.Throws<ValidationException>(() =>
                new File("", 1640995200.0, language, 1024));
        }

        [Fact]
        public void Name_ReturnsFileName()
        {
            // Arrange
            var file = new File("src/main.cs", 1640995200.0, Language.CSharp, 1024);

            // Act
            var name = file.Name;

            // Assert
            Assert.Equal("main.cs", name);
        }

        [Fact]
        public void Extension_ReturnsFileExtension()
        {
            // Arrange
            var file = new File("src/main.cs", 1640995200.0, Language.CSharp, 1024);

            // Act
            var extension = file.Extension;

            // Assert
            Assert.Equal(".cs", extension);
        }

        [Fact]
        public void IsSupportedLanguage_UnknownLanguage_ReturnsFalse()
        {
            // Arrange
            var file = new File("unknown.xyz", 1640995200.0, Language.Unknown, 1024);

            // Act
            var isSupported = file.IsSupportedLanguage();

            // Assert
            Assert.False(isSupported);
        }

        [Fact]
        public void FromDict_ValidData_CreatesFile()
        {
            // Arrange
            var data = new Dictionary<string, object>
            {
                ["path"] = "src/main.cs",
                ["mtime"] = 1640995200.0,
                ["language"] = "csharp",
                ["size_bytes"] = 1024L
            };

            // Act
            var file = File.FromDict(data);

            // Assert
            Assert.Equal("src/main.cs", file.Path);
            Assert.Equal(1640995200.0, file.Mtime);
            Assert.Equal(Language.CSharp, file.Language);
            Assert.Equal(1024L, file.SizeBytes);
        }

        [Fact]
        public void ToDict_ConvertsCorrectly()
        {
            // Arrange
            var file = new File("src/main.cs", 1640995200.0, Language.CSharp, 1024);

            // Act
            var dict = file.ToDict();

            // Assert
            Assert.Equal("src/main.cs", dict["path"]);
            Assert.Equal(1640995200.0, dict["mtime"]);
            Assert.Equal("csharp", dict["language"]);
            Assert.Equal(1024L, dict["size_bytes"]);
        }

        [Fact]
        public void WithId_CreatesNewInstanceWithId()
        {
            // Arrange
            var file = new File("src/main.cs", 1640995200.0, Language.CSharp, 1024);

            // Act
            var fileWithId = file.WithId(123);

            // Assert
            Assert.Equal(123, fileWithId.Id);
            Assert.Equal(file.Path, fileWithId.Path); // Other properties unchanged
        }
    }
}
```

## Dependencies

- `ChunkHound.Core.Types.Language`
- `ChunkHound.Core.Exceptions.ValidationException`

## Notes

- This class is designed as a record for immutability, following functional programming principles.
- Validation is performed in the constructor to ensure data integrity.
- Serialization methods provide backward compatibility with existing systems.
- All methods are pure functions where possible to support testability and concurrency.