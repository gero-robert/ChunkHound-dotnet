using ChunkHound.Core;
using ChunkHound.Parsers;
using Microsoft.Extensions.Logging;
using System.Text;
using System.IO;
using FileModel = ChunkHound.Core.File;
using System.Collections.Generic;

namespace ChunkHound.Services;

/// <summary>
/// Universal parser that works with all supported languages using Tree-sitter and cAST algorithm.
/// This parser combines Tree-sitter parsing with semantic concept extraction and optimal chunking
/// to create semantically meaningful code chunks for embedding and search.
/// </summary>
public class UniversalParser : IUniversalParser
{
    private readonly ILogger<UniversalParser> _logger;
    private readonly ILanguageConfigProvider _config;
    private readonly Dictionary<Language, IUniversalParser> _parsers;
    private readonly IParserFactory _factory;
    private readonly RecursiveChunkSplitter _splitter;
    private long _totalFilesParsed;
    private long _totalChunksCreated;

    /// <summary>
    /// Initializes a new instance of the UniversalParser class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="config">The language config provider.</param>
    /// <param name="parsers">The parsers dictionary.</param>
    /// <param name="factory">The parser factory.</param>
    /// <param name="splitter">The chunk splitter.</param>
    public UniversalParser(ILogger<UniversalParser> logger, ILanguageConfigProvider config, Dictionary<Language, IUniversalParser> parsers, IParserFactory? factory = null, RecursiveChunkSplitter? splitter = null)
    {
        _logger = logger;
        _config = config ?? new LanguageConfigProvider();
        _parsers = parsers;
        _factory = factory;
        _splitter = splitter;
        _totalFilesParsed = 0;
        _totalChunksCreated = 0;
    }

    /// <summary>
    /// Parses a file into code chunks using the cAST algorithm.
    /// </summary>
    public async Task<List<Chunk>> ParseAsync(FileModel file)
    {
        _logger.LogInformation("Parsing file {FilePath} with {Language} parser", file.Path, file.Language);

        if (!System.IO.File.Exists(file.Path))
        {
            throw new FileNotFoundException($"File not found: {file.Path}");
        }

        try
        {
            if (_parsers.TryGetValue(file.Language, out var p))
            {
                var chunks = await p.ParseAsync(file);
                _logger.LogInformation("Parsed {FilePath}: {ChunkCount} chunks created", file.Path, chunks.Count);
                Interlocked.Increment(ref _totalFilesParsed);
                Interlocked.Add(ref _totalChunksCreated, chunks.Count);
                return chunks;
            }
            else
            {
                // fallback to basic chunking
                var content = await ReadFileContentAsync(file.Path);
                content = NormalizeContent(content);
                var chunks = await ParseWithBasicChunkingAsync(content, file);
                _logger.LogInformation("Parsed {FilePath}: {ChunkCount} chunks created", file.Path, chunks.Count);
                Interlocked.Increment(ref _totalFilesParsed);
                Interlocked.Add(ref _totalChunksCreated, chunks.Count);
                return chunks;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file {FilePath}", file.Path);
            throw;
        }
    }

    /// <summary>
    /// Parses a file by path into chunks.
    /// </summary>
    /// <param name="filePath">The file path to parse.</param>
    /// <returns>List of chunks.</returns>
    public async Task<IReadOnlyList<Chunk>> ParseFileAsync(string filePath)
    {
        if (_factory == null) throw new InvalidOperationException("ParserFactory not provided");
        if (_splitter == null) throw new InvalidOperationException("RecursiveChunkSplitter not provided");

        var content = await System.IO.File.ReadAllTextAsync(filePath);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        
        var parser = _factory.GetParser(ext);
        var initialChunks = await parser.ParseAsync(content, filePath);
        
        var config = _config.GetConfig(Language.Unknown);
        return _splitter.Split(initialChunks, config.MaxChunkSize, 0);
    }

    /// <summary>
    /// Parses content string and extracts semantic chunks using the cAST algorithm.
    /// </summary>
    private async Task<List<Chunk>> ParseContentAsync(string content, FileModel file)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new List<Chunk>();
        }

