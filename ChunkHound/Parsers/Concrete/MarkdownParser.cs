using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ChunkHound.Core;
using Markdig;
using Markdig.Syntax;

namespace ChunkHound.Parsers.Concrete;

/// <summary>
/// Parser for Markdown files using Markdig.
/// Handles .md extensions and parses headings and sections.
/// </summary>
public class MarkdownParser : IChunkParser
{
    /// <summary>
    /// Determines if this parser can handle the given file extension.
    /// </summary>
    /// <param name="fileExtension">The file extension to check.</param>
    /// <returns>True if the extension is .md, otherwise false.</returns>
    public bool CanHandle(string fileExtension)
    {
        return fileExtension.Equals(".md", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses the Markdown content and returns chunks for each section.
    /// </summary>
    /// <param name="content">The Markdown file content.</param>
    /// <param name="filePath">The path to the Markdown file.</param>
    /// <returns>A list of chunks representing the parsed Markdown sections.</returns>
    public async Task<IReadOnlyList<Chunk>> ParseAsync(string content, string filePath)
    {
        var chunks = new List<Chunk>();
        var lines = content.Split('\n');

        // Parse the markdown
        var pipeline = new MarkdownPipelineBuilder().Build();
        var document = Markdown.Parse(content, pipeline);

        // Find all headings
        var headings = new List<HeadingBlock>();
        foreach (var node in document.Descendants())
        {
            if (node is HeadingBlock heading)
            {
                headings.Add(heading);
            }
        }

        // Create chunks for each section
        for (int i = 0; i < headings.Count; i++)
        {
            var heading = headings[i];
            var startLine = heading.Line + 1; // Markdig lines are 0-based
            var endLine = (i + 1 < headings.Count) ? headings[i + 1].Line : lines.Length;

            // Extract content from startLine to endLine
            var sectionLines = new List<string>();
            for (int line = startLine; line <= endLine && line <= lines.Length; line++)
            {
                sectionLines.Add(lines[line - 1]); // lines is 0-based
            }

            var sectionContent = string.Join('\n', sectionLines);

            var chunk = new Chunk(
                heading.Inline?.FirstChild?.ToString() ?? $"Heading {heading.Level}",
                startLine,
                endLine,
                sectionContent,
                ChunkType.Documentation, // Markdown sections are documentation
                0,
                Language.Unknown);

            chunks.Add(chunk);
        }

        // If no headings found, create a single chunk for the whole file (if content is not empty)
        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(content))
        {
            var chunk = new Chunk(
                Path.GetFileNameWithoutExtension(filePath),
                1,
                lines.Length,
                content,
                ChunkType.Documentation,
                0,
                Language.Unknown);
            chunks.Add(chunk);
        }

        return chunks;
    }
}