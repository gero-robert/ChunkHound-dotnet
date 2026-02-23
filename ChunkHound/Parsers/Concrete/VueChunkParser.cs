using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ChunkHound.Core;
using HtmlAgilityPack;

namespace ChunkHound.Parsers.Concrete;

/// <summary>
/// Parser for Vue single-file components.
/// Handles .vue extensions and extracts script, template, and style sections.
/// </summary>
public class VueChunkParser : IChunkParser
{
    /// <summary>
    /// Determines if this parser can handle the given file extension.
    /// </summary>
    /// <param name="fileExtension">The file extension to check.</param>
    /// <returns>True if the extension is .vue, otherwise false.</returns>
    public bool CanHandle(string fileExtension)
    {
        return fileExtension.Equals(".vue", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses the Vue SFC content and returns chunks for each section.
    /// </summary>
    /// <param name="content">The Vue file content.</param>
    /// <param name="filePath">The path to the Vue file.</param>
    /// <returns>A list of chunks representing the parsed Vue sections.</returns>
    public async Task<IReadOnlyList<Chunk>> ParseAsync(string content, string filePath)
    {
        var chunks = new List<Chunk>();
        var lines = content.Split('\n');

        // Parse the Vue file as HTML
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(content);

        // Extract template section
        var templateNode = htmlDoc.DocumentNode.SelectSingleNode("//template");
        if (templateNode != null)
        {
            var templateContent = templateNode.InnerHtml.Trim();
            if (!string.IsNullOrEmpty(templateContent))
            {
                var startLine = GetStartLine(templateNode, content);
                var endLine = startLine + templateContent.Split('\n').Length - 1;

                var chunk = new Chunk(
                    Guid.NewGuid().ToString(),
                    0,
                    templateContent,
                    startLine,
                    endLine,
                    Language.Unknown,
                    ChunkType.Vue,
                    "template",
                    null,
                    new Dictionary<string, object> { ["chunkType"] = ChunkType.Vue, ["language"] = Language.Unknown },
                    null,
                    default,
                    default);
                chunks.Add(chunk);
            }
        }

        // Extract script section
        var scriptNode = htmlDoc.DocumentNode.SelectSingleNode("//script");
        if (scriptNode != null)
        {
            var scriptContent = scriptNode.InnerText.Trim();
            if (!string.IsNullOrEmpty(scriptContent))
            {
                var startLine = GetStartLine(scriptNode, content);
                var endLine = startLine + scriptContent.Split('\n').Length - 1;

                var chunk = new Chunk(
                    Guid.NewGuid().ToString(),
                    0,
                    scriptContent,
                    startLine,
                    endLine,
                    Language.JavaScript,
                    ChunkType.Vue,
                    "script",
                    null,
                    new Dictionary<string, object> { ["chunkType"] = ChunkType.Vue },
                    null,
                    default,
                    default);
                chunks.Add(chunk);
            }
        }

        // Extract style sections
        var styleNodes = htmlDoc.DocumentNode.SelectNodes("//style");
        if (styleNodes != null)
        {
            for (int i = 0; i < styleNodes.Count; i++)
            {
                var styleNode = styleNodes[i];
                var styleContent = styleNode.InnerText.Trim();
                if (!string.IsNullOrEmpty(styleContent))
                {
                    var startLine = GetStartLine(styleNode, content);
                    var endLine = startLine + styleContent.Split('\n').Length - 1;

                    var chunk = new Chunk(
                        Guid.NewGuid().ToString(),
                        0,
                        styleContent,
                        startLine,
                        endLine,
                        Language.Unknown,
                        ChunkType.Vue,
                        styleNodes.Count > 1 ? $"style_{i + 1}" : "style",
                        null,
                        new Dictionary<string, object> { ["chunkType"] = ChunkType.Vue },
                        null,
                        default,
                        default);
                    chunks.Add(chunk);
                }
            }
        }

        // If no sections found and content is not empty, treat as unknown
        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(content))
        {
            var chunk = new Chunk(
                Guid.NewGuid().ToString(),
                0,
                content,
                1,
                lines.Length,
                Language.Unknown,
                ChunkType.Unknown,
                Path.GetFileNameWithoutExtension(filePath),
                null,
                new Dictionary<string, object> { ["chunkType"] = ChunkType.Unknown },
                null,
                default,
                default);
            chunks.Add(chunk);
        }

        return chunks;
    }

    private static int GetStartLine(HtmlNode node, string content)
    {
        // Simple approximation: find the line where the node starts
        var outerHtml = node.OuterHtml;
        var index = content.IndexOf(outerHtml);
        if (index == -1) return 1;

        var before = content.Substring(0, index);
        return before.Split('\n').Length;
    }
}