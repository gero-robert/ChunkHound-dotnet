using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ChunkHound.Core;

namespace ChunkHound.Parsers
{
    /// <summary>
    /// Universal semantic concepts found in all programming languages.
    /// </summary>
    public enum UniversalConcept
    {
        Definition,
        Block,
        Comment,
        Import,
        Structure
    }

    /// <summary>
    /// Language-agnostic representation of semantic code unit.
    /// </summary>
    public record UniversalChunk(
        UniversalConcept Concept,
        string Name,
        string Content,
        int StartLine,
        int EndLine,
        Dictionary<string, object> Metadata,
        string LanguageNodeType
    );

    /// <summary>
    /// Configuration for cAST algorithm.
    /// </summary>
    public class CASTConfig
    {
        public int MaxChunkSize { get; set; } = 1200;
        public int MinChunkSize { get; set; } = 50;
        public float MergeThreshold { get; set; } = 0.8f;
        public bool PreserveStructure { get; set; } = true;
        public bool GreedyMerge { get; set; } = true;
        public int SafeTokenLimit { get; set; } = 6000;
    }

    /// <summary>
    /// Metrics for measuring chunk quality and size.
    /// </summary>
    public class ChunkMetrics
    {
        public int NonWhitespaceChars { get; }
        public int TotalChars { get; }
        public int Lines { get; }
        public int AstDepth { get; }

        public ChunkMetrics(int nonWhitespaceChars, int totalChars, int lines, int astDepth = 0)
        {
            NonWhitespaceChars = nonWhitespaceChars;
            TotalChars = totalChars;
            Lines = lines;
            AstDepth = astDepth;
        }

        public static ChunkMetrics FromContent(string content, int astDepth = 0)
        {
            var nonWs = Regex.Replace(content, @"\s", "").Length;
            var total = content.Length;
            var lines = content.Split('\n').Length;
            return new ChunkMetrics(nonWs, total, lines, astDepth);
        }
    }

    /// <summary>
    /// Handles splitting chunks that exceed size limits with recursive semantic splitting.
    /// </summary>
    public class RecursiveChunkSplitter
    {
        private readonly ILanguageConfigProvider _configProvider;
        private readonly CASTConfig _castConfig;

        public RecursiveChunkSplitter(ILanguageConfigProvider configProvider)
        {
            _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
            _castConfig = new CASTConfig();
        }

        /// <summary>
        /// Splits initial chunks into smaller chunks that fit within size limits.
        /// </summary>
        /// <param name="initialChunks">The initial chunks to split.</param>
        /// <param name="maxChunkSize">Maximum chunk size in non-whitespace characters.</param>
        /// <param name="overlap">Overlap between chunks (not implemented yet).</param>
        /// <returns>List of chunks that fit within size limits.</returns>
        public IReadOnlyList<Chunk> Split(IReadOnlyList<Chunk> initialChunks, int maxChunkSize, int overlap)
        {
            if (initialChunks == null)
                return new List<Chunk>();

            var result = new List<Chunk>();
            foreach (var chunk in initialChunks)
            {
                var universalChunk = ChunkToUniversal(chunk);
                var splitChunks = ValidateAndSplit(universalChunk, maxChunkSize);
                foreach (var uc in splitChunks)
                {
                    result.Add(UniversalToChunk(uc, chunk.FilePath, chunk.FileId, chunk.Language));
                }
            }
            return result;
        }

        private List<UniversalChunk> ValidateAndSplit(UniversalChunk chunk, int maxChunkSize)
        {
            var metrics = ChunkMetrics.FromContent(chunk.Content);
            var estimatedTokens = EstimateTokens(chunk.Content);

            if (metrics.NonWhitespaceChars <= maxChunkSize && estimatedTokens <= _castConfig.SafeTokenLimit)
            {
                return new List<UniversalChunk> { chunk };
            }

            return RecursiveSplit(chunk, maxChunkSize);
        }

        private List<UniversalChunk> RecursiveSplit(UniversalChunk chunk, int maxChunkSize)
        {
            var metrics = ChunkMetrics.FromContent(chunk.Content);
            var estimatedTokens = EstimateTokens(chunk.Content);

            if (metrics.NonWhitespaceChars <= maxChunkSize && estimatedTokens <= _castConfig.SafeTokenLimit)
            {
                return new List<UniversalChunk> { chunk };
            }

            var lines = chunk.Content.Split('\n');
            var (hasVeryLongLines, isRegularCode) = AnalyzeLines(lines);

            if (lines.Length <= 2 || hasVeryLongLines)
            {
                return EmergencySplit(chunk, maxChunkSize);
            }
            else if (isRegularCode)
            {
                return SplitByLinesSimple(chunk, lines, maxChunkSize);
            }
            else
            {
                return SplitByLinesWithFallback(chunk, lines, maxChunkSize);
            }
        }

