using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using ChunkHound.Core;
using ChunkHound.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace ChunkHound.Core.Tests.Services
{
    public class IndexingCoordinatorTests
    {
        private readonly Mock<IDatabaseProvider> _databaseProviderMock;
        private readonly Mock<IEmbeddingProvider> _embeddingProviderMock;
        private readonly Mock<ILogger<IndexingCoordinator>> _loggerMock;
        private readonly string _baseDirectory;

        public IndexingCoordinatorTests()
        {
            _databaseProviderMock = new Mock<IDatabaseProvider>();
            _embeddingProviderMock = new Mock<IEmbeddingProvider>();
            _loggerMock = new Mock<ILogger<IndexingCoordinator>>();
            _baseDirectory = Path.Combine(Path.GetTempPath(), "ChunkHoundTest" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_baseDirectory);
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Act
            var coordinator = CreateCoordinator();

            // Assert
            Assert.NotNull(coordinator);
        }

        [Fact]
        public void Constructor_NullDatabaseProvider_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new IndexingCoordinator(null!, _baseDirectory));
        }

        [Fact]
        public void Constructor_NullBaseDirectory_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new IndexingCoordinator(_databaseProviderMock.Object, null!));
        }

        [Fact]
        public async Task ProcessDirectoryAsync_EmptyDirectory_ReturnsNoFilesStatus()
        {
            // Arrange
            var coordinator = CreateCoordinator();
            var emptyDir = Path.Combine(_baseDirectory, "empty");
            Directory.CreateDirectory(emptyDir);

            // Act
            var result = await coordinator.ProcessDirectoryAsync(emptyDir);

            // Assert
            Assert.Equal(IndexingStatus.NoFiles, result.Status);
            Assert.Equal(0, result.FilesProcessed);
            Assert.Equal(0, result.TotalChunks);
        }

        [Fact]
        public async Task ProcessDirectoryAsync_WithCancellation_ThrowsOperationCanceledException()
        {
            // Arrange
            var coordinator = CreateCoordinator();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                coordinator.ProcessDirectoryAsync(_baseDirectory, cancellationToken: cts.Token));
        }

        [Fact]
        public async Task ProcessFileAsync_UnsupportedLanguage_ReturnsUnsupportedLanguageStatus()
        {
            // Arrange
            var coordinator = CreateCoordinator();
            var testFile = Path.Combine(_baseDirectory, "test.unknown");
            System.IO.File.WriteAllText(testFile, "content");

            // Act
            var result = await coordinator.ProcessFileAsync(testFile);

            // Assert
            Assert.Equal(FileProcessingStatus.UnsupportedLanguage, result.Status);
        }

        [Fact]
        public async Task ProcessFileAsync_NoParser_ReturnsNoParserStatus()
        {
            // Arrange
            var coordinator = CreateCoordinator();
            var testFile = Path.Combine(_baseDirectory, "test.cs");
            System.IO.File.WriteAllText(testFile, "content");

            // Act
            var result = await coordinator.ProcessFileAsync(testFile);

            // Assert
            Assert.Equal(FileProcessingStatus.NoParser, result.Status);
        }

        [Fact]
        public async Task IndexAsync_CallsProcessDirectoryAsync()
        {
            // Arrange
            _embeddingProviderMock.Setup(e => e.ProviderName).Returns("test");
            _embeddingProviderMock.Setup(e => e.ModelName).Returns("model");
            _embeddingProviderMock.Setup(e => e.GetMaxTokensPerBatch()).Returns(1000);
            _embeddingProviderMock.Setup(e => e.GetMaxDocumentsPerBatch()).Returns(10);
            _embeddingProviderMock.Setup(e => e.GetRecommendedConcurrency()).Returns(8);

            var coordinator = CreateCoordinator(embeddingProvider: _embeddingProviderMock.Object);
            var testDir = Path.Combine(_baseDirectory, "test");
            Directory.CreateDirectory(testDir);

            // Act
            await coordinator.IndexAsync(testDir);

            // Assert - Method should complete without throwing
            Assert.True(true);
        }

        [Fact]
        public async Task IndexWithMocks_NoHang()
        {
            // Arrange
            var mockDb = new ChunkHound.Providers.FakeDatabaseProvider();
            var mockEmbed = new ChunkHound.Providers.FakeConstantEmbeddingProvider();
            var mockParser = new Mock<IUniversalParser>();
            mockParser.Setup(p => p.ParseAsync(It.IsAny<File>()))
                      .ReturnsAsync(new List<Chunk> { new Chunk("1", 1, "code", 1, 1, Language.CSharp, ChunkType.Function) });
            var parsers = new Dictionary<Language, IUniversalParser>
            {
                [Language.CSharp] = mockParser.Object
            };
            var coord = new IndexingCoordinator(mockDb, _baseDirectory, mockEmbed, parsers, null, null, _loggerMock.Object);
            var testDir = Path.Combine(_baseDirectory, "testdir");
            Directory.CreateDirectory(testDir);
            var testFile = Path.Combine(testDir, "test.cs");
            System.IO.File.WriteAllText(testFile, "class Test {}");

            // Act
            await coord.IndexAsync(testDir);

            // Assert
            Assert.True(true); // Should not hang
        }

        /// <summary>
        /// Tests that ProcessFileAsync successfully processes a C# file with a parser.
        /// This validates the file processing pipeline including language detection and parsing.
        /// </summary>
        [Fact]
        public async Task ProcessFileAsync_WithValidParser_ReturnsSuccess()
        {
            // Arrange
            var mockParser = new Mock<IUniversalParser>();
            var testFile = Path.Combine(_baseDirectory, "test.cs");
            var testContent = "public class Test { }";
            var expectedChunks = new List<Chunk>
            {
                new Chunk(testContent, 1, 1, testContent, ChunkType.Class, 1, Language.CSharp)
            };

            System.IO.File.WriteAllText(testFile, testContent);
            mockParser.Setup(p => p.ParseAsync(It.IsAny<File>()))
                     .ReturnsAsync(expectedChunks);

            _databaseProviderMock.Setup(d => d.GetFileByPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult((File?)null));
            _databaseProviderMock.Setup(d => d.UpsertFileAsync(It.IsAny<File>(), It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult(1));
            _databaseProviderMock.Setup(d => d.InsertChunksBatchAsync(It.IsAny<List<Chunk>>(), It.IsAny<CancellationToken>()))
                                .Returns((List<Chunk> chunks, CancellationToken ct) => Task.FromResult(new List<int>(Enumerable.Range(1, chunks.Count))));

            var coordinator = CreateCoordinator(new Dictionary<Language, IUniversalParser>
            {
                [Language.CSharp] = mockParser.Object
            });

            // Act
            var result = await coordinator.ProcessFileAsync(testFile);

            // Assert
            Assert.Equal(FileProcessingStatus.Success, result.Status);
            Assert.Equal(1, result.ChunksProcessed);
            Assert.Equal(1, result.ChunksStored);
        }

        /// <summary>
        /// Tests that ProcessDirectoryAsync processes multiple files successfully.
        /// This validates the directory processing pipeline and file discovery.
        /// </summary>
        [Fact]
        public async Task ProcessDirectoryAsync_WithMultipleFiles_ProcessesSuccessfully()
        {
            // Arrange
            var testDir = _baseDirectory;

            var file1 = Path.Combine(testDir, "test1.cs");
            var file2 = Path.Combine(testDir, "test2.cs");
            System.IO.File.WriteAllText(file1, "class Test1 {}");
            System.IO.File.WriteAllText(file2, "class Test2 {}");

            var mockParser = new Mock<IUniversalParser>();
            mockParser.Setup(p => p.ParseAsync(It.IsAny<File>()))
                     .Returns(Task.FromResult(new List<Chunk> { new Chunk("code", 1, 1, "code", ChunkType.Class, 1, Language.CSharp) }));

            _databaseProviderMock.Setup(d => d.GetFileByPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult((File?)null));
            _databaseProviderMock.Setup(d => d.UpsertFileAsync(It.IsAny<File>(), It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult(1));
            _databaseProviderMock.Setup(d => d.InsertChunksBatchAsync(It.IsAny<List<Chunk>>(), It.IsAny<CancellationToken>()))
                                .Returns((List<Chunk> chunks, CancellationToken ct) => Task.FromResult(new List<int>(Enumerable.Range(1, chunks.Count))));

            _embeddingProviderMock.Setup(e => e.ProviderName).Returns("test");
            _embeddingProviderMock.Setup(e => e.ModelName).Returns("model");
            _embeddingProviderMock.Setup(e => e.GetMaxTokensPerBatch()).Returns(1000);
            _embeddingProviderMock.Setup(e => e.GetMaxDocumentsPerBatch()).Returns(10);
            _embeddingProviderMock.Setup(e => e.GetRecommendedConcurrency()).Returns(8);
            _embeddingProviderMock.Setup(e => e.EmbedAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync((List<string> texts, CancellationToken ct) => texts.Select(t => new List<float> { 0.1f, 0.2f }).ToList());

            var coordinator = CreateCoordinator(
                parsers: new Dictionary<Language, IUniversalParser>
                {
                    [Language.CSharp] = mockParser.Object
                },
                embeddingProvider: _embeddingProviderMock.Object);

            // Act
            var result = await coordinator.ProcessDirectoryAsync(testDir);

            // Assert
            Assert.Equal(IndexingStatus.Success, result.Status);
            Assert.Equal(2, result.FilesProcessed);
            Assert.True(result.TotalChunks >= 0);
        }

        /// <summary>
        /// Tests that ProcessDirectoryAsync filters out files that haven't changed.
        /// This validates the change detection logic for incremental indexing.
        /// </summary>
        [Fact]
        public async Task ProcessDirectoryAsync_WithUnchangedFiles_SkipsProcessing()
        {
            // Arrange
            var testDir = Path.Combine(_baseDirectory, "unchanged");
            Directory.CreateDirectory(testDir);

            var testFile = Path.Combine(testDir, "test.cs");
            var content = "class Test {}";
            System.IO.File.WriteAllText(testFile, content);

            var fileInfo = new FileInfo(testFile);
            var existingFile = new File(
                path: Path.GetRelativePath(_baseDirectory, testFile),
                mtime: new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeSeconds(),
                language: Language.CSharp,
                sizeBytes: fileInfo.Length,
                id: 1
            );

            _databaseProviderMock.Setup(d => d.GetFileByPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                .ReturnsAsync(existingFile);

            _embeddingProviderMock.Setup(e => e.ProviderName).Returns("test");
            _embeddingProviderMock.Setup(e => e.ModelName).Returns("model");
            _embeddingProviderMock.Setup(e => e.GetMaxTokensPerBatch()).Returns(1000);
            _embeddingProviderMock.Setup(e => e.GetMaxDocumentsPerBatch()).Returns(10);
            _embeddingProviderMock.Setup(e => e.GetRecommendedConcurrency()).Returns(8);

            var coordinator = CreateCoordinator(embeddingProvider: _embeddingProviderMock.Object);

            // Act
            var result = await coordinator.ProcessDirectoryAsync(testDir);

            // Assert
            Assert.Equal(IndexingStatus.Success, result.Status);
            Assert.Equal(0, result.FilesProcessed); // No files should be processed
        }

        /// <summary>
        /// Tests that ProcessDirectoryAsync handles include patterns correctly.
        /// This validates file filtering based on include patterns.
        /// </summary>
        [Fact]
        public async Task ProcessDirectoryAsync_WithIncludePatterns_FiltersFiles()
        {
            // Arrange
            var testDir = _baseDirectory;

            var csFile = Path.Combine(testDir, "test.cs");
            var txtFile = Path.Combine(testDir, "test.txt");
            System.IO.File.WriteAllText(csFile, "class Test {}");
            System.IO.File.WriteAllText(txtFile, "text content");

            var mockParser = new Mock<IUniversalParser>();
            mockParser.Setup(p => p.ParseAsync(It.IsAny<File>()))
                     .Returns(Task.FromResult(new List<Chunk> { new Chunk("code", 1, 1, "code", ChunkType.Class, 1, Language.CSharp) }));

            _databaseProviderMock.Setup(d => d.GetFileByPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult((File?)null));
            _databaseProviderMock.Setup(d => d.UpsertFileAsync(It.IsAny<File>(), It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult(1));
            _databaseProviderMock.Setup(d => d.InsertChunksBatchAsync(It.IsAny<List<Chunk>>(), It.IsAny<CancellationToken>()))
                                .Returns((List<Chunk> chunks, CancellationToken ct) => Task.FromResult(new List<int>(Enumerable.Range(1, chunks.Count))));

            _embeddingProviderMock.Setup(e => e.ProviderName).Returns("test");
            _embeddingProviderMock.Setup(e => e.ModelName).Returns("model");
            _embeddingProviderMock.Setup(e => e.GetMaxTokensPerBatch()).Returns(1000);
            _embeddingProviderMock.Setup(e => e.GetMaxDocumentsPerBatch()).Returns(10);
            _embeddingProviderMock.Setup(e => e.GetRecommendedConcurrency()).Returns(8);
            _embeddingProviderMock.Setup(e => e.EmbedAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync((List<string> texts, CancellationToken ct) => texts.Select(t => new List<float> { 0.1f, 0.2f }).ToList());

            var coordinator = CreateCoordinator(
                parsers: new Dictionary<Language, IUniversalParser>
                {
                    [Language.CSharp] = mockParser.Object
                },
                embeddingProvider: _embeddingProviderMock.Object);

            // Act
            var result = await coordinator.ProcessDirectoryAsync(testDir, patterns: new List<string> { "*.cs" });

            // Assert
            Assert.Equal(IndexingStatus.Success, result.Status);
            Assert.Equal(1, result.FilesProcessed); // Only .cs file should be processed
        }

        private IndexingCoordinator CreateCoordinator(
            Dictionary<Language, IUniversalParser>? parsers = null,
            ChunkCacheService? cacheService = null,
            IndexingConfig? config = null,
            IEmbeddingProvider? embeddingProvider = null)
        {
            return new IndexingCoordinator(
                _databaseProviderMock.Object,
                _baseDirectory,
                embeddingProvider,
                parsers,
                cacheService,
                config,
                _loggerMock.Object);
        }
    }
}
