namespace ChunkHound.Core;

/// <summary>
/// Enumeration of supported programming languages.
/// </summary>
public enum Language
{
    CSharp,
    Python,
    JavaScript,
    TypeScript,
    Java,
    Go,
    Rust,
    // Add more as needed
}

/// <summary>
/// Extension methods for Language enum.
/// </summary>
public static class LanguageExtensions
{
    /// <summary>
    /// Gets the file extension for the language.
    /// </summary>
    public static string GetExtension(this Language language) => language switch
    {
        Language.CSharp => ".cs",
        Language.Python => ".py",
        Language.JavaScript => ".js",
        Language.TypeScript => ".ts",
        Language.Java => ".java",
        Language.Go => ".go",
        Language.Rust => ".rs",
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };
}