using Xunit;
using ChunkHound.Workers;
using ChunkHound.Providers;
using ChunkHound.Services;
using ChunkHound.Core;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using FileModel = ChunkHound.Core.File;

namespace ChunkHound.Tests.Workers;

/// <summary>
/// Integration tests for the worker components.
/// Tests parallel processing of files, graceful error handling, and queue backpressure management.
/// </summary>
public class WorkerIntegrationTests : IDisposable
{
    private readonly ConcurrentQueue<string> _filesQueue;
    private readonly ConcurrentQueue<Chunk> _chunksQueue;
    private readonly ConcurrentQueue<EmbedChunk> _embedChunksQueue;
    private readonly ReaderWriterLockSlim _dbLock;
    private readonly Mock<ILogger<ParseWorker>> _parseLogger;
    private readonly Mock<ILogger<EmbedWorker>> _embedLogger;
    private readonly Mock<ILogger<StoreWorker>> _storeLogger;
    private readonly Mock<ILogger<UniversalParser>> _parserLogger;
    private readonly Mock<ILogger<DatabaseProvider>> _dbLogger;

    public WorkerIntegrationTests()
    {
        _filesQueue = new ConcurrentQueue<string>();
        _chunksQueue = new ConcurrentQueue<Chunk>();
        _embedChunksQueue = new ConcurrentQueue<EmbedChunk>();
        _dbLock = new ReaderWriterLockSlim();
        _parseLogger = new Mock<ILogger<ParseWorker>>();
        _embedLogger = new Mock<ILogger<EmbedWorker>>();
        _storeLogger = new Mock<ILogger<StoreWorker>>();
        _parserLogger = new Mock<ILogger<UniversalParser>>();
        _dbLogger = new Mock<ILogger<DatabaseProvider>>();
    }

    /// <summary>
    /// Tests the full indexing pipeline integration by processing 100 files through parse, embed, and store workers.
    /// Validates that workers can handle high-throughput file processing, queue management, and end-to-end data flow
    /// from file discovery to database storage, ensuring the system scales appropriately for batch operations.
    /// </summary>
    [Fact]
    public async Task FullPipelineIntegration_Processes100Files_Successfully()
    {
        // Arrange
        var testFiles = GenerateTestFiles(100);
        var languageConfig = new LanguageConfigProvider();

        // Setup parser
        var parser = new UniversalParser(_parserLogger.Object, languageConfig);

        // Setup embedding provider
        var embedProvider = new FakeConstantEmbeddingProvider();

        // Setup database provider
        var dbProvider = new DatabaseProvider(_dbLogger.Object);

        // Setup workers
        var parseWorker = new ParseWorker(_filesQueue, _chunksQueue, parser, logger: _parseLogger.Object);
        var embedConfig = new WorkerConfig { BatchSize = 10 };
        var embedWorker = new EmbedWorker(embedProvider, _chunksQueue, _embedChunksQueue, logger: _embedLogger.Object, config: embedConfig);
        var storeConfig = new WorkerConfig { BatchSize = 20 };
        var storeWorker = new StoreWorker(dbProvider, _embedChunksQueue, _dbLock, logger: _storeLogger.Object, config: storeConfig);

        // Enqueue files
        foreach (var file in testFiles)
        {
            _filesQueue.Enqueue(file);
        }

        // Act
        var cts = new CancellationTokenSource(30000); // 30 second timeout

        var parseTask = Task.Run(() => parseWorker.RunAsync(cts.Token));
        var embedTask = Task.Run(() => embedWorker.StartAsync(cts.Token));
        var storeTask = Task.Run(() => storeWorker.StartAsync(cts.Token));

        // Let workers process for a bit
        await Task.Delay(5000, cts.Token);

        // Stop workers
        cts.Cancel();

        await Task.WhenAll(parseTask, embedTask, storeTask);

        // Assert
        // Verify that files were processed (queues should be empty or nearly empty)
        Assert.True(_filesQueue.IsEmpty || _filesQueue.Count < 10, "Most files should be processed");
        Assert.True(_chunksQueue.IsEmpty, "Chunks queue should be processed by embed worker");
        Assert.True(_embedChunksQueue.IsEmpty, "Embed chunks queue should be processed by store worker");
    }

