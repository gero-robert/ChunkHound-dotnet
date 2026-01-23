using System;

namespace ChunkHound.Core;

/// <summary>
/// Configuration class for worker components with system-capability-based defaults.
/// Provides configurable parameters for batch processing, retry logic, and timing.
/// </summary>
public class WorkerConfig
{
    /// <summary>
    /// Gets or sets the batch size for processing operations.
    /// Default is based on CPU cores (10 items per core, minimum 1).
    /// </summary>
    public int BatchSize { get; set; } = Math.Max(1, Environment.ProcessorCount * 10);

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed operations.
    /// Default is 3 retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay in milliseconds between busy wait iterations.
    /// Default is 10ms.
    /// </summary>
    public int BusyWaitDelayMs { get; set; } = 10;

    /// <summary>
    /// Gets or sets the initial delay in milliseconds for retry operations.
    /// Default is 100ms.
    /// </summary>
    public int RetryInitialDelayMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum delay in milliseconds for retry operations (exponential backoff cap).
    /// Default is 5000ms (5 seconds).
    /// </summary>
    public int MaxRetryDelayMs { get; set; } = 5000;
}