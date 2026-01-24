using System.Collections.Generic;

namespace ChunkHound.Core
{
    /// <summary>
    /// Status of indexing operation.
    /// </summary>
    public enum IndexingStatus
    {
        Success,
        NoFiles,
        Error,
        Cancelled
    }

    /// <summary>
    /// Configuration for indexing operations.
    /// </summary>
    public record IndexingConfig
    {
        /// <summary>
        /// Number of parse workers.
        /// </summary>
        public int ParseWorkers { get; init; } = 4;

        /// <summary>
        /// Number of embed workers.
        /// </summary>
        public int EmbedWorkers { get; init; } = 2;

        /// <summary>
        /// Number of store workers.
        /// </summary>
        public int StoreWorkers { get; init; } = 2;

        /// <summary>
        /// Batch size for embedding operations.
        /// </summary>
        public int EmbeddingBatchSize { get; init; } = 100;

        /// <summary>
        /// Batch size for database operations.
        /// </summary>
        public int DatabaseBatchSize { get; init; } = 1000;
    }

    /// <summary>
    /// Progress information for indexing operations.
    /// </summary>
    public record IndexingProgress
    {
        /// <summary>
        /// Total files discovered.
        /// </summary>
        public int TotalFiles { get; init; }

        /// <summary>
        /// Files processed so far.
        /// </summary>
        public int FilesProcessed { get; init; }

        /// <summary>
        /// Total chunks processed.
        /// </summary>
        public int TotalChunks { get; init; }

        /// <summary>
        /// Current phase of indexing.
        /// </summary>
        public string? CurrentPhase { get; init; }

        /// <summary>
        /// Percentage complete (0-100).
        /// </summary>
        public double PercentComplete { get; init; }
    }

    /// <summary>
    /// Result of an indexing operation.
    /// </summary>
    public record IndexingResult
    {
        /// <summary>
        /// The indexing status.
        /// </summary>
        public IndexingStatus Status { get; init; }

        /// <summary>
        /// Error message if indexing failed.
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// Number of files processed.
        /// </summary>
        public int FilesProcessed { get; init; }

        /// <summary>
        /// Total number of chunks stored.
        /// </summary>
        public int TotalChunks { get; init; }

        /// <summary>
        /// Time taken for indexing in milliseconds.
        /// </summary>
        public long DurationMs { get; init; }
    }
}