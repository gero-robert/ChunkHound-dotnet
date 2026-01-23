using System.IO;
using ChunkHound.Core.Exceptions;

namespace ChunkHound.Core;

/// <summary>
/// Domain model representing a source code file.
/// This immutable model encapsulates all information about a file that has been
/// indexed by ChunkHound, including its path, metadata, and language information.
/// </summary>
public record File
{
    /// <summary>
    /// Unique file identifier
    /// </summary>
    public int? Id { get; init; }

    /// <summary>
    /// Relative path to the file (with forward slashes)
    /// </summary>
    public string Path { get; init; }

    /// <summary>
    /// Last modification time as Unix timestamp
    /// </summary>
    public double Mtime { get; init; }

    /// <summary>
    /// Programming language of the file
    /// </summary>
    public Language Language { get; init; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// Fast checksum for change detection
    /// </summary>
    public string? ContentHash { get; init; }

    /// <summary>
    /// When the file was first indexed
    /// </summary>
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// When the file record was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; init; }

    private const string PATH_FIELD = "path";
    private const string MTIME_FIELD = "mtime";
    private const string SIZE_BYTES_FIELD = "size_bytes";
    private const string LANGUAGE_FIELD = "language";
    private const string ID_FIELD = "id";
    private const string CONTENT_HASH_FIELD = "content_hash";
    private const string CREATED_AT_FIELD = "created_at";
    private const string UPDATED_AT_FIELD = "updated_at";

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

    /// <summary>
    /// Checks if the file's language is supported by ChunkHound.
    /// </summary>
    public bool IsSupportedLanguage() => Language != Language.Unknown;

    /// <summary>
    /// Creates a new File instance with the specified ID.
    /// </summary>
    public File WithId(int fileId) => this with { Id = fileId };

    /// <summary>
    /// Creates a new File instance with updated modification time.
    /// </summary>
    public File WithUpdatedMtime(double newMtime) => this with { Mtime = newMtime, UpdatedAt = DateTime.UtcNow };

    /// <summary>
    /// Creates a File model from a dictionary.
    /// </summary>
    public static File FromDict(Dictionary<string, object> data)
    {
        var path = data.GetValueOrDefault(PATH_FIELD) as string ?? throw new ValidationException("path", null, "Path is required");
        var mtime = Convert.ToDouble(data.GetValueOrDefault(MTIME_FIELD) ?? throw new ValidationException("mtime", null, "Modification time is required"));
        var sizeBytes = Convert.ToInt64(data.GetValueOrDefault(SIZE_BYTES_FIELD) ?? 0);

        var languageValue = data.GetValueOrDefault(LANGUAGE_FIELD);
        var language = languageValue is Language l ? l :
                       languageValue is string s ? LanguageExtensions.FromString(s) :
                       Language.Unknown;

        var id = data.GetValueOrDefault(ID_FIELD) is int i ? (int?)i : null;
        var contentHash = data.GetValueOrDefault(CONTENT_HASH_FIELD) as string;

        DateTime? createdAt = null;
        if (data.GetValueOrDefault(CREATED_AT_FIELD) is string cas)
            createdAt = DateTime.Parse(cas);

        DateTime? updatedAt = null;
        if (data.GetValueOrDefault(UPDATED_AT_FIELD) is string uas)
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
            [PATH_FIELD] = Path,
            [MTIME_FIELD] = Mtime,
            [LANGUAGE_FIELD] = Language.Value(),
            [SIZE_BYTES_FIELD] = SizeBytes
        };

        if (Id.HasValue) result[ID_FIELD] = Id.Value;
        if (ContentHash != null) result[CONTENT_HASH_FIELD] = ContentHash;
        if (CreatedAt.HasValue) result[CREATED_AT_FIELD] = CreatedAt.Value.ToString("O");
        if (UpdatedAt.HasValue) result[UPDATED_AT_FIELD] = UpdatedAt.Value.ToString("O");

        return result;
    }
}