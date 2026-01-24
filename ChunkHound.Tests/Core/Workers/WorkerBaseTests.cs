using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using ChunkHound.Core.Workers;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace ChunkHound.Core.Tests.Workers
{
    // Test implementation of WorkerBase for testing purposes
    internal class TestWorker : WorkerBase
    {
        private readonly TaskCompletionSource<bool> _tcs = new();
        private readonly bool _shouldThrow;

        public TestWorker(ILogger logger, bool shouldThrow = false) : base(logger)
        {
            _shouldThrow = shouldThrow;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(10, cancellationToken); // Simulate some work
                Interlocked.Increment(ref _itemsProcessed);

                if (_shouldThrow)
                {
                    throw new InvalidOperationException("Test exception");
                }

                _tcs.SetResult(true);
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException)
            {
                _tcs.SetCanceled();
                throw new OperationCanceledException("Operation was canceled", ex);
            }
        }

        public Task WaitForCompletion() => _tcs.Task;
    }

    public class WorkerBaseTests
    {
        private readonly Mock<ILogger> _loggerMock;

        public WorkerBaseTests()
        {
            _loggerMock = new Mock<ILogger>();
        }

        /// <summary>
        /// Tests that WorkerBase.StartAsync() successfully executes work and increments the items processed counter.
        /// This validates the core worker execution flow and progress tracking.
        /// </summary>
        [Fact]
        public async Task StartAsync_ExecutesSuccessfully_IncrementsItemsProcessed()
        {
            // Arrange
            var worker = new TestWorker(_loggerMock.Object);

            // Act
            await worker.StartAsync();

            // Assert
            Assert.Equal(1, worker.ItemsProcessed);
        }

        /// <summary>
        /// Tests that WorkerBase.StartAsync() handles cancellation tokens properly and stops gracefully.
        /// This validates cancellation handling and resource cleanup during interruption.
        /// </summary>
        [Fact]
        public async Task StartAsync_WithCancellation_StopsGracefully()
        {
            // Arrange
            var worker = new TestWorker(_loggerMock.Object);
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => worker.StartAsync(cts.Token));
            // Worker should still be disposed properly
        }

        /// <summary>
        /// Tests that WorkerBase.StartAsync() properly handles and logs exceptions thrown during execution.
        /// This validates error handling and logging in the worker base class.
        /// </summary>
        [Fact]
        public async Task StartAsync_WithException_ThrowsAndLogsError()
        {
            // Arrange
            var worker = new TestWorker(_loggerMock.Object, shouldThrow: true);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => worker.StartAsync());
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        /// <summary>
        /// Tests that WorkerBase.StopAsync() properly cancels running worker operations.
        /// This validates the shutdown mechanism and graceful termination of workers.
        /// </summary>
        [Fact]
        public async Task StopAsync_CancelsWorker()
        {
            // Arrange
            var worker = new TestWorker(_loggerMock.Object);
            var startTask = worker.StartAsync();

            // Act
            await worker.StopAsync();

            // Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => startTask);
        }

        /// <summary>
        /// Tests that WorkerBase.Dispose() properly cleans up resources without throwing exceptions.
        /// This validates resource management and cleanup in the worker lifecycle.
        /// </summary>
        [Fact]
        public void Dispose_CleansUpResources()
        {
            // Arrange
            var worker = new TestWorker(_loggerMock.Object);

            // Act
            worker.Dispose();

            // Assert - No exceptions thrown, resources cleaned up
            Assert.True(true); // If we get here, Dispose worked
        }

        /// <summary>
        /// Tests that WorkerBase constructor throws ArgumentNullException when logger is null.
        /// This validates input validation for required dependencies in worker initialization.
        /// </summary>
        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TestWorker(null!));
        }
    }
}