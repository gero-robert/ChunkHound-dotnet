namespace ChunkHound.Core;

/// <summary>
/// Enumeration of supported chunk types for semantic code parsing.
/// </summary>
public enum ChunkType
{
    /// <summary>
    /// Function or method definition
    /// </summary>
    Function,

    /// <summary>
    /// Class definition
    /// </summary>
    Class,

    /// <summary>
    /// Interface definition
    /// </summary>
    Interface,

    /// <summary>
    /// Struct definition
    /// </summary>
    Struct,

    /// <summary>
    /// Enum definition
    /// </summary>
    Enum,

    /// <summary>
    /// Module or namespace
    /// </summary>
    Module,

    /// <summary>
    /// Documentation comment
    /// </summary>
    Documentation,

    /// <summary>
    /// Import or using statement
    /// </summary>
    Import,

    /// <summary>
    /// Unknown or unclassified chunk type
    /// </summary>
    Unknown
}

/// <summary>
/// Extension methods for ChunkType enum.
/// </summary>
public static class ChunkTypeExtensions
{
    /// <summary>
    /// Gets the string value representation of the chunk type.
    /// </summary>
    public static string Value(this ChunkType chunkType) => chunkType switch
    {
        ChunkType.Function => "function",
        ChunkType.Class => "class",
        ChunkType.Interface => "interface",
        ChunkType.Struct => "struct",
        ChunkType.Enum => "enum",
        ChunkType.Module => "module",
        ChunkType.Documentation => "documentation",
        ChunkType.Import => "import",
        ChunkType.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(chunkType))
    };

    /// <summary>
    /// Checks if this chunk type represents code structure.
    /// </summary>
    public static bool IsCode(this ChunkType chunkType) => chunkType switch
    {
        ChunkType.Function or ChunkType.Class or ChunkType.Interface or
        ChunkType.Struct or ChunkType.Enum or ChunkType.Module => true,
        _ => false
    };

    /// <summary>
    /// Checks if this chunk type represents documentation.
    /// </summary>
    public static bool IsDocumentation(this ChunkType chunkType) => chunkType == ChunkType.Documentation;

    /// <summary>
    /// Creates a ChunkType from a string value.
    /// </summary>
    public static ChunkType FromString(string value) => value.ToLowerInvariant() switch
    {
        "function" => ChunkType.Function,
        "class" => ChunkType.Class,
        "interface" => ChunkType.Interface,
        "struct" => ChunkType.Struct,
        "enum" => ChunkType.Enum,
        "module" => ChunkType.Module,
        "documentation" => ChunkType.Documentation,
        "import" => ChunkType.Import,
        "unknown" => ChunkType.Unknown,
        _ => ChunkType.Unknown
    };
}