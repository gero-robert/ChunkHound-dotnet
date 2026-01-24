using System.Collections.Generic;

namespace ChunkHound.Core
{
    /// <summary>
    /// Result of batch processing operations.
    /// </summary>
    public record BatchProcessingResult
    {
        /// <summary>
        /// Total files attempted for processing.
        /// </summary>
        public int TotalAttempted { get; init; }

        /// <summary>
        /// Total files successfully processed.
        /// </summary>
        public int TotalProcessed { get; init; }

        /// <summary>
        /// Total files that failed processing.
        /// </summary>
        public int TotalFailed { get; init; }

        /// <summary>
        /// Total permanent failures.
        /// </summary>
        public int TotalPermanentFailures { get; init; }

        /// <summary>
        /// Number of batches processed.
        /// </summary>
        public int BatchCount { get; init; }

        /// <summary>
        /// Error statistics by type.
        /// </summary>
        public Dictionary<string, int> ErrorStats { get; init; } = new();
    }
}