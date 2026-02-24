using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
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
    private readonly Channel<string> _filesChannel;
    private readonly Channel<Chunk> _chunksChannel;
    private readonly IUniversalParser _parser;
    private readonly int _workerId;
    private readonly WorkerConfig _config;
    private long _totalChunksGenerated;

    /// <summary>
    /// Initializes a new instance of the ParseWorker class.
    /// </summary>
    public ParseWorker(
        Channel<string> filesChannel,
        Channel<Chunk> chunksChannel,
        IUniversalParser parser,
        int workerId = 0,
        ILogger<ParseWorker>? logger = null,
        WorkerConfig? config = null)
        : base(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ParseWorker>.Instance)
    {
        _filesChannel = filesChannel ?? throw new ArgumentNullException(nameof(filesChannel));
        _chunksChannel = chunksChannel ?? throw new ArgumentNullException(nameof(chunksChannel));
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
        var reader = _filesChannel.Reader;
        var writer = _chunksChannel.Writer;
        try
        {
            while (await reader.WaitToReadAsync(cancellationToken))
            {
                var batch = new List<string>();
                while (batch.Count < 1 && reader.TryRead(out var item))
                {
                    batch.Add(item);
                }
                if (batch.Count > 0)
                {
                    var processed = await ProcessBatchAsync(batch, cancellationToken);
                    foreach (var chunk in processed)
                    {
                        await writer.WriteAsync(chunk, cancellationToken);
                    }
                    Interlocked.Add(ref _itemsProcessed, batch.Count);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not ObjectDisposedException)
        {
            _logger.LogError(ex, "{WorkerName} failed - continuing", nameof(ParseWorker));
            writer.TryComplete(ex);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    /// <summary>
    /// Processes a batch of files by parsing them into chunks.
    /// </summary>
    private async Task<List<Chunk>> ProcessBatchAsync(List<string> batch, CancellationToken cancellationToken)
    {
        var allChunks = new List<Chunk>();
        foreach (var filePath in batch)
        {
            try
            {
                var chunks = await ParseFileAsync(filePath, cancellationToken);
                allChunks.AddRange(chunks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ParseWorker {WorkerId} failed to parse file: {FilePath}, skipping", _workerId, filePath);
            }
        }
        return allChunks;
    }

    /// <summary>
    /// Parses a file using the universal parser.
    /// </summary>


    /// <summary>
    /// Parses a file using the universal parser.
    /// </summary>
    private async Task<List<Chunk>> ParseFileAsync(string filePath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("ParseWorker {WorkerId} parsing file: {FilePath}", _workerId, filePath);

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