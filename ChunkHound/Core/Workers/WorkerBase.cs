using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Core.Workers;

/// <summary>
/// Abstract base class for worker components providing common lifecycle management,
/// cancellation handling, logging, and metrics collection.
/// </summary>
public abstract class WorkerBase : IDisposable
{
    protected readonly ILogger _logger;
    protected readonly CancellationTokenSource _cts;
    protected long _itemsProcessed;

    /// <summary>
    /// Initializes a new instance of the WorkerBase class.
    /// </summary>
    /// <param name="logger">Logger instance for the worker.</param>
    protected WorkerBase(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Gets the number of items processed by this worker.
    /// </summary>
    public long ItemsProcessed => _itemsProcessed;

    /// <summary>
    /// Starts the worker asynchronously with the provided cancellation token.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the worker.</param>
    /// <returns>A task representing the worker execution.</returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        _logger.LogInformation("{WorkerType} starting", GetType().Name);

        try
        {
            await ExecuteAsync(linkedCts.Token);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation("{WorkerType} cancelled gracefully", GetType().Name);
            throw new OperationCanceledException("Operation was canceled", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{WorkerType} failed unexpectedly", GetType().Name);
            throw;
        }
        finally
        {
            _logger.LogInformation("{WorkerType} stopping. Processed {ItemsProcessed} items",
                GetType().Name, _itemsProcessed);
        }
    }

    /// <summary>
    /// Stops the worker gracefully.
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping {WorkerType}...", GetType().Name);
        _cts.Cancel();

        // Give some time for graceful shutdown
        await Task.Delay(100);
    }

    /// <summary>
    /// Abstract method to be implemented by derived classes for the main execution logic.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the execution.</returns>
    protected abstract Task ExecuteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Disposes resources used by the worker.
    /// </summary>
    public void Dispose()
    {
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}