        // For now, implement basic line-based chunking as Tree-sitter is not available
        // TODO: Replace with actual Tree-sitter integration when available
        return await ParseWithBasicChunkingAsync(content, file);
    }

    /// <summary>
    /// Basic chunking implementation until Tree-sitter is integrated.
    /// This creates chunks based on semantic boundaries and context-aware analysis following cAST principles.
    /// </summary>
    private async Task<List<Chunk>> ParseWithBasicChunkingAsync(string content, FileModel file)
    {
        var chunks = new List<Chunk>();
        var lines = content.Split('\n');
        var currentChunkLines = new List<string>();
        var currentStartLine = 1;
        var lineNum = 1;
        var currentIndentationLevel = 0;
        var config = _config.GetConfig(file.Language);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            var lineIndentation = GetIndentationLevel(line, file.Language);

            // Check if this line should start a new chunk based on semantic boundaries
            var shouldStartNewChunk = ShouldStartNewChunk(trimmedLine, file.Language);

            // Check chunk size limits to prevent oversized chunks
            if (currentChunkLines.Count != 0)
            {
                var currentContent = string.Join("\n", currentChunkLines) + "\n" + line;
                var metrics = CalculateChunkMetrics(currentContent);
                if (metrics.NonWhitespaceChars >= config.MaxChunkSize)
                {
                    shouldStartNewChunk = true;
                }
            }

            if (shouldStartNewChunk && currentChunkLines.Count != 0)
            {
                // Create chunk from accumulated lines
                var chunkContent = string.Join("\n", currentChunkLines);
                var chunk = CreateChunkFromLines(chunkContent, currentStartLine, lineNum - 1, file);
                if (chunk != null)
                {
                    chunks.Add(chunk);
                }

                // Start new chunk
                currentChunkLines.Clear();
                currentStartLine = lineNum;
                currentIndentationLevel = lineIndentation;
            }

            currentChunkLines.Add(line);
            lineNum++;
        }

        // Handle last chunk
        if (currentChunkLines.Count != 0)
        {
            var chunkContent = string.Join("\n", currentChunkLines);
            var chunk = CreateChunkFromLines(chunkContent, currentStartLine, lineNum - 1, file);
            if (chunk != null)
            {
                chunks.Add(chunk);
            }
        }

        // If no chunks were created but we have content, create a single chunk
        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(content))
        {
            var chunk = CreateChunkFromLines(content, 1, lines.Length, file);
            if (chunk != null)
            {
                chunks.Add(chunk);
            }
        }

        // Apply cAST algorithm post-processing
        chunks = await ApplyCastAlgorithmAsync(chunks, content);

        return chunks;
    }

    /// <summary>
    /// Gets the indentation level of a line for context-aware chunking.
    /// </summary>
    private int GetIndentationLevel(string line, Language language)
    {
        var leadingWhitespace = line.Length - line.TrimStart().Length;

        if (language == Language.Python)
        {
            // Python uses spaces/tabs for indentation
            return leadingWhitespace;
        }
        else
        {
            // For other languages, indentation might be less critical for chunking
            return leadingWhitespace / 4; // Convert to indentation levels
        }
    }

    /// <summary>
    /// Determines if a line should start a new chunk based on language-specific patterns.
    /// </summary>
    private bool ShouldStartNewChunk(string trimmedLine, Language language)
    {
        if (string.IsNullOrWhiteSpace(trimmedLine))
            return false;

        var config = _config.GetConfig(language);
        return config.ChunkStartKeywords.Any(keyword => trimmedLine.StartsWith(keyword) || trimmedLine.Contains(keyword));
    }

    /// <summary>
    /// Creates a chunk from a group of lines.
    /// </summary>
    private Chunk? CreateChunkFromLines(string content, int startLine, int endLine, FileModel file)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var config = _config.GetConfig(file.Language);
        var metrics = CalculateChunkMetrics(content);
        if (metrics.NonWhitespaceChars < config.MinChunkSize)
            return null;

        // Determine chunk type based on content analysis
        var chunkType = DetermineChunkType(content, file.Language);
        var symbol = ExtractSymbol(content, file.Language);

        return new Chunk(
            Guid.NewGuid().ToString(),
            startLine,
            endLine,
            content,
            chunkType,
            file.Id ?? 0,
            file.Language
        );
    }

    /// <summary>
    /// Calculates metrics for chunk content.
    /// </summary>
    private ChunkMetrics CalculateChunkMetrics(string content)
    {
        var nonWhitespaceChars = content.Count(c => !char.IsWhiteSpace(c));
        var totalChars = content.Length;
        var lines = content.Split('\n').Length;
        return new ChunkMetrics(nonWhitespaceChars, totalChars, lines);
    }

    /// <summary>
    /// Determines the chunk type based on content analysis.
    /// </summary>
    private ChunkType DetermineChunkType(string content, Language language)
    {
        var firstLine = content.Split('\n').FirstOrDefault()?.Trim() ?? "";

        var config = _config.GetConfig(language);
        foreach (var pattern in config.TypePatterns)
        {
            if (firstLine.StartsWith(pattern.Key) || firstLine.Contains(pattern.Key))
            {
                return pattern.Value;
            }
        }

        return ChunkType.Unknown;
    }

    /// <summary>
    /// Extracts symbol name from content.
    /// </summary>
    private string? ExtractSymbol(string content, Language language)
    {
        var firstLine = content.Split('\n').FirstOrDefault()?.Trim() ?? "";

        var config = _config.GetConfig(language);
        foreach (var pattern in config.SymbolPatterns)
        {
            if (firstLine.StartsWith(pattern.Key) || firstLine.Contains(pattern.Key))
            {
                return ExtractAfterKeyword(firstLine, pattern.Key);
            }
        }

        return null;
    }

    private string? ExtractAfterKeyword(string line, string keyword)
    {
        var index = line.IndexOf(keyword);
        if (index >= 0)
        {
            var afterKeyword = line[(index + keyword.Length)..].Trim();
            var spaceIndex = afterKeyword.IndexOf(' ');
            return spaceIndex >= 0 ? afterKeyword[..spaceIndex] : afterKeyword;
        }
        return null;
    }

    /// <summary>
    /// Applies the cAST algorithm for optimal semantic chunking.
    /// </summary>
    private async Task<List<Chunk>> ApplyCastAlgorithmAsync(List<Chunk> chunks, string content)
    {
        // Placeholder for cAST algorithm implementation
        // TODO: Implement full cAST algorithm when Tree-sitter is available

        // For now, just validate chunk sizes
        var optimizedChunks = new List<Chunk>();

        foreach (var chunk in chunks)
        {
            var validatedChunks = await ValidateAndSplitChunkAsync(chunk, content);
            optimizedChunks.AddRange(validatedChunks);
        }

        return optimizedChunks;
    }

    /// <summary>
    /// Validates chunk size and splits if necessary.
    /// </summary>
    private async Task<List<Chunk>> ValidateAndSplitChunkAsync(Chunk chunk, string content)
    {
        var config = _config.GetConfig(chunk.Language);
        var metrics = CalculateChunkMetrics(chunk.Code);

        if (metrics.NonWhitespaceChars <= config.MaxChunkSize)
        {
            return new List<Chunk> { chunk };
        }

        // Simple splitting for oversized chunks
        return await SplitLargeChunkAsync(chunk, content);
    }

    /// <summary>
    /// Splits a large chunk into smaller ones.
    /// </summary>
    private async Task<List<Chunk>> SplitLargeChunkAsync(Chunk chunk, string content)
    {
        var config = _config.GetConfig(chunk.Language);
        var lines = chunk.Code.Split('\n');
        var result = new List<Chunk>();
        var currentLines = new List<string>();
        var currentStartLine = chunk.StartLine;

        foreach (var line in lines)
        {
            currentLines.Add(line);

            var currentContent = string.Join("\n", currentLines);
            var metrics = CalculateChunkMetrics(currentContent);

            if (metrics.NonWhitespaceChars >= config.MaxChunkSize)
            {
                // Create chunk from current lines (excluding the last one that made it too big)
                if (currentLines.Count > 1)
                {
                    var chunkContent = string.Join("\n", currentLines.Take(currentLines.Count - 1));
                    var newChunk = new Chunk(
                        Guid.NewGuid().ToString(),
                        chunk.FileId,
                        chunkContent,
                        currentStartLine,
                        currentStartLine + currentLines.Count - 2,
                        chunk.Language,
                        ChunkType.Unknown,
                        null,
                        chunk.FilePath,
                        null,
                        null,
                        default,
                        default
                    );
                    result.Add(newChunk);

                    // Reset with the last line
                    currentLines = new List<string> { line };
                    currentStartLine = currentStartLine + currentLines.Count - 1;
                }
            }
        }

        // Add remaining lines
        if (currentLines.Count != 0)
        {
            var chunkContent = string.Join("\n", currentLines);
            var newChunk = new Chunk(
                Guid.NewGuid().ToString(),
                chunk.FileId,
                chunkContent,
                currentStartLine,
                chunk.EndLine,
                chunk.Language,
                ChunkType.Unknown,
                null,
                chunk.FilePath,
                null,
                null,
                default,
                default
            );
            result.Add(newChunk);
        }

        return result;
    }

    /// <summary>
    /// Reads file content with encoding detection and fallback.
    /// </summary>
    private async Task<string> ReadFileContentAsync(string filePath)
    {
        try
        {
            // Try UTF-8 first
            return await System.IO.File.ReadAllTextAsync(filePath, Encoding.UTF8);
        }
        catch (DecoderFallbackException)
        {
            // Fallback to other encodings
            foreach (var encoding in new[] { Encoding.Latin1, Encoding.GetEncoding(1252) })
            {
                try
                {
                    return await System.IO.File.ReadAllTextAsync(filePath, encoding);
                }
                catch (DecoderFallbackException)
                {
                    continue;
                }
            }
            throw new DecoderFallbackException($"Could not decode file {filePath}");
        }
    }

    /// <summary>
    /// Normalizes content for consistent parsing.
    /// </summary>
    private string NormalizeContent(string content)
    {
        // Normalize line endings to LF
        return content.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    /// <summary>
    /// Metrics for measuring chunk quality and size.
    /// </summary>
    private record ChunkMetrics(int NonWhitespaceChars, int TotalChars, int Lines);
}
