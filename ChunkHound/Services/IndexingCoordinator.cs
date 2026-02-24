using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ChunkHound.Core;
using ChunkHound.Workers;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileModel = ChunkHound.Core.File;

namespace ChunkHound.Services;

/// <summary>
/// Orchestrates file indexing workflows with parsing, chunking, and embeddings.
/// This service coordinates the streamlined indexing process: discovery→parse→embed→store
/// where all chunks get embedded during initial indexing without separate missing embedding queries.
/// </summary>
public class IndexingCoordinator : IIndexingCoordinator
{
    private readonly IDatabaseProvider _databaseProvider;
    private readonly IEmbeddingProvider? _embeddingProvider;
    private readonly Dictionary<Language, IUniversalParser> _languageParsers;
    private readonly ChunkCacheService _chunkCacheService;
    private readonly string _baseDirectory;
    private readonly IndexingConfig? _config;
    private readonly ILogger<IndexingCoordinator> _logger;
    private readonly IProgress<IndexingProgress>? _progress;
    private readonly IndexingOptions _options;
    private readonly string _tempPath;

    // Channels for pipeline
    private readonly Channel<FileProcessingResult> _fileResultsChannel;
    private readonly Channel<Chunk> _chunksChannel;
    private readonly Channel<Chunk> _embeddedChunksChannel;

    // Synchronization primitives
    private readonly ReaderWriterLockSlim _dbLock;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks;
    private readonly CancellationTokenSource _cancellationTokenSource;

    // Statistics counters
    private int _filesProcessed;
    private int _chunksStored;

    /// <summary>
    /// Initializes a new instance of the IndexingCoordinator class.
    /// </summary>
    /// <param name="databaseProvider">The database provider.</param>
    /// <param name="baseDirectory">The base directory for indexing.</param>
    /// <param name="embeddingProvider">The embedding provider.</param>
    /// <param name="languageParsers">The language parsers.</param>
    /// <param name="chunkCacheService">The chunk cache service.</param>
    /// <param name="config">The indexing config.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="progress">The progress reporter.</param>
    /// <param name="options">The indexing options.</param>
    public IndexingCoordinator(
        IDatabaseProvider databaseProvider,
        string baseDirectory,
        IEmbeddingProvider? embeddingProvider = null,
        Dictionary<Language, IUniversalParser>? languageParsers = null,
        ChunkCacheService? chunkCacheService = null,
        IndexingConfig? config = null,
        ILogger<IndexingCoordinator>? logger = null,
        IProgress<IndexingProgress>? progress = null,
        IOptions<IndexingOptions> options = null)
    {
        _databaseProvider = databaseProvider ?? throw new ArgumentNullException(nameof(databaseProvider));
        _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        _embeddingProvider = embeddingProvider;
        _languageParsers = languageParsers ?? new Dictionary<Language, IUniversalParser>();
        _chunkCacheService = chunkCacheService ?? new ChunkCacheService();
        _config = config;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<IndexingCoordinator>.Instance;
        _progress = progress;
        _options = options?.Value ?? new IndexingOptions();
        _tempPath = _options.TempPath;

        // Initialize channels
        _fileResultsChannel = Channel.CreateUnbounded<FileProcessingResult>();
        _chunksChannel = Channel.CreateUnbounded<Chunk>();
        _embeddedChunksChannel = Channel.CreateUnbounded<Chunk>();

        // Initialize synchronization primitives
        _dbLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Runs the indexing process for the specified directory.
    /// </summary>
    /// <param name="directoryPath">The directory to index.</param>
    public async Task IndexAsync(string directoryPath)
    {
        await ProcessDirectoryIterativeAsync(directoryPath, default);
    }

    /// <summary>
    /// Processes all supported files in a directory with batch optimization and consistency checks.
    /// </summary>
    public async Task<IndexingResult> ProcessDirectoryAsync(
        string directory,
        List<string>? patterns = null,
        List<string>? excludePatterns = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting directory processing for {Directory}", directory);

            // Phase 1: Discovery
            var files = await DiscoverFilesAsync(directory, patterns, excludePatterns, cancellationToken);

            if (files.Count == 0)
            {
                return new IndexingResult { Status = IndexingStatus.NoFiles };
            }

            // Phase 2: Change detection and filtering
            var filesToProcess = await FilterChangedFilesAsync(files, cancellationToken);

            // Phase 3: Parallel processing pipeline
            var result = await ProcessFilesPipelineAsync(filesToProcess, cancellationToken);

            _logger.LogInformation("Directory processing completed: {FilesProcessed} files, {ChunksStored} chunks",
                result.FilesProcessed, result.TotalChunks);

            stopwatch.Stop();
            return result with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process directory {Directory}", directory);
            return new IndexingResult { Status = IndexingStatus.Error, Error = ex.Message };
        }
    }

