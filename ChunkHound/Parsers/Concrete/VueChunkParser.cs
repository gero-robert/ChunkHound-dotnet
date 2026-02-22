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
                    "template",
                    startLine,
                    endLine,
                    templateContent,
                    ChunkType.Vue,
                    0,
                    Language.Unknown); // Template is HTML-like
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
                    "script",
                    startLine,
                    endLine,
                    scriptContent,
                    ChunkType.Vue,
                    0,
                    Language.JavaScript); // Assume JavaScript, could be TypeScript
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
                        styleNodes.Count > 1 ? $"style_{i + 1}" : "style",
                        startLine,
                        endLine,
                        styleContent,
                        ChunkType.Vue,
                        0,
                        Language.Unknown); // CSS/SCSS
                    chunks.Add(chunk);
                }
            }
        }

        // If no sections found and content is not empty, treat as unknown
        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(content))
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