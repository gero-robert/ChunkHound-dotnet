using System.Diagnostics;

namespace ChunkHound.Tests.Benchmarks;

/// <summary>
/// Helper class for benchmarking operations and generating performance reports.
/// </summary>
public static class BenchmarkHelper
{
    /// <summary>
    /// Times an asynchronous operation and returns the elapsed time.
    /// </summary>
    /// <param name="operation">The operation to time.</param>
    /// <returns>The elapsed time in milliseconds.</returns>
    public static async Task<TimeSpan> TimeOperationAsync(Func<Task> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        await operation();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>
    /// Times a synchronous operation and returns the elapsed time.
    /// </summary>
    /// <param name="operation">The operation to time.</param>
    /// <returns>The elapsed time in milliseconds.</returns>
    public static TimeSpan TimeOperation(Action operation)
    {
        var stopwatch = Stopwatch.StartNew();
        operation();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>
    /// Times an asynchronous operation multiple times and returns statistics.
    /// </summary>
    /// <param name="operation">The operation to time.</param>
    /// <param name="iterations">Number of iterations to run.</param>
    /// <returns>Performance statistics.</returns>
    public static async Task<PerformanceStats> BenchmarkOperationAsync(Func<Task> operation, int iterations = 10)
    {
        var times = new List<TimeSpan>();

        for (int i = 0; i < iterations; i++)
        {
            var elapsed = await TimeOperationAsync(operation);
            times.Add(elapsed);
        }

        return new PerformanceStats(times);
    }

    /// <summary>
    /// Times a synchronous operation multiple times and returns statistics.
    /// </summary>
    /// <param name="operation">The operation to time.</param>
    /// <param name="iterations">Number of iterations to run.</param>
    /// <returns>Performance statistics.</returns>
    public static PerformanceStats BenchmarkOperation(Action operation, int iterations = 10)
    {
        var times = new List<TimeSpan>();

        for (int i = 0; i < iterations; i++)
        {
            var elapsed = TimeOperation(operation);
            times.Add(elapsed);
        }

        return new PerformanceStats(times);
    }

    /// <summary>
    /// Generates a performance report string.
    /// </summary>
    /// <param name="stats">The performance statistics.</param>
    /// <param name="operationName">Name of the operation being benchmarked.</param>
    /// <returns>Formatted performance report.</returns>
    public static string GenerateReport(PerformanceStats stats, string operationName)
    {
        return $@"
Performance Report for: {operationName}
=====================================
Iterations: {stats.Iterations}
Average Time: {stats.Average.TotalMilliseconds:F2} ms
Min Time: {stats.Min.TotalMilliseconds:F2} ms
Max Time: {stats.Max.TotalMilliseconds:F2} ms
Total Time: {stats.Total.TotalMilliseconds:F2} ms
Operations/sec: {stats.OperationsPerSecond:F2}
";
    }

    /// <summary>
    /// Performance statistics for benchmarked operations.
    /// </summary>
    public class PerformanceStats
    {
        private readonly List<TimeSpan> _times;

        public PerformanceStats(List<TimeSpan> times)
        {
            _times = times ?? throw new ArgumentNullException(nameof(times));
        }

        public int Iterations => _times.Count;
        public TimeSpan Average => TimeSpan.FromTicks((long)_times.Average(t => t.Ticks));
        public TimeSpan Min => _times.Min();
        public TimeSpan Max => _times.Max();
        public TimeSpan Total => TimeSpan.FromTicks(_times.Sum(t => t.Ticks));
        public double OperationsPerSecond => Iterations / Total.TotalSeconds;
    }
}