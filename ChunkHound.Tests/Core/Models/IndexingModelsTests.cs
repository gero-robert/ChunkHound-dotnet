using Xunit;
using ChunkHound.Core;

namespace ChunkHound.Core.Tests.Models
{
    public class IndexingModelsTests
    {
        [Fact]
        public void IndexingConfig_DefaultValues_AreCorrect()
        {
            // Act
            var config = new IndexingConfig();

            // Assert
            Assert.Equal(4, config.ParseWorkers);
            Assert.Equal(2, config.EmbedWorkers);
            Assert.Equal(2, config.StoreWorkers);
            Assert.Equal(100, config.EmbeddingBatchSize);
            Assert.Equal(1000, config.DatabaseBatchSize);
        }

        [Fact]
        public void IndexingConfig_CustomValues_AreSetCorrectly()
        {
            // Act
            var config = new IndexingConfig
            {
                ParseWorkers = 8,
                EmbedWorkers = 4,
                StoreWorkers = 4,
                EmbeddingBatchSize = 50,
                DatabaseBatchSize = 500
            };

            // Assert
            Assert.Equal(8, config.ParseWorkers);
            Assert.Equal(4, config.EmbedWorkers);
            Assert.Equal(4, config.StoreWorkers);
            Assert.Equal(50, config.EmbeddingBatchSize);
            Assert.Equal(500, config.DatabaseBatchSize);
        }

        [Fact]
        public void IndexingProgress_Constructor_SetsPropertiesCorrectly()
        {
            // Arrange
            var totalFiles = 100;
            var filesProcessed = 50;
            var totalChunks = 1000;
            var currentPhase = "Parsing";
            var percentComplete = 50.0;

            // Act
            var progress = new IndexingProgress
            {
                TotalFiles = totalFiles,
                FilesProcessed = filesProcessed,
                TotalChunks = totalChunks,
                CurrentPhase = currentPhase,
                PercentComplete = percentComplete
            };

            // Assert
            Assert.Equal(totalFiles, progress.TotalFiles);
            Assert.Equal(filesProcessed, progress.FilesProcessed);
            Assert.Equal(totalChunks, progress.TotalChunks);
            Assert.Equal(currentPhase, progress.CurrentPhase);
            Assert.Equal(percentComplete, progress.PercentComplete);
        }

        [Fact]
        public void IndexingProgress_DefaultValues_AreCorrect()
        {
            // Act
            var progress = new IndexingProgress();

            // Assert
            Assert.Equal(0, progress.TotalFiles);
            Assert.Equal(0, progress.FilesProcessed);
            Assert.Equal(0, progress.TotalChunks);
            Assert.Null(progress.CurrentPhase);
            Assert.Equal(0.0, progress.PercentComplete);
        }

        [Fact]
        public void IndexingResult_Constructor_SetsPropertiesCorrectly()
        {
            // Arrange
            var status = IndexingStatus.Success;
            var error = "Some error";
            var filesProcessed = 100;
            var totalChunks = 1000;
            var durationMs = 5000L;

            // Act
            var result = new IndexingResult
            {
                Status = status,
                Error = error,
                FilesProcessed = filesProcessed,
                TotalChunks = totalChunks,
                DurationMs = durationMs
            };

            // Assert
            Assert.Equal(status, result.Status);
            Assert.Equal(error, result.Error);
            Assert.Equal(filesProcessed, result.FilesProcessed);
            Assert.Equal(totalChunks, result.TotalChunks);
            Assert.Equal(durationMs, result.DurationMs);
        }

        [Fact]
        public void IndexingResult_DefaultValues_AreCorrect()
        {
            // Act
            var result = new IndexingResult();

            // Assert
            Assert.Equal(IndexingStatus.Success, result.Status); // Default enum value
            Assert.Null(result.Error);
            Assert.Equal(0, result.FilesProcessed);
            Assert.Equal(0, result.TotalChunks);
            Assert.Equal(0L, result.DurationMs);
        }

        [Theory]
        [InlineData(IndexingStatus.Success)]
        [InlineData(IndexingStatus.NoFiles)]
        [InlineData(IndexingStatus.Error)]
        [InlineData(IndexingStatus.Cancelled)]
        public void IndexingStatus_AllValues_AreDefined(IndexingStatus status)
        {
            // Assert - If we get here, the enum value exists
            Assert.True(Enum.IsDefined(typeof(IndexingStatus), status));
        }
    }
}