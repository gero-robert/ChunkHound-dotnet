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

    private const string SYMBOL_FIELD = "symbol";
    private const string START_LINE_FIELD = "start_line";
    private const string END_LINE_FIELD = "end_line";
    private const string CODE_FIELD = "code";
    private const string CHUNK_TYPE_FIELD = "chunk_type";
    private const string TYPE_FIELD = "type";
    private const string FILE_ID_FIELD = "file_id";
    private const string LANGUAGE_FIELD = "language";
    private const string LANGUAGE_INFO_FIELD = "language_info";
    private const string ID_FIELD = "id";
    private const string FILE_PATH_FIELD = "file_path";
    private const string PATH_FIELD = "path";
    private const string PARENT_HEADER_FIELD = "parent_header";
    private const string START_BYTE_FIELD = "start_byte";
    private const string END_BYTE_FIELD = "end_byte";
    private const string CREATED_AT_FIELD = "created_at";
    private const string UPDATED_AT_FIELD = "updated_at";
    private const string METADATA_FIELD = "metadata";

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
        var symbol = data.GetValueOrDefault(SYMBOL_FIELD) as string;
        var startLine = Convert.ToInt32(data.GetValueOrDefault(START_LINE_FIELD) ?? throw new ValidationException(START_LINE_FIELD, null, "Start line is required"));
        var endLine = Convert.ToInt32(data.GetValueOrDefault(END_LINE_FIELD) ?? throw new ValidationException(END_LINE_FIELD, null, "End line is required"));
        var code = data.GetValueOrDefault(CODE_FIELD) as string ?? throw new ValidationException(CODE_FIELD, null, "Code content is required");
        var fileId = Convert.ToInt32(data.GetValueOrDefault(FILE_ID_FIELD) ?? throw new ValidationException(FILE_ID_FIELD, null, "File ID is required"));

        var chunkTypeValue = data.GetValueOrDefault(CHUNK_TYPE_FIELD) ?? data.GetValueOrDefault(TYPE_FIELD);
        var chunkType = chunkTypeValue is ChunkType ct ? ct :
                       chunkTypeValue is string s ? ChunkTypeExtensions.FromString(s) :
                       ChunkType.Unknown;

        var languageValue = data.GetValueOrDefault(LANGUAGE_FIELD) ?? data.GetValueOrDefault(LANGUAGE_INFO_FIELD);
        var language = languageValue is Language l ? l :
                      languageValue is string ls ? LanguageExtensions.FromString(ls) :
                      Language.Unknown;

        var id = data.GetValueOrDefault(ID_FIELD) is int i ? (int?)i : null;
        var filePath = (data.GetValueOrDefault(FILE_PATH_FIELD) ?? data.GetValueOrDefault(PATH_FIELD)) as string;
        var parentHeader = data.GetValueOrDefault(PARENT_HEADER_FIELD) as string;
        var startByte = data.GetValueOrDefault(START_BYTE_FIELD) is long sb ? (long?)sb : null;
        var endByte = data.GetValueOrDefault(END_BYTE_FIELD) is long eb ? (long?)eb : null;

        DateTime? createdAt = null;
        if (data.GetValueOrDefault(CREATED_AT_FIELD) is string cas)
            createdAt = DateTime.Parse(cas);

        DateTime? updatedAt = null;
        if (data.GetValueOrDefault(UPDATED_AT_FIELD) is string uas)
            updatedAt = DateTime.Parse(uas);

        var metadata = data.GetValueOrDefault(METADATA_FIELD) as Dictionary<string, object>;

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
            [SYMBOL_FIELD] = Symbol!, // Symbol can be null, which is expected
            [START_LINE_FIELD] = StartLine,
            [END_LINE_FIELD] = EndLine,
            [CODE_FIELD] = Code,
            [CHUNK_TYPE_FIELD] = ChunkType.Value(),
            [FILE_ID_FIELD] = FileId,
            [LANGUAGE_FIELD] = Language.Value()
        };

        if (Id.HasValue) result[ID_FIELD] = Id.Value;
        if (FilePath != null) result[FILE_PATH_FIELD] = FilePath;
        if (ParentHeader != null) result[PARENT_HEADER_FIELD] = ParentHeader;
        if (StartByte.HasValue) result[START_BYTE_FIELD] = StartByte.Value;
        if (EndByte.HasValue) result[END_BYTE_FIELD] = EndByte.Value;
        if (CreatedAt.HasValue) result[CREATED_AT_FIELD] = CreatedAt.Value.ToString("O");
        if (UpdatedAt.HasValue) result[UPDATED_AT_FIELD] = UpdatedAt.Value.ToString("O");
        if (Metadata != null) result[METADATA_FIELD] = Metadata;

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