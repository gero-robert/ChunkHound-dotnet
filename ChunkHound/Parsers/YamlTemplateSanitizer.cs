using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChunkHound.Parsers;

/// <summary>
/// Utility for sanitizing YAML templates (e.g., Helm/Jinja2 placeholders) to make them parseable.
/// </summary>
public static class YamlTemplateSanitizer
{
    private const string PlaceholderBlock = "__CH_TPL_BLOCK__";
    private const string PlaceholderItem = "__CH_TPL_ITEM__";
    private const string PlaceholderMapKey = "__CH_TPL_MAP__";

    private static readonly string[] BlockTemplateHints = {
        "nindent", "toyaml", "toYaml", " indent", "indent(", "tplvalues.render"
    };

    private static readonly string[] CtrlKeywords = {
        "if", "else if", "else", "with", "range", "end", "block", "define"
    };

    private static readonly string[] NonYamlMarkers = { "<?xml", "server {", "%%httpGet" };

    private const int BlockScalarTplThreshold = 12;

    private static readonly Regex TplQuotedKeyRe = new Regex(@"""{{.*?}}""\s*:", RegexOptions.Compiled);
    private static readonly Regex TplQuotedAnyRe = new Regex(@"""[^""\n]*{{[^""\n]*}}[^""\n]*""\s*:", RegexOptions.Compiled);
    private static readonly Regex ComplexKeySingleRe = new Regex(@"^(?<indent>\s*)\?\s*(?<key>.+?):(?<spacing>\s*)(?<rest>.*)$", RegexOptions.Compiled);

    /// <summary>
    /// Sanitizes Helm-style templated YAML content for parsing.
    /// </summary>
    /// <param name="content">The YAML content to sanitize.</param>
    /// <returns>The sanitized content with templates replaced by placeholders.</returns>
    public static string? Sanitize(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        // Pre-skip for obvious non-YAML fragments
        if (NonYamlMarkers.Any(marker => content.Contains(marker)))
            return content;

        // Fast path: no templating markers
        if (!content.Contains("{{") || !content.Contains("}}"))
            return content;

        var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        var sanitized = new List<string>();
        var inBlockScalar = false;
        var blockScalarIndent = 0;
        var inTemplateComment = false;
        var templatedLinesInBlock = 0;

        // Process lines
        for (var idx = 0; idx < lines.Length; idx++)
        {
            var original = lines[idx];
            var (core, newline) = SplitNewline(original);
            var (indentChars, stripped) = SplitIndent(core);

            if (inTemplateComment && !stripped.StartsWith("{{"))
            {
                if (!string.IsNullOrWhiteSpace(stripped))
                {
                    sanitized.Add($"{indentChars}# {stripped}{newline}");
                }
                else
                {
                    sanitized.Add($"{indentChars}{newline}");
                }
                if (stripped.Contains("*/"))
                {
                    inTemplateComment = false;
                }
                continue;
            }

            if (inBlockScalar)
            {
                if (stripped.StartsWith("{{"))
                {
                    var body = ExtractTemplateBody(stripped);
                    var comment = $"{indentChars}  # CH_TPL_BLOCK: {Shorten(body)}{newline}";
                    sanitized.Add(comment);
                    templatedLinesInBlock++;
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(stripped) && indentChars.Length > blockScalarIndent)
                {
                    sanitized.Add(original);
                    continue;
                }
                inBlockScalar = false;
            }

            var templateBody = stripped.StartsWith("{{") ? ExtractTemplateBody(stripped) : null;
            var startCommentBlock = templateBody != null && templateBody.StartsWith("/*");

            var rewritten = RewriteLine(stripped, indentChars, newline);
            sanitized.Add(rewritten);

            if (startCommentBlock && templateBody != null && !templateBody.Contains("*/"))
            {
                inTemplateComment = true;
            }

            if (StartsBlockScalar(rewritten, indentChars))
            {
                inBlockScalar = true;
                blockScalarIndent = indentChars.Length;
            }
        }

        // If too many templated lines in block, return original
        if (templatedLinesInBlock > BlockScalarTplThreshold)
            return content;

        return string.Join("", sanitized);
    }

    private static string RewriteLine(string stripped, string indent, string newline)
    {
        if (string.IsNullOrWhiteSpace(stripped))
            return $"{indent}{newline}";

        if (stripped.StartsWith("{{"))
            return $"{indent}{RewriteTemplateOnlyLine(stripped, newline)}";

        if (stripped.Contains(":"))
        {
            var parts = stripped.Split(new[] { ':' }, 2);
            var before = parts[0];
            var after = parts[1];
            if (after.Contains("{{"))
            {
                var key = before.TrimEnd();
                var templateNote = after.Trim();
                if (BlockTemplateHints.Any(token => templateNote.Contains(token)))
                {
                    var childIndent = indent + "  ";
                    var nl = string.IsNullOrEmpty(newline) ? "\n" : newline;
                    var lines = new List<string>
                    {
                        $"{indent}{key}:",
                        $"{childIndent}{PlaceholderMapKey}: \"{PlaceholderBlock}\""
                    };
                    if (!string.IsNullOrWhiteSpace(templateNote))
                    {
                        lines.Add($"{childIndent}# CH_TPL_INLINE: {templateNote}");
                    }
                    return string.Join(nl, lines) + newline;
                }
                var spacing = key.EndsWith(" ") ? "" : " ";
                var result = $"{indent}{key}:{spacing}\"{PlaceholderBlock}\"{newline}";
                if (!string.IsNullOrWhiteSpace(templateNote))
                {
                    var commentIndent = indent + "  ";
                    result += $"{commentIndent}# CH_TPL_INLINE: {templateNote}{newline}";
                }
                return result;
            }
        }

        var strippedL = stripped.TrimStart();
        if (strippedL.StartsWith("-"))
        {
            var remainder = strippedL[1..].TrimStart();
            if (remainder.StartsWith("{{"))
            {
                return $"{indent}- \"{PlaceholderItem}\"{newline}";
            }
        }

        return $"{indent}{stripped}{newline}";
    }

    private static string RewriteTemplateOnlyLine(string stripped, string newline)
    {
        var body = ExtractTemplateBody(stripped);
        var lowered = body.ToLower();
        if (CtrlKeywords.Any(keyword => lowered.StartsWith(keyword)))
        {
            return $"# CH_TPL_CTRL: {body}{newline}";
        }

        if (body.Contains(":="))
        {
            return $"# CH_TPL_SET: {body}{newline}";
        }

        return $"# CH_TPL_INCLUDE: {body}{newline}";
    }

    private static string ExtractTemplateBody(string stripped)
    {
        var body = stripped[2..];
        if (body.StartsWith("-"))
            body = body[1..];
        body = body.Trim();
        if (body.EndsWith("}}"))
            body = body[..^2].TrimEnd();
        else if (body.EndsWith("-}}"))
            body = body[..^3].TrimEnd();
        return body;
    }

    private static string Shorten(string snippet, int limit = 60)
    {
        snippet = snippet.Trim();
        if (snippet.Length <= limit)
            return snippet;
        return snippet[..(limit - 3)] + "...";
    }

    private static (string, string) SplitIndent(string text)
    {
        var idx = 0;
        while (idx < text.Length && (text[idx] == ' ' || text[idx] == '\t'))
            idx++;
        return (text[..idx], text[idx..]);
    }

    private static (string, string) SplitNewline(string line)
    {
        if (line.EndsWith("\r\n"))
            return (line[..^2], "\r\n");
        if (line.EndsWith("\n"))
            return (line[..^1], "\n");
        if (line.EndsWith("\r"))
            return (line[..^1], "\r");
        return (line, "");
    }

    private static bool StartsBlockScalar(string line, string indent)
    {
        var stripped = line[indent.Length..];
        var colonIdx = stripped.IndexOf(':');
        if (colonIdx == -1)
            return false;
        var suffix = stripped[(colonIdx + 1)..].Trim();
        return suffix.StartsWith("|") || suffix.StartsWith(">");
    }
}