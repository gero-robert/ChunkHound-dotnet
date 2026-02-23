using System.Collections.Immutable;
using ChunkHound.Core.Exceptions;

namespace ChunkHound.Core;

/// <summary>
/// Domain model representing a semantic code chunk.
/// This immutable model encapsulates all information about a semantic unit of code
/// that has been extracted from a source file, including its location, content,
/// and metadata.
/// </summary>
public sealed record Chunk
{
    private const string IdField = "id";
    private const string FileIdField = "file_id";
    private const string ContentField = "content";
    private const string StartLineField = "start_line";
    private const string EndLineField = "end_line";
    private const string MetadataField = "metadata";
    private const string EmbeddingField = "embedding";
    private const string CreatedAtField = "created_at";
    private const string UpdatedAtField = "updated_at";

    /// <summary>
    /// Unique chunk identifier
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Reference to the parent file
    /// </summary>
    public int FileId { get; init; }

    /// <summary>
    /// Raw code content
    /// </summary>
    public string Content { get; init; }

    /// <summary>
    /// Alias for Content
    /// </summary>
    public string Code => Content;

    /// <summary>
    /// Starting line number (1-based)
    /// </summary>
    public int StartLine { get; init; }

    /// <summary>
    /// Ending line number (1-based, inclusive)
    /// </summary>
    public int EndLine { get; init; }

    /// <summary>
    /// Programming language
    /// </summary>
    public Language Language { get; init; }

    /// <summary>
    /// Type of chunk
    /// </summary>
    public ChunkType ChunkType { get; init; }

    /// <summary>
    /// Symbol name
    /// </summary>
    public string? Symbol { get; init; }

    /// <summary>
    /// File path
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Language-specific metadata
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = ImmutableDictionary<string, object>.Empty;

    /// <summary>
    /// Vector embedding for semantic search
    /// </summary>
    public ReadOnlyMemory<float>? Embedding { get; init; }

