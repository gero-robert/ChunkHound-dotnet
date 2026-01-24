using Xunit;
using Moq;
using ChunkHound.Services;
using ChunkHound.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChunkHound.Tests.Services
{
    public class BatchProcessorTests
    {
        private readonly Mock<IFileProcessor> _mockFileProcessor;
        private readonly Mock<IErrorHandler> _mockErrorHandler;
        private readonly Mock<IProgressManager> _mockProgressManager;
        private readonly BatchProcessor _processor;

        public BatchProcessorTests()
        {
            _mockFileProcessor = new Mock<IFileProcessor>();
            _mockErrorHandler = new Mock<IErrorHandler>();
            _mockProgressManager = new Mock<IProgressManager>();
            _processor = new BatchProcessor(
                _mockFileProcessor.Object,
                _mockErrorHandler.Object,
                _mockProgressManager.Object);
        }

        /// <summary>
        /// Tests that the batch processor handles an empty file list gracefully, returning an empty result without errors.
        /// This validates the edge case where no files are provided, ensuring system stability and preventing null reference exceptions.
        /// Business value: Maintains robustness for automated scripts or user inputs with no files to process.
        /// </summary>
        [Fact]
        public async Task ProcessAllBatchesAsync_EmptyFileList_ReturnsEmptyResult()
        {
            // Arrange
            var filePaths = new List<string>();

            // Act
            var result = await _processor.ProcessAllBatchesAsync(filePaths);

            // Assert
            Assert.Equal(0, result.TotalAttempted);
            Assert.Equal(0, result.TotalProcessed);
            Assert.Equal(0, result.TotalFailed);
        }

        /// <summary>
        /// Tests the primary success scenario where all files in the batch are processed successfully.
        /// Validates that the processor correctly aggregates results, reporting accurate counts of attempted, processed, and failed files.
        /// Business value: Ensures core batch processing functionality works reliably for typical use cases with valid files.
        /// </summary>
        [Fact]
        public async Task ProcessAllBatchesAsync_AllFilesProcessed_ReturnsSuccess()
        {
            // Arrange
            var filePaths = new List<string> { "file1.txt", "file2.txt" };
            _mockFileProcessor.Setup(p => p.ProcessFileAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new FileProcessingResult { Status = FileProcessingStatus.Success });

            // Act
            var result = await _processor.ProcessAllBatchesAsync(filePaths);

            // Assert
            Assert.Equal(2, result.TotalAttempted);
            Assert.Equal(2, result.TotalProcessed);
            Assert.Equal(0, result.TotalFailed);
        }

        /// <summary>
        /// Tests error handling and failure tracking when some files fail during batch processing.
        /// Validates that the system accurately counts and reports successful vs failed operations.
        /// Business value: Provides reliable feedback on partial failures, enabling users to identify and address issues without losing visibility into successful operations.
        /// </summary>
        [Fact]
        public async Task ProcessAllBatchesAsync_SomeFilesFail_TracksFailures()
        {
            // Arrange
            var filePaths = new List<string> { "file1.txt", "file2.txt" };
            _mockFileProcessor.Setup(p => p.ProcessFileAsync("file1.txt", It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new FileProcessingResult { Status = FileProcessingStatus.Success });
            _mockFileProcessor.Setup(p => p.ProcessFileAsync("file2.txt", It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new FileProcessingResult
                {
                    Status = FileProcessingStatus.Error,
                    Error = "Processing failed"
                });

            // Act
            var result = await _processor.ProcessAllBatchesAsync(filePaths);

            // Assert
            Assert.Equal(2, result.TotalAttempted);
            Assert.Equal(1, result.TotalProcessed);
            Assert.Equal(1, result.TotalFailed);
        }

        /// <summary>
        /// Tests the performance optimization feature of dynamic batch sizing, where batch size increases when processing is fast.
        /// Validates that the system adapts batch sizes based on processing speed to maximize throughput while respecting time thresholds.
        /// Business value: Improves efficiency and performance for large-scale file processing by optimizing batch sizes dynamically.
        /// </summary>
        [Fact]
        public async Task ProcessAllBatchesAsync_DynamicBatchSizing_IncreasesOnFastProcessing()
        {
            // Arrange
            var filePaths = new List<string>();
            for (int i = 0; i < 50; i++) filePaths.Add($"file{i}.txt");
            _mockFileProcessor.Setup(p => p.ProcessFileAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new FileProcessingResult { Status = FileProcessingStatus.Success });

            // Act
            var result = await _processor.ProcessAllBatchesAsync(filePaths, initialBatchSize: 5, maxBatchSize: 20, fastThreshold: TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(50, result.TotalAttempted);
            Assert.Equal(50, result.TotalProcessed);
            Assert.True(result.BatchCount > 1); // Should have multiple batches with dynamic sizing
        }

        /// <summary>
        /// Tests error recovery mechanisms for permanent failures, ensuring the system continues processing remaining files.
        /// Validates that permanent failures are tracked separately and do not halt the entire batch operation.
        /// Business value: Ensures resilience in large-scale file processing, preventing single failures from stopping workflows.
        /// </summary>
        [Fact]
        public async Task ProcessAllBatchesAsync_ErrorRecovery_BatchSplitting()
        {
            // Arrange
            var filePaths = new List<string> { "file1.txt", "file2.txt", "file3.txt" };
            _mockFileProcessor.Setup(p => p.ProcessFileAsync("file1.txt", It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new FileProcessingResult { Status = FileProcessingStatus.Success });
            _mockFileProcessor.Setup(p => p.ProcessFileAsync("file2.txt", It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new FileProcessingResult { Status = FileProcessingStatus.PermanentFailure, Error = "Permanent error" });
            _mockFileProcessor.Setup(p => p.ProcessFileAsync("file3.txt", It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new FileProcessingResult { Status = FileProcessingStatus.Success });

            // Act
            var result = await _processor.ProcessAllBatchesAsync(filePaths);

            // Assert
            Assert.Equal(3, result.TotalAttempted);
            Assert.Equal(2, result.TotalProcessed);
            Assert.Equal(1, result.TotalFailed);
            Assert.Equal(1, result.TotalPermanentFailures);
        }
    }
}