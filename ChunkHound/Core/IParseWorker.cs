namespace ChunkHound.Core;

/// <summary>
/// Interface for parse worker services.
/// </summary>
public interface IParseWorker
{
    /// <summary>
    /// Runs the worker loop to process files.
    /// </summary>
    Task RunAsync(CancellationToken cancellationToken = default);
}