    /// <summary>
    /// Tests error handling in the parse worker when encountering invalid files.
    /// Validates that the worker continues processing valid files even when some files fail to parse,
    /// ensuring system resilience and preventing single file errors from stopping the entire indexing process.
    /// </summary>
    [Fact]
    public async Task ErrorHandling_ParseWorkerFails_ContinuesProcessing()
    {
        // Arrange
        var validContent = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            Console.WriteLine(""Hello World"");
        }
    }
}";
        var validFile = CreateTestFile("valid.cs", validContent);
        var invalidFile = "nonexistent.cs"; // This will cause an error

        _filesQueue.Enqueue(validFile);
        _filesQueue.Enqueue(invalidFile);

        var languageConfig = new LanguageConfigProvider();
        var parser = new UniversalParser(_parserLogger.Object, languageConfig);
        var parseWorker = new ParseWorker(_filesQueue, _chunksQueue, parser, logger: _parseLogger.Object);

        // Act
        var cts = new CancellationTokenSource(5000);
        await parseWorker.RunAsync(cts.Token);

        // Assert
        // Should have processed the valid file despite the error on invalid file
        Assert.True(_chunksQueue.TryDequeue(out var chunk), "Should have processed valid file");
        Assert.NotNull(chunk);
        Assert.Equal(Language.CSharp, chunk.Language);
    }

    /// <summary>
    /// Tests backpressure management in the embed worker by verifying batch size limits are respected.
    /// Ensures that the worker processes chunks in appropriately sized batches to prevent memory overload
    /// and maintain efficient resource utilization during high-volume embedding operations.
    /// </summary>
    [Fact]
    public async Task BackpressureManagement_EmbedWorkerBatchSize_RespectsLimits()
    {
        // Arrange
        var embedProviderMock = new Mock<IEmbeddingProvider>();
        embedProviderMock.Setup(p => p.ProviderName).Returns("MockProvider");
        embedProviderMock.Setup(p => p.ModelName).Returns("mock-v1");
        embedProviderMock.Setup(p => p.EmbedAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken _) =>
                texts.Select(_ => new List<float> { 0.1f, 0.2f, 0.3f }).ToList());

        var embedConfig = new WorkerConfig { BatchSize = 5 };
        var embedWorker = new EmbedWorker(embedProviderMock.Object, _chunksQueue, _embedChunksQueue, logger: _embedLogger.Object, config: embedConfig);

        // Add more chunks than batch size
        for (int i = 0; i < 12; i++)
        {
            var chunk = new Chunk($"Symbol{i}", 1, 5, $"code {i}", ChunkType.Function, 1, Language.CSharp);
            _chunksQueue.Enqueue(chunk);
        }

        // Act
        var cts = new CancellationTokenSource(2000);
        await embedWorker.StartAsync(cts.Token);

        // Assert
        // Verify that EmbedAsync was called with batches of size <= 5
        embedProviderMock.Verify(p => p.EmbedAsync(It.Is<List<string>>(texts => texts.Count <= 5), It.IsAny<CancellationToken>()), Times.AtLeast(2));

        // Should process all chunks
        Assert.Equal(12, _embedChunksQueue.Count);
    }

    private List<string> GenerateTestFiles(int count)
    {
        var files = new List<string>();
        var languages = new[] { "cs", "py", "js" };

        for (int i = 0; i < count; i++)
        {
            var lang = languages[i % languages.Length];
            var fileName = $"test_{i}.{lang}";
            var filePath = CreateTestFile(fileName, GenerateCodeSnippet(lang));
            files.Add(filePath);
        }

        return files;
    }

    private string CreateTestFile(string fileName, string content)
    {
        var tempDir = Path.GetTempPath();
        var filePath = Path.Combine(tempDir, "ChunkHoundTests", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        System.IO.File.WriteAllText(filePath, content);
        return filePath;
    }

    private string GenerateCodeSnippet(string extension)
    {
        return extension switch
        {
            "cs" => "using System;\n\nnamespace Test {\n    public class TestClass {\n        public void Method() { }\n    }\n}",
            "py" => "def test_function():\n    pass\n\nclass TestClass:\n    def method(self):\n        pass",
            "js" => "function testFunction() {\n    return true;\n}\n\nconst testClass = {\n    method() { }\n};",
            _ => "test content"
        };
    }

    public void Dispose()
    {
        _dbLock.Dispose();

        // Cleanup test files
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ChunkHoundTests");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}