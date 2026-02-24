using System.Threading;
using System.Threading.Tasks;
using ChunkHound.Core;
using ChunkHound.Core.Workers;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Workers;

/// <summary>
/// Worker that processes FileProcessingResult from the discovery queue, parses them into chunks,
/// and enqueues chunks for embedding. This worker runs continuously until cancelled,
/// processing files in a producer-consumer pattern.
/// </summary>
public class ParseWorker : PipelineWorker<FileProcessingResult, Chunk>
{
    private readonly Dictionary<Language, IUniversalParser> _parsers;

    /// <summary>
    /// Initializes a new instance of the ParseWorker class.
    /// </summary>
    public ParseWorker(
        Dictionary<Language, IUniversalParser> parsers,
        ILogger<ParseWorker>? logger = null,
        WorkerConfig? config = null)
        : base(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ParseWorker>.Instance, config ?? new WorkerConfig())
    {
        _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));
    }

    /// <summary>
    /// Processes a batch of FileProcessingResult by parsing them into chunks.
    /// </summary>
    protected override async Task<IReadOnlyList<Chunk>> ProcessBatchAsync(IReadOnlyList<FileProcessingResult> batch, CancellationToken ct)
    {
        var allChunks = new List<Chunk>();
        foreach (var result in batch)
        {
            if (result.File != null && _parsers.TryGetValue(result.File.Language, out var parser))
            {
                var chunks = await parser.ParseAsync(result.File);
                allChunks.AddRange(chunks);
            }
        }
        return allChunks;
    }

}