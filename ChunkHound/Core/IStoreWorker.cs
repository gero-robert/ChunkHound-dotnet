namespace ChunkHound.Core;

/// <summary>
/// Interface for store worker services.
/// </summary>
public interface IStoreWorker
{
    /// <summary>
    /// Starts the store worker processing loop.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the store worker gracefully.
    /// </summary>
    Task StopAsync();
}