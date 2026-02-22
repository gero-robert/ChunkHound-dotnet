using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ChunkHound.Core;
using YamlDotNet.Serialization;

namespace ChunkHound.Parsers.Concrete;

/// <summary>
/// Parser for YAML files using YamlDotNet.
/// Handles .yaml and .yml extensions.
/// </summary>
public class RapidYamlParser : IChunkParser
{
    /// <summary>
    /// Determines if this parser can handle the given file extension.
    /// </summary>
    /// <param name="fileExtension">The file extension to check.</param>
    /// <returns>True if the extension is .yaml or .yml, otherwise false.</returns>
    public bool CanHandle(string fileExtension)
    {
        return fileExtension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses the YAML content and returns semantic chunks.
    /// </summary>
    /// <param name="content">The YAML file content.</param>
    /// <param name="filePath">The path to the YAML file.</param>
    /// <returns>A list of chunks representing the parsed YAML.</returns>
    public async Task<IReadOnlyList<Chunk>> ParseAsync(string content, string filePath)
    {
        // For now, create a single chunk for the entire YAML file
        // In a full implementation, this would parse the YAML structure and create chunks for each top-level key
        var chunks = new List<Chunk>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return chunks;
        }

        try
        {
            // Sanitize templates first
            var templateSanitizedContent = YamlTemplateSanitizer.Sanitize(content);

            // Basic validation that it's valid YAML
            var deserializer = new DeserializerBuilder().Build();
            var yamlObject = deserializer.Deserialize<object>(templateSanitizedContent);

            // Sanitize the content (remove comments, etc.)
            var sanitizedContent = SanitizeYamlContent(templateSanitizedContent);

            var chunk = new Chunk(
                Path.GetFileNameWithoutExtension(filePath),
                1,
                content.Split('\n').Length,
                sanitizedContent,
                ChunkType.YamlTemplate,
                0, // Will be set by caller
                Language.Yaml);

            chunks.Add(chunk);
        }
        catch (Exception)
        {
            // If parsing fails, still create a chunk but mark as unknown
            var chunk = new Chunk(
                Path.GetFileNameWithoutExtension(filePath),
                1,
                content.Split('\n').Length,
                content,
                ChunkType.Unknown,
                0,
                Language.Yaml);

            chunks.Add(chunk);
        }

        return chunks;
    }

    private static string SanitizeYamlContent(string content)
    {
        // Basic sanitization: remove comments
        var lines = content.Split('\n');
        var sanitizedLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith('#'))
            {
                sanitizedLines.Add(line);
            }
        }

        return string.Join('\n', sanitizedLines);
    }
}