        private (bool hasVeryLongLines, bool isRegularCode) AnalyzeLines(string[] lines)
        {
            if (lines.Length == 0) return (false, false);

            var lengths = lines.Select(l => l.Length).ToArray();
            var maxLength = lengths.Max();
            var avgLength = lengths.Average();

            var longLineThreshold = _castConfig.MaxChunkSize * 0.2;
            var hasVeryLongLines = maxLength > longLineThreshold;

            var isRegularCode = lengths.Length > 10 && maxLength < 200 && avgLength < 100.0;

            return (hasVeryLongLines, isRegularCode);
        }

        private List<UniversalChunk> SplitByLinesSimple(UniversalChunk chunk, string[] lines, int maxChunkSize)
        {
            if (lines.Length <= 2) return new List<UniversalChunk> { chunk };

            var midPoint = lines.Length / 2;

            var chunk1Content = string.Join("\n", lines.Take(midPoint));
            var chunk2Content = string.Join("\n", lines.Skip(midPoint));

            var chunk1Lines = midPoint;
            var chunk1EndLine = chunk.StartLine + chunk1Lines - 1;
            var chunk2StartLine = chunk1EndLine + 1;

            chunk1EndLine = Math.Max(chunk.StartLine, Math.Min(chunk1EndLine, chunk.EndLine));
            chunk2StartLine = Math.Max(chunk.StartLine, Math.Min(chunk2StartLine, chunk.EndLine));

            var chunk1 = new UniversalChunk(
                chunk.Concept,
                $"{chunk.Name}_part1",
                chunk1Content,
                chunk.StartLine,
                chunk1EndLine,
                new Dictionary<string, object>(chunk.Metadata),
                chunk.LanguageNodeType
            );

            var chunk2 = new UniversalChunk(
                chunk.Concept,
                $"{chunk.Name}_part2",
                chunk2Content,
                chunk2StartLine,
                chunk.EndLine,
                new Dictionary<string, object>(chunk.Metadata),
                chunk.LanguageNodeType
            );

            var result = new List<UniversalChunk>();
            foreach (var subChunk in new[] { chunk1, chunk2 })
            {
                var subMetrics = ChunkMetrics.FromContent(subChunk.Content);
                var subTokens = EstimateTokens(subChunk.Content);

                if (subMetrics.NonWhitespaceChars > maxChunkSize || subTokens > _castConfig.SafeTokenLimit)
                {
                    result.AddRange(RecursiveSplit(subChunk, maxChunkSize));
                }
                else
                {
                    result.Add(subChunk);
                }
            }

            return result;
        }

        private List<UniversalChunk> SplitByLinesWithFallback(UniversalChunk chunk, string[] lines, int maxChunkSize)
        {
            var lineSplitResult = SplitByLinesSimple(chunk, lines, maxChunkSize);

            var validatedResult = new List<UniversalChunk>();
            foreach (var subChunk in lineSplitResult)
            {
                var subMetrics = ChunkMetrics.FromContent(subChunk.Content);
                var subTokens = EstimateTokens(subChunk.Content);

                if (subMetrics.NonWhitespaceChars > maxChunkSize || subTokens > _castConfig.SafeTokenLimit)
                {
                    validatedResult.AddRange(EmergencySplit(subChunk, maxChunkSize));
                }
                else
                {
                    validatedResult.Add(subChunk);
                }
            }

            return validatedResult;
        }

