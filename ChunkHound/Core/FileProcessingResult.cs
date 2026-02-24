namespace ChunkHound.Core
{
    /// <summary>
    /// Status of file processing.
    /// </summary>
    public enum FileProcessingStatus
    {
        Success,
        Error,
        PermanentFailure,
        UnsupportedLanguage,
        NoParser,
        NoChunks
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

        /// <summary>
        /// Number of chunks processed from the file.
        /// </summary>
        public int ChunksProcessed { get; init; }

        /// <summary>
        /// Number of chunks stored in the database.
        /// </summary>
        public int ChunksStored { get; init; }

        /// <summary>
        /// The file ID assigned during processing.
        /// </summary>
        public int FileId { get; init; }

        /// <summary>
        /// The chunks extracted from the file.
        /// </summary>
        public List<Chunk> Chunks { get; init; } = new();

        /// <summary>
        /// The file being processed.
        /// </summary>
        public File File { get; init; }
    }
}