using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ChunkHound.Core;

namespace ChunkHound.Parsers.Concrete;

/// <summary>
/// Fallback parser for text files.
/// Splits text into basic chunks.
/// </summary>
public class UniversalTextParser : IChunkParser
{
    private static readonly string[] SupportedExtensions = { ".txt", ".log", ".json", ".xml", ".html", ".css", ".scss", ".sql", ".sh", ".bat", ".ps1", ".yml", ".yaml" };

    /// <summary>
    /// Determines if this parser can handle the given file extension.
    /// Acts as a fallback for text-based files not handled by other parsers.
    /// </summary>
    /// <param name="fileExtension">The file extension to check.</param>
    /// <returns>True if the extension is a supported text file extension, otherwise false.</returns>
    public bool CanHandle(string fileExtension)
    {
        // This is a fallback parser, so it can handle many extensions
        // But in practice, it should only be used when no other parser matches
        return SupportedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase) ||
               !string.IsNullOrEmpty(fileExtension); // Catch-all for any extension
    }

    /// <summary>
    /// Parses the text content and returns basic chunks.
    /// </summary>
    /// <param name="content">The text file content.</param>
    /// <param name="filePath">The path to the text file.</param>
    /// <returns>A list of chunks representing the parsed text.</returns>
    public async Task<IReadOnlyList<Chunk>> ParseAsync(string content, string filePath)
    {
        var chunks = new List<Chunk>();
        var lines = content.Split('\n');

        if (string.IsNullOrWhiteSpace(content))
        {
            return chunks;
        }

        // For small files, create a single chunk
        if (lines.Length <= 100)
        {
            var chunk = new Chunk(
                Path.GetFileNameWithoutExtension(filePath),
                1,
                lines.Length,
                content,
                ChunkType.Unknown,
                0,
                Language.Unknown);
            chunks.Add(chunk);
            return chunks;
        }

        // For larger files, split into chunks of approximately 50 lines each
        const int chunkSize = 50;
        for (int i = 0; i < lines.Length; i += chunkSize)
        {
            var chunkLines = lines.Skip(i).Take(chunkSize).ToArray();
            var chunkContent = string.Join('\n', chunkLines);

            var chunk = new Chunk(
                $"{Path.GetFileNameWithoutExtension(filePath)}_part_{i / chunkSize + 1}",
                i + 1,
                Math.Min(i + chunkSize, lines.Length),
                chunkContent,
                ChunkType.Unknown,
                0,
                Language.Unknown);

            chunks.Add(chunk);
        }

        return chunks;
    }
}