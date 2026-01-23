using System.IO;
using ChunkHound.Core.Exceptions;

namespace ChunkHound.Core;

/// <summary>
/// Domain model representing a semantic code chunk.
/// This immutable model encapsulates all information about a semantic unit of code
/// that has been extracted from a source file, including its location, content,
/// and metadata.
/// </summary>
public record Chunk
{
    /// <summary>
    /// Function, class, or element name
    /// </summary>
    public string? Symbol { get; init; }

    /// <summary>
    /// Starting line number (1-based)
    /// </summary>
    public int StartLine { get; init; }

    /// <summary>
    /// Ending line number (1-based, inclusive)
    /// </summary>
    public int EndLine { get; init; }

    /// <summary>
    /// Raw code content
    /// </summary>
    public string Code { get; init; }

    /// <summary>
    /// Type of semantic chunk
    /// </summary>
    public ChunkType ChunkType { get; init; }

    /// <summary>
    /// Reference to the parent file
    /// </summary>
    public int FileId { get; init; }

    /// <summary>
    /// Programming language of the chunk
    /// </summary>
    public Language Language { get; init; }

    /// <summary>
    /// Unique chunk identifier
    /// </summary>
    public int? Id { get; init; }

    /// <summary>
    /// Path to the source file
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Parent header for nested content (markdown)
    /// </summary>
    public string? ParentHeader { get; init; }

    /// <summary>
    /// Starting byte offset
    /// </summary>
    public long? StartByte { get; init; }

    /// <summary>
    /// Ending byte offset
    /// </summary>
    public long? EndByte { get; init; }

    /// <summary>
    /// When the chunk was first indexed
    /// </summary>
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// When the chunk was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// Language-specific metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

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
            throw new ValidationException("Symbol", Symbol, "Symbol cannot be whitespace only");
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
    public string DisplayName => ChunkType.IsCode()
        ? $"{ChunkType.Value()}: {Symbol}"
        : $"{ChunkType.Value()}: {Code[..Math.Min(50, Code.Length)].Replace("\n", " ").Trim()}{(Code.Length > 50 ? "..." : "")}";

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

    /// <summary>
    /// Checks if this chunk represents code structure.
    /// </summary>
    public bool IsCodeChunk() => ChunkType.IsCode();

    /// <summary>
    /// Checks if this chunk represents documentation.
    /// </summary>
    public bool IsDocumentationChunk() => ChunkType.IsDocumentation();

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

    /// <summary>
    /// Creates a new Chunk instance with the specified ID.
    /// </summary>
    public Chunk WithId(int chunkId) => this with { Id = chunkId };

    /// <summary>
    /// Creates a new Chunk instance with the specified file path.
    /// </summary>
    public Chunk WithFilePath(string filePath) => this with { FilePath = filePath };

    /// <summary>
    /// Creates a Chunk model from a dictionary.
    /// </summary>
    public static Chunk FromDict(Dictionary<string, object> data)
    {
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
        var filePath = (data.GetValueOrDefault("file_path") ?? data.GetValueOrDefault("path")) as string;
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
            ["symbol"] = Symbol!, // Symbol can be null, which is expected
            ["start_line"] = StartLine,
            ["end_line"] = EndLine,
            ["code"] = Code,
            ["chunk_type"] = ChunkType.Value(),
            ["file_id"] = FileId,
            ["language"] = Language.Value()
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