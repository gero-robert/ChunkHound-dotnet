using ChunkHound.Core;

namespace ChunkHound.Core;

/// <summary>
/// Provides language-specific configuration for chunking parameters.
/// </summary>
public interface ILanguageConfigProvider
{
    /// <summary>
    /// Gets the configuration for the specified language.
    /// </summary>
    LanguageConfig GetConfig(Language language);
}

/// <summary>
/// Configuration parameters for a specific programming language.
/// </summary>
public class LanguageConfig
{
    /// <summary>
    /// Maximum chunk size in non-whitespace characters.
    /// </summary>
    public int MaxChunkSize { get; set; }

    /// <summary>
    /// Minimum chunk size to avoid tiny fragments.
    /// </summary>
    public int MinChunkSize { get; set; }

    /// <summary>
    /// Conservative token limit to stay under API limits.
    /// </summary>
    public int SafeTokenLimit { get; set; }

    /// <summary>
    /// Keywords that indicate the start of a new chunk.
    /// </summary>
    public IReadOnlySet<string> ChunkStartKeywords { get; set; } = new HashSet<string>();

    /// <summary>
    /// Patterns for determining chunk types.
    /// </summary>
    public IReadOnlyDictionary<string, ChunkType> TypePatterns { get; set; } = new Dictionary<string, ChunkType>();

    /// <summary>
    /// Patterns for extracting symbols from code.
    /// </summary>
    public IReadOnlyDictionary<string, string> SymbolPatterns { get; set; } = new Dictionary<string, string>();
}