using Xunit;
using ChunkHound.Core;

namespace ChunkHound.Core.Tests.Models
{
    public class FileProcessingResultTests
    {
        [Fact]
        public void FileProcessingResult_Constructor_SetsPropertiesCorrectly()
        {
            // Arrange
            var status = FileProcessingStatus.Success;
            var error = "Some error";
            var chunksProcessed = 10;
            var chunksStored = 8;
            var fileId = 123;

            // Act
            var result = new FileProcessingResult
            {
                Status = status,
                Error = error,
                ChunksProcessed = chunksProcessed,
                ChunksStored = chunksStored,
                FileId = fileId
            };

            // Assert
            Assert.Equal(status, result.Status);
            Assert.Equal(error, result.Error);
            Assert.Equal(chunksProcessed, result.ChunksProcessed);
            Assert.Equal(chunksStored, result.ChunksStored);
            Assert.Equal(fileId, result.FileId);
        }

        [Fact]
        public void FileProcessingResult_DefaultValues_AreCorrect()
        {
            // Act
            var result = new FileProcessingResult();

            // Assert
            Assert.Equal(FileProcessingStatus.Success, result.Status); // Default enum value
            Assert.Null(result.Error);
            Assert.Equal(0, result.ChunksProcessed);
            Assert.Equal(0, result.ChunksStored);
            Assert.Equal(0, result.FileId);
        }

        [Fact]
        public void FileProcessingResult_WithNullError_SetsErrorToNull()
        {
            // Act
            var result = new FileProcessingResult
            {
                Status = FileProcessingStatus.Error,
                Error = null,
                ChunksProcessed = 5,
                ChunksStored = 5,
                FileId = 1
            };

            // Assert
            Assert.Equal(FileProcessingStatus.Error, result.Status);
            Assert.Null(result.Error);
        }

        [Theory]
        [InlineData(FileProcessingStatus.Success)]
        [InlineData(FileProcessingStatus.Error)]
        [InlineData(FileProcessingStatus.PermanentFailure)]
        [InlineData(FileProcessingStatus.UnsupportedLanguage)]
        [InlineData(FileProcessingStatus.NoParser)]
        [InlineData(FileProcessingStatus.NoChunks)]
        public void FileProcessingStatus_AllValues_AreDefined(FileProcessingStatus status)
        {
            // Assert - If we get here, the enum value exists
            Assert.True(Enum.IsDefined(typeof(FileProcessingStatus), status));
        }
    }
}