using Xunit;
using ChunkHound.Core;

namespace ChunkHound.Core.Tests.Models
{
    public class WorkerConfigTests
    {
        [Fact]
        public void WorkerConfig_DefaultValues_AreSetCorrectly()
        {
            // Act
            var config = new WorkerConfig();

            // Assert
            Assert.Equal(Math.Max(1, Environment.ProcessorCount * 10), config.BatchSize);
            Assert.Equal(3, config.MaxRetries);
            Assert.Equal(10, config.BusyWaitDelayMs);
            Assert.Equal(100, config.RetryInitialDelayMs);
            Assert.Equal(5000, config.MaxRetryDelayMs);
        }

        [Fact]
        public void WorkerConfig_CustomValues_CanBeSet()
        {
            // Act
            var config = new WorkerConfig
            {
                BatchSize = 50,
                MaxRetries = 5,
                BusyWaitDelayMs = 20,
                RetryInitialDelayMs = 200,
                MaxRetryDelayMs = 10000
            };

            // Assert
            Assert.Equal(50, config.BatchSize);
            Assert.Equal(5, config.MaxRetries);
            Assert.Equal(20, config.BusyWaitDelayMs);
            Assert.Equal(200, config.RetryInitialDelayMs);
            Assert.Equal(10000, config.MaxRetryDelayMs);
        }

        [Fact]
        public void WorkerConfig_BatchSize_IsAtLeastOne()
        {
            // Arrange - This test assumes we have at least 1 CPU core
            var config = new WorkerConfig();

            // Assert
            Assert.True(config.BatchSize >= 1);
        }

        [Fact]
        public void WorkerConfig_BatchSize_CalculatedFromProcessorCount()
        {
            // Arrange
            var expectedBatchSize = Math.Max(1, Environment.ProcessorCount * 10);
            var config = new WorkerConfig();

            // Assert
            Assert.Equal(expectedBatchSize, config.BatchSize);
        }

        [Fact]
        public void WorkerConfig_Properties_AreIndependent()
        {
            // Arrange
            var config1 = new WorkerConfig { BatchSize = 100 };
            var config2 = new WorkerConfig { BatchSize = 200 };

            // Assert
            Assert.Equal(100, config1.BatchSize);
            Assert.Equal(200, config2.BatchSize);
        }
    }
}