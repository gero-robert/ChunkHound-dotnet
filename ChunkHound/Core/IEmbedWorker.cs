namespace ChunkHound.Core;

/// <summary>
/// Interface for embed worker services.
/// </summary>
public interface IEmbedWorker
{
    /// <summary>
    /// Starts the embed worker processing loop.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the embed worker gracefully.
    /// </summary>
    Task StopAsync();
}