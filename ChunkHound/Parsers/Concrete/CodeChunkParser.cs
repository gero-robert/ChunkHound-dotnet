using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ChunkHound.Core;

namespace ChunkHound.Parsers.Concrete;

/// <summary>
/// Parser for code files.
/// Detects language and extracts functions/classes.
/// </summary>
public class CodeChunkParser : IChunkParser
{
    private static readonly string[] SupportedExtensions = { ".cs", ".py", ".js", ".ts", ".java", ".cpp", ".c", ".go", ".rs", ".php", ".rb" };

    /// <summary>
    /// Determines if this parser can handle the given file extension.
    /// </summary>
    /// <param name="fileExtension">The file extension to check.</param>
    /// <returns>True if the extension is a supported code file extension, otherwise false.</returns>
    public bool CanHandle(string fileExtension)
    {
        return SupportedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses the code content and returns chunks for functions and classes.
    /// </summary>
    /// <param name="content">The code file content.</param>
    /// <param name="filePath">The path to the code file.</param>
    /// <returns>A list of chunks representing the parsed code elements.</returns>
    public async Task<IReadOnlyList<Chunk>> ParseAsync(string content, string filePath)
    {
        var chunks = new List<Chunk>();
        var lines = content.Split('\n');
        var language = DetectLanguage(filePath);

        // Simple regex-based parsing for functions and classes
        var patterns = GetPatternsForLanguage(language);

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(content, pattern.Regex, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var symbol = match.Groups["name"].Value.Trim();
                if (string.IsNullOrEmpty(symbol)) continue;

                // Find the line number
                var beforeMatch = content.Substring(0, match.Index);
                var startLine = beforeMatch.Split('\n').Length;

                // Estimate end line (simple: find next similar pattern or end of file)
                var afterMatch = content.Substring(match.Index + match.Length);
                var nextMatch = Regex.Match(afterMatch, pattern.Regex, RegexOptions.Multiline | RegexOptions.IgnoreCase);
                var endLine = nextMatch.Success
                    ? startLine + afterMatch.Substring(0, nextMatch.Index).Split('\n').Length
                    : lines.Length;

                // Extract code
                var codeLines = lines.Skip(startLine - 1).Take(endLine - startLine + 1);
                var code = string.Join('\n', codeLines);

                var chunk = new Chunk(
                    symbol,
                    startLine,
                    endLine,
                    code,
                    pattern.Type,
                    0,
                    language);

                chunks.Add(chunk);
            }
        }

        // If no chunks found, create a single chunk for the whole file (if content is not empty)
        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(content))
        {
            var chunk = new Chunk(
                Path.GetFileNameWithoutExtension(filePath),
                1,
                lines.Length,
                content,
                ChunkType.Unknown,
                0,
                language);
            chunks.Add(chunk);
        }

        return chunks;
    }

    private static Language DetectLanguage(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cs" => Language.CSharp,
            ".py" => Language.Python,
            ".js" => Language.JavaScript,
            ".ts" => Language.TypeScript,
            ".java" => Language.Java,
            ".cpp" or ".c" => Language.Cpp,
            ".go" => Language.Go,
            ".rs" => Language.Rust,
            ".php" => Language.PHP,
            ".rb" => Language.Ruby,
            _ => Language.Unknown
        };
    }

    private static List<(string Regex, ChunkType Type)> GetPatternsForLanguage(Language language)
    {
        return language switch
        {
            Language.CSharp => new List<(string, ChunkType)>
            {
                (@"^\s*(?:public|private|protected|internal)?\s*(?:static|virtual|override|abstract)?\s*(?:async)?\s*(?:\w+\s+)+\s*(?<name>\w+)\s*\(", ChunkType.Function),
                (@"^\s*(?:public|private|protected|internal)?\s*(?:static|abstract|sealed)?\s*(?:class|interface|struct|enum)\s+(?<name>\w+)", ChunkType.Class)
            },
            Language.Python => new List<(string, ChunkType)>
            {
                (@"^\s*def\s+(?<name>\w+)\s*\(", ChunkType.Function),
                (@"^\s*class\s+(?<name>\w+)", ChunkType.Class)
            },
            Language.JavaScript or Language.TypeScript => new List<(string, ChunkType)>
            {
                (@"^\s*(?:function|const|let|var)?\s*(?<name>\w+)\s*\(", ChunkType.Function),
                (@"^\s*class\s+(?<name>\w+)", ChunkType.Class)
            },
            _ => new List<(string, ChunkType)>
            {
                (@"^\s*(?:function|def|func)\s+(?<name>\w+)\s*\(", ChunkType.Function),
                (@"^\s*class\s+(?<name>\w+)", ChunkType.Class)
            }
        };
    }
}