    /// <summary>
    /// When the chunk was first indexed
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the chunk was last updated
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Initializes a new instance of the Chunk class.
    /// </summary>
    public Chunk(string id, int fileId, string content, int startLine, int endLine, Language language = Language.Unknown, ChunkType chunkType = ChunkType.Unknown, string? symbol = null, string? filePath = null, IReadOnlyDictionary<string, object>? metadata = null, ReadOnlyMemory<float>? embedding = null, DateTimeOffset createdAt = default, DateTimeOffset updatedAt = default)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
        if (fileId < 0) throw new ArgumentException("FileId must be non-negative", nameof(fileId));
        if (string.IsNullOrEmpty(content)) throw new ArgumentNullException(nameof(content));
        if (startLine < 1 || endLine < startLine || startLine > endLine) throw new ValidationException("Invalid line range");
        Id = id;
        FileId = fileId;
        Content = content;
        StartLine = startLine;
        EndLine = endLine;
        Language = language;
        ChunkType = chunkType;
        Symbol = symbol;
        FilePath = filePath;
        Metadata = (metadata ?? ImmutableDictionary<string, object>.Empty).ToImmutableDictionary();
        Embedding = embedding;
        CreatedAt = createdAt == default ? DateTimeOffset.UtcNow : createdAt;
        UpdatedAt = updatedAt == default ? DateTimeOffset.UtcNow : updatedAt;
    }

    /// <summary>
    /// Initializes a new instance of the Chunk class (legacy constructor).
    /// </summary>
    public Chunk(string id, int startLine, int endLine, string content, ChunkType chunkType, int fileId, Language language)
        : this(id, fileId, content, startLine, endLine, language, chunkType, id, null)
    {
    }

    /// <summary>
    /// Creates a new Chunk instance with the specified ID.
    /// </summary>
    public Chunk WithId(string id) => new(id, FileId, Content, StartLine, EndLine, Language, ChunkType, Symbol, FilePath, Metadata, Embedding, CreatedAt, UpdatedAt);

    /// <summary>
    /// Creates a new Chunk instance with the specified file ID.
    /// </summary>
    public Chunk WithFileId(int fileId) => new(Id, fileId, Content, StartLine, EndLine, Language, ChunkType, Symbol, FilePath, Metadata, Embedding, CreatedAt, UpdatedAt);

    /// <summary>
    /// Creates a new Chunk instance with the specified content.
    /// </summary>
    public Chunk WithContent(string content) => new(Id, FileId, content, StartLine, EndLine, Language, ChunkType, Symbol, FilePath, Metadata, Embedding, CreatedAt, UpdatedAt);

    /// <summary>
    /// Creates a new Chunk instance with the specified line range.
    /// </summary>
    public Chunk WithLines(int start, int end) => new(Id, FileId, Content, start, end, Language, ChunkType, Symbol, FilePath, Metadata, Embedding, CreatedAt, UpdatedAt);

    /// <summary>
    /// Creates a new Chunk instance with the specified metadata.
    /// </summary>
    public Chunk WithMetadata(IReadOnlyDictionary<string, object> meta) => new(Id, FileId, Content, StartLine, EndLine, Language, ChunkType, Symbol, FilePath, meta, Embedding, CreatedAt, UpdatedAt);

    /// <summary>
    /// Creates a new Chunk instance with the specified embedding.
    /// </summary>
    public Chunk WithEmbedding(ReadOnlyMemory<float>? emb) => new(Id, FileId, Content, StartLine, EndLine, Language, ChunkType, Symbol, FilePath, Metadata, emb, CreatedAt, UpdatedAt);

    /// <summary>
    /// Creates a new Chunk instance with the specified timestamps.
    /// </summary>
    public Chunk WithTimestamps(DateTimeOffset created, DateTimeOffset updated) => new(Id, FileId, Content, StartLine, EndLine, Language, ChunkType, Symbol, FilePath, Metadata, Embedding, created, updated);

    /// <summary>
    /// Creates a new Chunk instance with the updated timestamp.
    /// </summary>
    public Chunk WithUpdatedAt(DateTimeOffset ut) => WithTimestamps(CreatedAt, ut);

    /// <summary>
    /// Creates a Chunk model from a dictionary.
    /// </summary>
    public static Chunk FromDict(Dictionary<string, object> dict)
    {
        var id = dict.GetValueOrDefault(IdField)?.ToString();
        if (string.IsNullOrEmpty(id)) throw new ValidationException("Missing id");
        if (!int.TryParse(dict.GetValueOrDefault(FileIdField)?.ToString(), out var fileId) || fileId <= 0) throw new ValidationException("Invalid file_id");
        var content = dict.GetValueOrDefault(ContentField)?.ToString();
        if (string.IsNullOrEmpty(content)) throw new ValidationException("Missing content");
        if (!int.TryParse(dict.GetValueOrDefault(StartLineField)?.ToString(), out var sl) || sl < 1) throw new ValidationException("Invalid start_line");
        if (!int.TryParse(dict.GetValueOrDefault(EndLineField)?.ToString(), out var el) || el < sl) throw new ValidationException("Invalid end_line");
        var metaObj = dict.GetValueOrDefault(MetadataField);
        var meta = metaObj as Dictionary<string, object> ?? new();
        ReadOnlyMemory<float>? emb = null;
        if (dict.TryGetValue(EmbeddingField, out var eObj) && eObj is IEnumerable<object> floats)
        {
            var arr = floats.Select(f => (float)Convert.ToDouble(f)).ToArray();
            emb = new ReadOnlyMemory<float>(arr);
        }
        DateTimeOffset ca = DateTimeOffset.TryParse(dict.GetValueOrDefault(CreatedAtField)?.ToString(), out var c) ? c : DateTimeOffset.UtcNow;
        DateTimeOffset ua = DateTimeOffset.TryParse(dict.GetValueOrDefault(UpdatedAtField)?.ToString(), out var u) ? u : DateTimeOffset.UtcNow;
        // For backward compatibility, set defaults
        var language = Language.Unknown;
        var chunkType = ChunkType.Unknown;
        string? symbol = null;
        string? filePath = null;
        return new Chunk(id, fileId, content, sl, el, language, chunkType, symbol, filePath, meta, emb, ca, ua);
    }

    /// <summary>
    /// Converts Chunk model to dictionary.
    /// </summary>
    public Dictionary<string, object> ToDict() => new()
    {
        [IdField] = Id,
        [FileIdField] = FileId.ToString(),
        [ContentField] = Content,
        [StartLineField] = StartLine,
        [EndLineField] = EndLine,
        [MetadataField] = Metadata.ToDictionary(k => k.Key, v => v.Value),
        [EmbeddingField] = Embedding?.ToArray(),
        [CreatedAtField] = CreatedAt.ToString("O"),
        [UpdatedAtField] = UpdatedAt.ToString("O")
    };
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