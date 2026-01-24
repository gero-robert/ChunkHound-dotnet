namespace ChunkHound.Core
{
    /// <summary>
    /// Status of file processing.
    /// </summary>
    public enum FileProcessingStatus
    {
        Success,
        Error,
        PermanentFailure
    }

    /// <summary>
    /// Result of processing a single file.
    /// </summary>
    public record FileProcessingResult
    {
        /// <summary>
        /// The processing status.
        /// </summary>
        public FileProcessingStatus Status { get; init; }

        /// <summary>
        /// Error message if processing failed.
        /// </summary>
        public string? Error { get; init; }
    }
}