using Xunit;
using ChunkHound.Core;
using System.Collections.Generic;

namespace ChunkHound.Core.Tests.Models
{
    public class EmbeddingModelsTests
    {
        /// <summary>
        /// Tests that EmbeddingData constructor properly initializes all properties with valid inputs.
        /// This ensures embedding data structures are correctly populated for storage and processing.
        /// </summary>
        [Fact]
        public void EmbeddingData_Constructor_SetsPropertiesCorrectly()
        {
            // Arrange
            var chunkId = 1L;
            var provider = "OpenAI";
            var model = "text-embedding-ada-002";
            var dimensions = 1536;
            var embedding = new List<float> { 0.1f, 0.2f, 0.3f };
            var status = "success";

            // Act
            var data = new EmbeddingData(chunkId, provider, model, dimensions, embedding, status);

            // Assert
            Assert.Equal(chunkId, data.ChunkId);
            Assert.Equal(provider, data.Provider);
            Assert.Equal(model, data.Model);
            Assert.Equal(dimensions, data.Dimensions);
            Assert.Equal(embedding, data.Embedding);
            Assert.Equal(status, data.Status);
        }

        /// <summary>
        /// Tests that EmbeddingData constructor throws ArgumentNullException when provider is null.
        /// This validates input validation for embedding provider identification.
        /// </summary>
        [Fact]
        public void EmbeddingData_Constructor_NullProvider_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EmbeddingData(1, null!, "model", 1536, new List<float>(), "success"));
        }

        /// <summary>
        /// Tests that EmbeddingData constructor throws ArgumentNullException when model is null.
        /// This validates input validation for embedding model specification.
        /// </summary>
        [Fact]
        public void EmbeddingData_Constructor_NullModel_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EmbeddingData(1, "provider", null!, 1536, new List<float>(), "success"));
        }

        /// <summary>
        /// Tests that EmbeddingData constructor throws ArgumentNullException when embedding vector is null.
        /// This validates input validation for embedding vector data integrity.
        /// </summary>
        [Fact]
        public void EmbeddingData_Constructor_NullEmbedding_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EmbeddingData(1, "provider", "model", 1536, null!, "success"));
        }

        /// <summary>
        /// Tests that EmbeddingData constructor throws ArgumentNullException when status is null.
        /// This validates input validation for embedding operation status tracking.
        /// </summary>
        [Fact]
        public void EmbeddingData_Constructor_NullStatus_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EmbeddingData(1, "provider", "model", 1536, new List<float>(), null!));
        }

        /// <summary>
        /// Tests that EmbeddingResult default constructor initializes all counters and collections to zero/empty.
        /// This ensures proper baseline state for tracking embedding operation results.
        /// </summary>
        [Fact]
        public void EmbeddingResult_DefaultValues_AreCorrect()
        {
            // Act
            var result = new EmbeddingResult();

            // Assert
            Assert.Equal(0, result.TotalGenerated);
            Assert.Equal(0, result.TotalProcessed);
            Assert.Equal(0, result.SuccessfulChunks);
            Assert.Equal(0, result.FailedChunks);
            Assert.Equal(0, result.PermanentFailures);
            Assert.Equal(0, result.RetryAttempts);
            Assert.NotNull(result.ErrorStats);
            Assert.NotNull(result.ErrorSamples);
            Assert.Null(result.Error);
        }

        /// <summary>
        /// Tests that RegenerateResult can be initialized with a status value.
        /// This validates the basic functionality of regeneration result tracking.
        /// </summary>
        [Fact]
        public void RegenerateResult_WithRequiredStatus_SetsStatus()
        {
            // Act
            var result = new RegenerateResult { Status = "completed" };

            // Assert
            Assert.Equal("completed", result.Status);
        }

        /// <summary>
        /// Tests that EmbeddingProgressInfo constructor properly initializes progress tracking properties.
        /// This ensures progress reporting works correctly during embedding operations.
        /// </summary>
        [Fact]
        public void EmbeddingProgressInfo_Constructor_SetsPropertiesCorrectly()
        {
            // Arrange
            var progress = 0.5;
            var message = "Processing chunks";
            var processed = 50;
            var total = 100;

            // Act
            var info = new EmbeddingProgressInfo(progress, message, processed, total);

            // Assert
            Assert.Equal(progress, info.Progress);
            Assert.Equal(message, info.Message);
            Assert.Equal(processed, info.Processed);
            Assert.Equal(total, info.Total);
        }

        /// <summary>
        /// Tests that EmbeddingProgressInfo constructor throws ArgumentNullException when message is null.
        /// This validates input validation for progress message requirements.
        /// </summary>
        [Fact]
        public void EmbeddingProgressInfo_Constructor_NullMessage_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EmbeddingProgressInfo(0.5, null!, 50, 100));
        }

        /// <summary>
        /// Tests that BatchResult default constructor initializes collections and counters properly.
        /// This ensures batch processing results start with correct baseline state.
        /// </summary>
        [Fact]
        public void BatchResult_DefaultValues_AreCorrect()
        {
            // Act
            var result = new BatchResult();

            // Assert
            Assert.Equal(0, result.BatchNum);
            Assert.NotNull(result.SuccessfulChunks);
            Assert.NotNull(result.FailedChunks);
            Assert.NotNull(result.ErrorStats);
            Assert.NotNull(result.ErrorSamples);
            Assert.Equal(0, result.RetryAttempts);
        }

        /// <summary>
        /// Tests that BatchResult can track successfully processed chunks with their embeddings.
        /// This validates the data structure for successful batch operation results.
        /// </summary>
        [Fact]
        public void BatchResult_CanAddSuccessfulChunks()
        {
            // Arrange
            var result = new BatchResult();
            var chunk = new Chunk("test", 1, 10, "code", ChunkType.Function, 1, Language.CSharp);
            var embedding = new List<float> { 0.1f, 0.2f };

            // Act
            result.SuccessfulChunks.Add((chunk, embedding));

            // Assert
            Assert.Single(result.SuccessfulChunks);
            Assert.Equal(chunk, result.SuccessfulChunks[0].Chunk);
            Assert.Equal(embedding, result.SuccessfulChunks[0].Embedding);
        }

        /// <summary>
        /// Tests that BatchResult can track failed chunks with error information and classification.
        /// This validates error tracking and classification in batch processing results.
        /// </summary>
        [Fact]
        public void BatchResult_CanAddFailedChunks()
        {
            // Arrange
            var result = new BatchResult();
            var chunk = new Chunk("test", 1, 10, "code", ChunkType.Function, 1, Language.CSharp);
            var error = "API error";
            var classification = EmbeddingErrorClassification.Transient;

            // Act
            result.FailedChunks.Add((chunk, error, classification));

            // Assert
            Assert.Single(result.FailedChunks);
            Assert.Equal(chunk, result.FailedChunks[0].Chunk);
            Assert.Equal(error, result.FailedChunks[0].Error);
            Assert.Equal(classification, result.FailedChunks[0].Classification);
        }
    }
}