    /// <summary>
    /// Processes a single file through the parsing and chunking pipeline.
    /// </summary>
    public async Task<FileProcessingResult> ProcessFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing single file {FilePath}", filePath);

        // File-level locking to prevent concurrent processing
        var fileLock = await GetFileLockAsync(filePath);
        await fileLock.WaitAsync(cancellationToken);

        try
        {
            // Detect language
            var language = await DetectFileLanguageAsync(filePath);
            if (language == Language.Unknown)
            {
                return new FileProcessingResult { Status = FileProcessingStatus.UnsupportedLanguage };
            }

            // Get parser
            if (!_languageParsers.TryGetValue(language, out var parser))
            {
                return new FileProcessingResult { Status = FileProcessingStatus.NoParser };
            }

            // Get or create fileId for parsing
            var relativePath = Path.GetRelativePath(_baseDirectory, filePath);
            var existingFile = await _databaseProvider.GetFileByPathAsync(relativePath, cancellationToken);
            var fileId = existingFile?.Id ?? 0;

            // Create file model for parsing
            var fileInfo = new FileInfo(filePath);
            var file = new FileModel(
                path: relativePath,
                mtime: new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeSeconds(),
                language: language,
                sizeBytes: fileInfo.Length,
                id: fileId
            );

            // Parse file
            var chunks = await parser.ParseAsync(file);
            if (chunks.Count == 0)
            {
                return new FileProcessingResult { Status = FileProcessingStatus.NoChunks };
            }

            // Store chunks with diffing
            var storeResult = await StoreParsedChunksAsync(filePath, chunks, cancellationToken);

            return new FileProcessingResult
            {
                Status = FileProcessingStatus.Success,
                ChunksProcessed = chunks.Count,
                ChunksStored = storeResult.ChunksStored,
                FileId = storeResult.FileId,
                Chunks = chunks
            };
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <summary>
    /// Processes files using a producer-consumer pipeline with async workers.
    /// </summary>
    private async Task<IndexingResult> ProcessFilesPipelineAsync(
        List<string> files,
        CancellationToken cancellationToken)
    {
        // Create workers
        var parseWorker = new ParseWorker(_languageParsers, null);
        var embedWorker = new EmbedWorker(_embeddingProvider, null);
        var storeWorker = new StoreWorker(_databaseProvider, null);

        // Run workers
        var parseTask = parseWorker.RunAsync(_fileResultsChannel.Reader, _chunksChannel.Writer, cancellationToken);
        var embedTask = embedWorker.RunAsync(_chunksChannel.Reader, _embeddedChunksChannel.Writer, cancellationToken);
        var dummyChannel = Channel.CreateUnbounded<object>();
        var storeTask = storeWorker.RunAsync(_embeddedChunksChannel.Reader, dummyChannel.Writer, cancellationToken);

        // Producer
        foreach (var file in files)
        {
            var fileInfo = new System.IO.FileInfo(file);
            var language = DetectLanguageFromPath(file);
            if (language == Language.Unknown) continue;
            var fileModel = new FileModel(
                path: file,
                mtime: new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeSeconds(),
                language: language,
                sizeBytes: fileInfo.Length,
                id: null,
                contentHash: null
            );
            var result = new FileProcessingResult { File = fileModel };
            await _fileResultsChannel.Writer.WriteAsync(result, cancellationToken);
        }
        _fileResultsChannel.Writer.Complete();
        _logger.LogInformation("Parse input completed");

        // Await
        await Task.WhenAll(parseTask, embedTask, storeTask);

        // Collect results
        return await CollectPipelineResultsAsync();
    }

    /// <summary>
    /// Worker that reads files from channel, parses them, and writes chunks to channel.
    /// </summary>
    private async Task ParseWorkerAsync(CancellationToken cancellationToken)
    {
        await foreach (var filePath in _filesChannel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                var chunks = await ParseFileAsync(filePath, cancellationToken);
                foreach (var chunk in chunks)
                {
                    await _chunksChannel.Writer.WriteAsync(chunk, cancellationToken);
                }
                Interlocked.Increment(ref _filesProcessed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Parse worker failed for file {FilePath}", filePath);
            }
        }
    }

    /// <summary>
    /// Worker that reads chunks from channel, generates embeddings, and writes embed chunks to channel.
    /// </summary>
    private async Task EmbedWorkerAsync(CancellationToken cancellationToken)
    {
        var batch = new List<Chunk>();
        var batchSize = _config?.EmbeddingBatchSize ?? 100;

        try
        {
            while (await _chunksChannel.Reader.WaitToReadAsync(cancellationToken))
            {
                // Collect available chunks up to batch size
                while (batch.Count < batchSize && _chunksChannel.Reader.TryRead(out var chunk))
                {
                    batch.Add(chunk);
                }

                if (batch.Count != 0)
                {
                    try
                    {
                        var texts = batch.Select(c => c.Content).ToList();
                        var embeddings = await _embeddingProvider!.EmbedAsync(texts, cancellationToken);

                        for (var i = 0; i < batch.Count; i++)
                        {
                            var embedChunk = new EmbedChunk(
                                batch[i],
                                embeddings[i],
                                _embeddingProvider.ProviderName,
                                _embeddingProvider.ModelName);
                            await _embedChunksChannel.Writer.WriteAsync(embedChunk, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Embed worker failed for batch of {Count} chunks", batch.Count);
                    }
                    finally
                    {
                        batch.Clear();
                    }
                }
            }
        }
        catch (ChannelClosedException)
        {
            // Channel completed, exit
        }
    }

    /// <summary>
    /// Worker that reads chunks or embed chunks from channel and stores them in the database.
    /// </summary>
    private async Task StoreWorkerAsync(CancellationToken cancellationToken)
    {
        var batchSize = _config?.DatabaseBatchSize ?? 1000;

        try
        {
            if (_embeddingProvider == null)
            {
                // Read chunks directly
                var chunksBatch = new List<Chunk>();
                while (await _chunksChannel.Reader.WaitToReadAsync(cancellationToken))
                {
                    // Collect available chunks up to batch size
                    while (chunksBatch.Count < batchSize && _chunksChannel.Reader.TryRead(out var chunk))
                    {
                        chunksBatch.Add(chunk);
                    }

                    if (chunksBatch.Count != 0)
                    {
                        try
                        {
                            await ExecuteWithLockAsync(async () =>
                            {
                                var chunkIds = await _databaseProvider.InsertChunksBatchAsync(chunksBatch, cancellationToken);

                                Interlocked.Add(ref _chunksStored, chunkIds.Count);
                                return chunkIds.Count;
                            }, writeLock: true, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Store worker failed for batch of {Count} chunks", chunksBatch.Count);
                        }
                        finally
                        {
                            chunksBatch.Clear();
                        }
                    }
                }

                // Process any remaining chunks in the batch
                if (chunksBatch.Count != 0)
                {
                    try
                    {
                        await ExecuteWithLockAsync(async () =>
                        {
                            var chunkIds = await _databaseProvider.InsertChunksBatchAsync(chunksBatch, cancellationToken);

                            Interlocked.Add(ref _chunksStored, chunkIds.Count);
                            return chunkIds.Count;
                        }, writeLock: true, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Store worker failed for remaining batch of {Count} chunks", chunksBatch.Count);
                    }
                    finally
                    {
                        chunksBatch.Clear();
                    }
                }
            }
            else
            {
                // Read embed chunks
                var batch = new List<EmbedChunk>();
                while (await _embedChunksChannel.Reader.WaitToReadAsync(cancellationToken))
                {
                    // Collect available embed chunks up to batch size
                    while (batch.Count < batchSize && _embedChunksChannel.Reader.TryRead(out var embedChunk))
                    {
                        batch.Add(embedChunk);
                    }

                    if (batch.Count != 0)
                    {
                        try
                        {
                            await ExecuteWithLockAsync(async () =>
                            {
                                var chunks = batch.Select(ec => ec.Chunk).ToList();
                                var embeddings = batch.Select(ec => ec.Embedding).ToList();

                                var chunkIds = await _databaseProvider.InsertChunksBatchAsync(chunks, cancellationToken);

                                if (_embeddingProvider != null)
                                {
                                    await _databaseProvider.InsertEmbeddingsBatchAsync(chunkIds, embeddings, cancellationToken);
                                }

                                Interlocked.Add(ref _chunksStored, chunkIds.Count);
                                return chunkIds.Count;
                            }, writeLock: true, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Store worker failed for batch of {Count} chunks", batch.Count);
                        }
                        finally
                        {
                            batch.Clear();
                        }
                    }
                }

                // Process any remaining embed chunks in the batch
                if (batch.Count != 0)
                {
                    try
                    {
                        await ExecuteWithLockAsync(async () =>
                        {
                            var chunks = batch.Select(ec => ec.Chunk).ToList();
                            var embeddings = batch.Select(ec => ec.Embedding).ToList();

                            var chunkIds = await _databaseProvider.InsertChunksBatchAsync(chunks, cancellationToken);

                            if (_embeddingProvider != null)
                            {
                                await _databaseProvider.InsertEmbeddingsBatchAsync(chunkIds, embeddings, cancellationToken);
                            }

                            Interlocked.Add(ref _chunksStored, chunkIds.Count);
                            return chunkIds.Count;
                        }, writeLock: true, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Store worker failed for remaining batch of {Count} embed chunks", batch.Count);
                    }
                }
            }
        }
        catch (ChannelClosedException)
        {
            // Channel completed, exit
        }
    }

    /// <summary>
    /// Worker that reads FileProcessingResult from channel, and writes chunks to next channel.
    /// </summary>
    private async Task RunParseWorker(ChannelReader<FileProcessingResult> reader, ChannelWriter<Chunk> nextWriter, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var fileResult in reader.ReadAllAsync(cancellationToken))
            {
                foreach (var chunk in fileResult.Chunks)
                {
                    await nextWriter.WriteAsync(chunk, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parse worker failed");
        }
        finally
        {
            nextWriter.TryComplete();
        }
    }

    /// <summary>
    /// Worker that reads chunks from channel, generates embeddings, and writes chunks to next channel.
    /// </summary>
    private async Task RunEmbedWorker(ChannelReader<Chunk> reader, ChannelWriter<Chunk> nextWriter, CancellationToken cancellationToken)
    {
        var batch = new List<Chunk>();
        var batchSize = _config?.EmbeddingBatchSize ?? 100;

        try
        {
            await foreach (var chunk in reader.ReadAllAsync(cancellationToken))
            {
                batch.Add(chunk);
                if (batch.Count >= batchSize)
                {
                    await ProcessEmbedBatch(batch, nextWriter, cancellationToken);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                await ProcessEmbedBatch(batch, nextWriter, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embed worker failed");
        }
        finally
        {
            nextWriter.TryComplete();
        }
    }

    /// <summary>
    /// Worker that reads chunks from channel and stores them in the database.
    /// </summary>
    private async Task RunStoreWorker(ChannelReader<Chunk> reader, CancellationToken cancellationToken)
    {
        var batchSize = _config?.DatabaseBatchSize ?? 1000;
        var batch = new List<Chunk>();

        try
        {
            await foreach (var chunk in reader.ReadAllAsync(cancellationToken))
            {
                batch.Add(chunk);
                if (batch.Count >= batchSize)
                {
                    await ProcessStoreBatch(batch, cancellationToken);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                await ProcessStoreBatch(batch, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Store worker failed");
        }
    }

    /// <summary>
    /// Processes a batch of chunks for embedding.
    /// </summary>
    private async Task ProcessEmbedBatch(List<Chunk> batch, ChannelWriter<Chunk> nextWriter, CancellationToken cancellationToken)
    {
        var texts = batch.Select(c => c.Content).ToList();
        var embeddings = await _embeddingProvider!.EmbedAsync(texts, cancellationToken);

        for (var i = 0; i < batch.Count; i++)
        {
            var chunk = batch[i] with { Embedding = new ReadOnlyMemory<float>(embeddings[i].ToArray()) };
            await nextWriter.WriteAsync(chunk, cancellationToken);
        }
    }

    /// <summary>
    /// Processes a batch of chunks for storage.
    /// </summary>
    private async Task ProcessStoreBatch(List<Chunk> batch, CancellationToken cancellationToken)
    {
        await ExecuteWithLockAsync(async () =>
        {
            var chunkIds = await _databaseProvider.InsertChunksBatchAsync(batch, cancellationToken);

            var embeddings = batch.Where(c => c.Embedding != null).Select(c => c.Embedding!.Value.ToArray().ToList()).ToList();
            if (embeddings.Any())
            {
                await _databaseProvider.InsertEmbeddingsBatchAsync(chunkIds, embeddings, cancellationToken);
            }

            Interlocked.Add(ref _chunksStored, chunkIds.Count);
            return chunkIds.Count;
        }, writeLock: true, cancellationToken);
    }

    /// <summary>
    /// Parses a single file and returns chunks (used by worker pipeline).
    /// </summary>
    private async Task<List<Chunk>> ParseFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        // Detect language
        var language = await DetectFileLanguageAsync(filePath);
        if (language == Language.Unknown)
        {
            return new List<Chunk>();
        }

        // Get parser
        if (!_languageParsers.TryGetValue(language, out var parser))
        {
            return new List<Chunk>();
        }

        // Get or create fileId for parsing
        var relativePath = Path.GetRelativePath(_baseDirectory, filePath);
        var existingFile = await _databaseProvider.GetFileByPathAsync(relativePath, cancellationToken);
        var fileId = existingFile?.Id ?? 0;

        // Create file model for parsing
        var fileInfo = new FileInfo(filePath);
        var file = new FileModel(
            path: relativePath,
            mtime: new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeSeconds(),
            language: language,
            sizeBytes: fileInfo.Length,
            id: fileId
        );

        // Parse file
        return await parser.ParseAsync(file);
    }

    /// <summary>
    /// Gets or creates a semaphore for file-level locking.
    /// </summary>
    private async Task<SemaphoreSlim> GetFileLockAsync(string filePath)
    {
        var key = Path.GetFullPath(filePath);
        return _fileLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

    /// <summary>
    /// Executes database operations with appropriate locking.
    /// </summary>
    private async Task<T> ExecuteWithLockAsync<T>(
        Func<Task<T>> operation,
        bool writeLock = false,
        CancellationToken cancellationToken = default)
    {
        if (writeLock)
        {
            _dbLock.EnterWriteLock();
            try
            {
                return await operation();
            }
            finally
            {
                _dbLock.ExitWriteLock();
            }
        }
        else
        {
            _dbLock.EnterReadLock();
            try
            {
                return await operation();
            }
            finally
            {
                _dbLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Discovers files in directory matching patterns with efficient exclude filtering.
    /// </summary>
    private async Task<List<string>> DiscoverFilesAsync(
        string directory,
        List<string>? patterns,
        List<string>? excludePatterns,
        CancellationToken cancellationToken)
    {
        // Implementation using parallel directory traversal
        var files = new ConcurrentBag<string>();

        // Use Task.Run for CPU-bound directory traversal
        await Task.Run(() =>
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                if (cancellationToken.IsCancellationRequested) break;

                if (ShouldIncludeFile(file, patterns, excludePatterns))
                {
                    files.Add(file);
                }
            }
        }, cancellationToken);

        return files.ToList();
    }

    /// <summary>
    /// Filters files to only those that have changed since last indexing.
    /// </summary>
    private async Task<List<string>> FilterChangedFilesAsync(
        List<string> files,
        CancellationToken cancellationToken)
    {
        var changedFiles = new List<string>();

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (await HasFileChangedAsync(file, cancellationToken))
            {
                changedFiles.Add(file);
            }
        }

        return changedFiles;
    }

    /// <summary>
    /// Collects results from the pipeline processing.
    /// </summary>
    private async Task<IndexingResult> CollectPipelineResultsAsync()
    {
        return new IndexingResult
        {
            Status = IndexingStatus.Success,
            FilesProcessed = _filesProcessed,
            TotalChunks = _chunksStored
        };
    }

    /// <summary>
    /// Stores parsed chunks and returns storage result.
    /// </summary>
    private async Task<(int ChunksStored, int FileId)> StoreParsedChunksAsync(
        string filePath,
        List<Chunk> chunks,
        CancellationToken cancellationToken)
    {
        var relativePath = Path.GetRelativePath(_baseDirectory, filePath);
        var fileInfo = new FileInfo(filePath);
        var language = await DetectFileLanguageAsync(filePath);
        var file = new FileModel(
            path: relativePath,
            mtime: new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeSeconds(),
            language: language,
            sizeBytes: fileInfo.Length
        );

        var fileId = await _databaseProvider.UpsertFileAsync(file, cancellationToken);
        var chunkIds = await _databaseProvider.InsertChunksBatchAsync(chunks, cancellationToken);

        return (chunkIds.Count, fileId);
    }

    /// <summary>
    /// Detects the language of a file based on its extension and content.
    /// </summary>
    private async Task<Language> DetectFileLanguageAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        // Simple extension-based detection
        return extension switch
        {
            ".cs" => Language.CSharp,
            ".py" => Language.Python,
            ".js" => Language.JavaScript,
            ".ts" => Language.TypeScript,
            ".java" => Language.Java,
            ".cpp" or ".cc" or ".cxx" => Language.Cpp,
            ".c" => Language.C,
            ".go" => Language.Go,
            ".rs" => Language.Rust,
            ".php" => Language.PHP,
            ".rb" => Language.Ruby,
            _ => Language.Unknown
        };
    }

    /// <summary>
    /// Checks if a file should be included based on patterns and exclude patterns.
    /// </summary>
    private bool ShouldIncludeFile(string filePath, List<string>? patterns, List<string>? excludePatterns)
    {
        var fileName = Path.GetFileName(filePath);

        // Check exclude patterns first
        if (excludePatterns != null)
        {
            var excludeMatcher = new Matcher();
            excludeMatcher.AddIncludePatterns(excludePatterns);
            if (excludeMatcher.Match(fileName).HasMatches)
            {
                return false;
            }
        }

        // If no patterns specified, include common code files
        if (patterns == null || patterns.Count == 0)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return new[] { ".cs", ".py", ".js", ".ts", ".java", ".cpp", ".c", ".go", ".rs", ".php", ".rb" }
                .Contains(extension);
        }

        // Check include patterns using glob matching
        var includeMatcher = new Matcher();
        includeMatcher.AddIncludePatterns(patterns);
        return includeMatcher.Match(fileName).HasMatches;
    }

    /// <summary>
    /// Checks if a file has changed since last indexing.
    /// </summary>
    private async Task<bool> HasFileChangedAsync(string filePath, CancellationToken cancellationToken)
    {
        var relativePath = Path.GetRelativePath(_baseDirectory, filePath);
        var existingFile = await _databaseProvider.GetFileByPathAsync(relativePath, cancellationToken);

        if (existingFile == null)
        {
            // New file, needs processing
            return true;
        }

        var fileInfo = new FileInfo(filePath);
        var currentMtime = new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeSeconds();

        // File has changed if modification time is different
        return currentMtime != existingFile.Mtime;
    }

    private bool IsSupportedFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return new[] { ".cs", ".py", ".js", ".ts", ".java", ".cpp", ".c", ".go", ".rs", ".php", ".rb" }.Contains(extension);
    }

    private bool IsIgnored(string dirPath)
    {
        var name = Path.GetFileName(dirPath);
        return name.StartsWith('.') || name is "bin" or "obj" or "node_modules" or ".git";
    }

    private async Task ProcessFilesAsync(IEnumerable<string> files, CancellationToken ct)
    {
        var filesToProcess = await FilterChangedFilesAsync(files.ToList(), ct);
        await ProcessFilesPipelineAsync(filesToProcess, ct);
    }

    private async Task ProcessDirectoryIterativeAsync(string rootPath, CancellationToken ct)
    {
        var directories = new Queue<string>();
        directories.Enqueue(rootPath);
        while (directories.TryDequeue(out var dir) && !ct.IsCancellationRequested)
        {
            var files = Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly).Where(IsSupportedFile);
            await ProcessFilesAsync(files, ct);
            var subdirs = Directory.EnumerateDirectories(dir).Where(d => !IsIgnored(d));
            foreach(var sub in subdirs) directories.Enqueue(sub);
            _logger.LogDebug("Processed dir {Dir}, queued {Count} subdirs", dir, directories.Count);
        }
    }

    /// <summary>
    /// Detects programming language from file path.
    /// </summary>
    private static Language DetectLanguageFromPath(string filePath)
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