using System;
using System.Collections.Generic;

namespace ChunkHound.Core;

/// <summary>
/// Represents embedding data for storage.
/// </summary>
public record EmbeddingData
{
    /// <summary>
    /// The chunk ID.
    /// </summary>
    public long ChunkId { get; init; }

    /// <summary>
    /// The provider name.
    /// </summary>
    public string Provider { get; init; }

    /// <summary>
    /// The model name.
    /// </summary>
    public string Model { get; init; }

    /// <summary>
    /// The dimensions of the embedding.
    /// </summary>
    public int Dimensions { get; init; }

    /// <summary>
    /// The embedding vector.
    /// </summary>
    public List<float> Embedding { get; init; }

    /// <summary>
    /// The status of the embedding.
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Initializes a new EmbeddingData.
    /// </summary>
    public EmbeddingData(long chunkId, string provider, string model, int dimensions, List<float> embedding, string status)
    {
        ChunkId = chunkId;
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Dimensions = dimensions;
        Embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
        Status = status ?? throw new ArgumentNullException(nameof(status));
    }
}

/// <summary>
/// Result of embedding generation.
/// </summary>
public record EmbeddingResult
{
    /// <summary>
    /// Number of embeddings successfully generated.
    /// </summary>
    public int TotalGenerated { get; init; }

    /// <summary>
    /// Total number of chunks processed.
    /// </summary>
    public int TotalProcessed { get; init; }

    /// <summary>
    /// Number of successful chunks.
    /// </summary>
    public int SuccessfulChunks { get; init; }

    /// <summary>
    /// Number of failed chunks.
    /// </summary>
    public int FailedChunks { get; init; }

    /// <summary>
    /// Number of permanent failures.
    /// </summary>
    public int PermanentFailures { get; init; }

    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryAttempts { get; init; }

    /// <summary>
    /// Error statistics.
    /// </summary>
    public Dictionary<string, int> ErrorStats { get; init; } = new();

    /// <summary>
    /// Error samples.
    /// </summary>
    public Dictionary<string, List<string>> ErrorSamples { get; init; } = new();

    /// <summary>
    /// Error message if any.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Result of regeneration.
/// </summary>
public record RegenerateResult
{
    /// <summary>
    /// Status of the operation.
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Number of embeddings regenerated.
    /// </summary>
    public int? Regenerated { get; init; }

    /// <summary>
    /// Total number of chunks.
    /// </summary>
    public int? TotalChunks { get; init; }

    /// <summary>
    /// Provider name.
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// Model name.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Error message if any.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Progress information for embedding operations.
/// </summary>
public record EmbeddingProgressInfo
{
    /// <summary>
    /// Current progress (0-1).
    /// </summary>
    public double Progress { get; init; }

    /// <summary>
    /// Current message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Number of chunks processed.
    /// </summary>
    public int Processed { get; init; }

    /// <summary>
    /// Total number of chunks.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Initializes a new EmbeddingProgressInfo.
    /// </summary>
    public EmbeddingProgressInfo(double progress, string message, int processed, int total)
    {
        Progress = progress;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Processed = processed;
        Total = total;
    }
}

/// <summary>
/// Classification of embedding errors.
/// </summary>
public enum EmbeddingErrorClassification
{
    /// <summary>
    /// Transient error that can be retried.
    /// </summary>
    Transient,

    /// <summary>
    /// Permanent error that cannot be retried.
    /// </summary>
    Permanent
}

/// <summary>
/// Result of a batch processing.
/// </summary>
public class BatchResult
{
    /// <summary>
    /// Batch number.
    /// </summary>
    public int BatchNum { get; set; }

    /// <summary>
    /// Successful chunks with embeddings.
    /// </summary>
    public List<(Chunk Chunk, List<float> Embedding)> SuccessfulChunks { get; set; } = new();

    /// <summary>
    /// Failed chunks with error and classification.
    /// </summary>
    public List<(Chunk Chunk, string Error, EmbeddingErrorClassification Classification)> FailedChunks { get; set; } = new();

    /// <summary>
    /// Error statistics.
    /// </summary>
    public Dictionary<string, int> ErrorStats { get; set; } = new();

    /// <summary>
    /// Error samples.
    /// </summary>
    public Dictionary<string, List<string>> ErrorSamples { get; set; } = new();

    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryAttempts { get; set; }
}