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
    C,
    Cpp,
    Go,
    Rust,
    PHP,
    Ruby,
    Yaml,
    /// <summary>
    /// Unknown or unsupported language
    /// </summary>
    Unknown
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
        Language.C => ".c",
        Language.Cpp => ".cpp",
        Language.Go => ".go",
        Language.Rust => ".rs",
        Language.PHP => ".php",
        Language.Ruby => ".rb",
        Language.Yaml => ".yaml",
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };

    /// <summary>
    /// Gets the string value representation of the language.
    /// </summary>
    public static string Value(this Language language) => language switch
    {
        Language.CSharp => "csharp",
        Language.Python => "python",
        Language.JavaScript => "javascript",
        Language.TypeScript => "typescript",
        Language.Java => "java",
        Language.C => "c",
        Language.Cpp => "cpp",
        Language.Go => "go",
        Language.Rust => "rust",
        Language.PHP => "php",
        Language.Ruby => "ruby",
        Language.Yaml => "yaml",
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };

    /// <summary>
    /// Creates a Language from a string value.
    /// </summary>
    public static Language FromString(string value) => value.ToLowerInvariant() switch
    {
        "csharp" or "c#" => Language.CSharp,
        "python" or "py" => Language.Python,
        "javascript" or "js" => Language.JavaScript,
        "typescript" or "ts" => Language.TypeScript,
        "java" => Language.Java,
        "c" => Language.C,
        "cpp" or "c++" => Language.Cpp,
        "go" => Language.Go,
        "rust" or "rs" => Language.Rust,
        "php" => Language.PHP,
        "ruby" or "rb" => Language.Ruby,
        "yaml" or "yml" => Language.Yaml,
        _ => Language.Unknown
    };
}