        private List<UniversalChunk> EmergencySplit(UniversalChunk chunk, int maxChunkSize)
        {
            var estimatedTokens = EstimateTokens(chunk.Content);
            double actualRatio = chunk.Content.Length > 0 ? (double)chunk.Content.Length / estimatedTokens : 0;
            var maxCharsFromTokens = (int)(_castConfig.SafeTokenLimit * actualRatio * 0.8);
            var maxChars = Math.Min(maxChunkSize, maxCharsFromTokens);

            var metrics = ChunkMetrics.FromContent(chunk.Content);
            if (metrics.NonWhitespaceChars <= maxChunkSize && chunk.Content.Length <= maxCharsFromTokens)
            {
                return new List<UniversalChunk> { chunk };
            }

            var splitChars = new[] { ";", "}", "{", ",", " " };

            var chunks = new List<UniversalChunk>();
            var remaining = chunk.Content;
            var partNum = 1;
            var totalContentLength = chunk.Content.Length;
            var currentPos = 0;

            while (remaining.Length > 0)
            {
                var remainingMetrics = ChunkMetrics.FromContent(remaining);
                if (remainingMetrics.NonWhitespaceChars <= maxChunkSize)
                {
                    chunks.Add(CreateSplitChunk(chunk, remaining, partNum, currentPos, totalContentLength));
                    break;
                }

                var bestSplit = 0;
                foreach (var splitChar in splitChars)
                {
                    var searchEnd = Math.Min(maxChars, remaining.Length);
                    var pos = remaining.LastIndexOf(splitChar, 0, searchEnd);

                    if (pos > bestSplit)
                    {
                        var testContent = remaining.Substring(0, pos + 1);
                        var testMetrics = ChunkMetrics.FromContent(testContent);
                        if (testMetrics.NonWhitespaceChars <= maxChunkSize)
                        {
                            bestSplit = pos + 1;
                            break;
                        }
                    }
                }

                if (bestSplit == 0)
                {
                    bestSplit = maxChars;
                }

                chunks.Add(CreateSplitChunk(chunk, remaining.Substring(0, bestSplit), partNum, currentPos, totalContentLength));
                remaining = remaining.Substring(bestSplit);
                currentPos += bestSplit;
                partNum++;
            }

            return chunks;
        }

        private UniversalChunk CreateSplitChunk(UniversalChunk original, string content, int partNum, int contentStartPos, int totalContentLength)
        {
            var originalLineSpan = original.EndLine - original.StartLine + 1;

            int startLine, endLine;
            if (totalContentLength > 0 && contentStartPos >= 0)
            {
                var positionRatio = (double)contentStartPos / totalContentLength;
                var contentRatio = (double)content.Length / totalContentLength;

                var lineOffset = (int)(positionRatio * originalLineSpan);
                var lineSpan = Math.Max(1, (int)(contentRatio * originalLineSpan));

                startLine = original.StartLine + lineOffset;
                endLine = Math.Min(original.EndLine, startLine + lineSpan - 1);

                startLine = Math.Min(startLine, original.EndLine);
                endLine = Math.Max(endLine, startLine);
            }
            else
            {
                startLine = original.StartLine;
                endLine = original.EndLine;
            }

            return new UniversalChunk(
                original.Concept,
                $"{original.Name}_part{partNum}",
                content,
                startLine,
                endLine,
                new Dictionary<string, object>(original.Metadata),
                original.LanguageNodeType
            );
        }

        private int EstimateTokens(string content)
        {
            // Simple approximation: roughly 4 characters per token
            return content.Length / 4;
        }

        private static readonly Dictionary<ChunkType, UniversalConcept> ChunkTypeToConcept = new()
        {
            { ChunkType.Function, UniversalConcept.Definition },
            { ChunkType.Class, UniversalConcept.Structure },
            { ChunkType.Interface, UniversalConcept.Structure },
            { ChunkType.Struct, UniversalConcept.Structure },
            { ChunkType.Enum, UniversalConcept.Structure },
            { ChunkType.Module, UniversalConcept.Structure },
            { ChunkType.Documentation, UniversalConcept.Comment },
            { ChunkType.Import, UniversalConcept.Import },
            { ChunkType.Unknown, UniversalConcept.Block }
        };

        private static UniversalChunk ChunkToUniversal(Chunk chunk)
        {
            var concept = ChunkTypeToConcept.GetValueOrDefault(chunk.ChunkType, UniversalConcept.Block);

            return new UniversalChunk(
                concept,
                chunk.Symbol ?? "",
                chunk.Code,
                chunk.StartLine,
                chunk.EndLine,
                chunk.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>(),
                chunk.ChunkType.ToString().ToLower()
            );
        }

        private static Chunk UniversalToChunk(UniversalChunk uc, string? filePath, int fileId, Language language)
        {
            var chunkTypeMap = new Dictionary<UniversalConcept, ChunkType>
            {
                { UniversalConcept.Definition, ChunkType.Function },
                { UniversalConcept.Block, ChunkType.Unknown },
                { UniversalConcept.Comment, ChunkType.Documentation },
                { UniversalConcept.Import, ChunkType.Import },
                { UniversalConcept.Structure, ChunkType.Class }
            };

            var chunkType = chunkTypeMap.GetValueOrDefault(uc.Concept, ChunkType.Unknown);

            return new Chunk(
                uc.Name,
                uc.StartLine,
                uc.EndLine,
                uc.Content,
                chunkType,
                fileId,
                language,
                filePath: filePath,
                metadata: uc.Metadata
            );
        }
    }
}