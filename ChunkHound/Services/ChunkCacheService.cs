using System;
using System.Collections.Generic;
using System.Linq;
using ChunkHound.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChunkHound.Services
{
    /// <summary>
    /// Represents differences between new and existing chunks for smart updates.
    /// </summary>
    public record ChunkDiff
    {
        /// <summary>
        /// Chunks with matching content.
        /// </summary>
        public List<Chunk> Unchanged { get; init; } = new();

        /// <summary>
        /// Chunks with different content.
        /// </summary>
        public List<Chunk> Modified { get; init; } = new();

        /// <summary>
        /// New chunks not in existing set.
        /// </summary>
        public List<Chunk> Added { get; init; } = new();

        /// <summary>
        /// Existing chunks not in new set.
        /// </summary>
        public List<Chunk> Deleted { get; init; } = new();
    }

    /// <summary>
    /// Service for comparing chunks based on direct content comparison to minimize embedding regeneration.
    /// </summary>
    public class ChunkCacheService
    {
        private readonly ILogger<ChunkCacheService> _logger;

        /// <summary>
        /// Logger for diagnostic information.
        /// </summary>
        public ILogger<ChunkCacheService> Logger => _logger;

        /// <summary>
        /// Initializes a new instance of the ChunkCacheService class.
        /// </summary>
        public ChunkCacheService(ILogger<ChunkCacheService>? logger = null)
        {
            _logger = logger ?? NullLogger<ChunkCacheService>.Instance;
        }

        /// <summary>
        /// Compare chunks by normalized content comparison to identify changes.
        /// </summary>
        public ChunkDiff DiffChunks(List<Chunk> newChunks, List<Chunk> existingChunks)
        {
            _logger.LogInformation("Comparing {NewCount} new chunks with {ExistingCount} existing chunks",
                newChunks.Count, existingChunks.Count);

            // Build content lookup for existing chunks using normalized comparison
            var existingByContent = new Dictionary<string, List<Chunk>>();
            foreach (var chunk in existingChunks)
            {
                var normalizedCode = NormalizeCodeForComparison(chunk.Code);
                if (!existingByContent.ContainsKey(normalizedCode))
                {
                    existingByContent[normalizedCode] = new List<Chunk>();
                }
                existingByContent[normalizedCode].Add(chunk);
            }

            // Build content lookup for new chunks
            var newByContent = new Dictionary<string, List<Chunk>>();
            foreach (var chunk in newChunks)
            {
                var normalizedCode = NormalizeCodeForComparison(chunk.Code);
                if (!newByContent.ContainsKey(normalizedCode))
                {
                    newByContent[normalizedCode] = new List<Chunk>();
                }
                newByContent[normalizedCode].Add(chunk);
            }

            // Find intersections and differences using content strings
            var existingContent = new HashSet<string>(existingByContent.Keys);
            var newContent = new HashSet<string>(newByContent.Keys);

            var unchangedContent = existingContent.Intersect(newContent);
            var deletedContent = existingContent.Except(newContent);
            var addedContent = newContent.Except(existingContent);

            // Flatten lists for result
            var unchangedChunks = unchangedContent.SelectMany(content => existingByContent[content]).ToList();
            var deletedChunks = deletedContent.SelectMany(content => existingByContent[content]).ToList();
            var addedChunks = addedContent.SelectMany(content => newByContent[content]).ToList();

            var result = new ChunkDiff
            {
                Unchanged = unchangedChunks,
                Modified = new List<Chunk>(), // Still using add/delete approach for simplicity
                Added = addedChunks,
                Deleted = deletedChunks
            };

            _logger.LogInformation("Diff result: {Unchanged} unchanged, {Added} added, {Deleted} deleted",
                result.Unchanged.Count, result.Added.Count, result.Deleted.Count);

            return result;
        }

        /// <summary>
        /// Normalize code content for comparison while preserving semantic meaning.
        /// </summary>
        private string NormalizeCodeForComparison(string code)
        {
            // Normalize line endings: Windows CRLF (\r\n) and Mac CR (\r) to Unix LF (\n)
            var normalized = code.Replace("\r\n", "\n").Replace("\r", "\n");

            // Strip leading and trailing whitespace for consistent comparison
            normalized = normalized.Trim();

            return normalized;
        }
    }
}