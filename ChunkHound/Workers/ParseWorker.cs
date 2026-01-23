using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core;
using ChunkHound.Core.Workers;
using Microsoft.Extensions.Logging;
using FileModel = ChunkHound.Core.File;

namespace ChunkHound.Workers;

/// <summary>
/// Worker that processes files from the discovery queue, parses them into chunks,
/// and enqueues chunks for embedding. This worker runs continuously until cancelled,
/// processing files in a producer-consumer pattern.
/// </summary>
public class ParseWorker : WorkerBase, IParseWorker
{
    private readonly ConcurrentQueue<string> _filesQueue;
    private readonly ConcurrentQueue<Chunk> _chunksQueue;
    private readonly IUniversalParser _parser;
    private readonly int _workerId;
    private readonly WorkerConfig _config;
    private long _totalChunksGenerated;

    /// <summary>
    /// Initializes a new instance of the ParseWorker class.
    /// </summary>
    public ParseWorker(
        ConcurrentQueue<string> filesQueue,
        ConcurrentQueue<Chunk> chunksQueue,
        IUniversalParser parser,
        int workerId = 0,
        ILogger<ParseWorker>? logger = null,
        WorkerConfig? config = null)
        : base(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ParseWorker>.Instance)
    {
        _filesQueue = filesQueue ?? throw new ArgumentNullException(nameof(filesQueue));
        _chunksQueue = chunksQueue ?? throw new ArgumentNullException(nameof(chunksQueue));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _workerId = workerId;
        _config = config ?? new WorkerConfig();
    }

    /// <summary>
    /// Runs the worker loop, continuously processing files until cancelled.
    /// </summary>
    public Task RunAsync(CancellationToken cancellationToken = default) => StartAsync(cancellationToken);

    /// <summary>
    /// Executes the main processing loop for the parse worker.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var filePath = await DequeueFileAsync(cancellationToken);
                if (filePath == null)
                {
                    // Queue is empty, small delay to prevent busy waiting
                    await Task.Delay(_config.BusyWaitDelayMs, cancellationToken);
                    continue;
                }

                var chunks = await ParseFileAsync(filePath, cancellationToken);
                await EnqueueChunksAsync(chunks, cancellationToken);

                Interlocked.Increment(ref _itemsProcessed);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, break the loop
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ParseWorker {WorkerId} encountered error", _workerId);
                // Continue processing other files despite errors
            }
        }
    }

    /// <summary>
    /// Dequeues the next file path from the files queue.
    /// </summary>
    private async Task<string?> DequeueFileAsync(CancellationToken cancellationToken)
    {
        string? filePath = null;

        // Use Task.Run for the synchronous dequeue operation
        await Task.Run(() =>
        {
            if (_filesQueue.TryDequeue(out var path))
            {
                filePath = path;
            }
        }, cancellationToken);

        if (filePath != null)
        {
            _logger.LogDebug("ParseWorker {WorkerId} dequeued file: {FilePath}", _workerId, filePath);
        }

        return filePath;
    }

    /// <summary>
    /// Parses a file using the universal parser.
    /// </summary>
    private async Task<List<Chunk>> ParseFileAsync(string filePath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("ParseWorker {WorkerId} parsing file: {FilePath}", _workerId, filePath);

        try
        {
            // Detect language from file extension
            var language = DetectLanguageFromPath(filePath);
            if (language == Language.Unknown)
            {
                _logger.LogWarning("ParseWorker {WorkerId} skipping unsupported file: {FilePath}", _workerId, filePath);
                return new List<Chunk>();
            }

            // Create File object for the parser
            var fileInfo = new System.IO.FileInfo(filePath);
            var file = new FileModel(
                path: filePath,
                mtime: new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeSeconds(),
                language: language,
                sizeBytes: fileInfo.Length,
                id: null, // FileId will be assigned by coordinator/database
                contentHash: null
            );

            var chunks = await _parser.ParseAsync(file);

            _logger.LogInformation("ParseWorker {WorkerId} parsed {FilePath}: {ChunkCount} chunks",
                _workerId, filePath, chunks.Count);

            return chunks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ParseWorker {WorkerId} failed to parse file: {FilePath}", _workerId, filePath);
            return new List<Chunk>();
        }
    }

    /// <summary>
    /// Enqueues parsed chunks to the chunks queue.
    /// </summary>
    private async Task EnqueueChunksAsync(List<Chunk> chunks, CancellationToken cancellationToken)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        // Use Task.Run for the synchronous enqueue operations
        await Task.Run(() =>
        {
            foreach (var chunk in chunks)
            {
                _chunksQueue.Enqueue(chunk);
            }
        }, cancellationToken);

        Interlocked.Add(ref _totalChunksGenerated, chunks.Count);

        _logger.LogDebug("ParseWorker {WorkerId} enqueued {ChunkCount} chunks", _workerId, chunks.Count);
    }

    /// <summary>
    /// Detects programming language from file path.
    /// </summary>
    private Language DetectLanguageFromPath(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".cs" => Language.CSharp,
            ".js" => Language.JavaScript,
            ".ts" => Language.TypeScript,
            ".py" => Language.Python,
            ".java" => Language.Java,
            ".go" => Language.Go,
            ".rs" => Language.Rust,
            _ => Language.Unknown
        };
    }
}