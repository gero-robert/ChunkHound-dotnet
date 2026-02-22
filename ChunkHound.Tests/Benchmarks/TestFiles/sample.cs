using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Benchmark
{
    /// <summary>
    /// Sample C# class for benchmarking parsing and chunking performance.
    /// This class demonstrates various language constructs that parsers should handle.
    /// </summary>
    public class BenchmarkService
    {
        private readonly ILogger<BenchmarkService> _logger;
        private readonly Dictionary<string, object> _cache;
        private readonly List<string> _processedItems;

        /// <summary>
        /// Initializes a new instance of the BenchmarkService class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public BenchmarkService(ILogger<BenchmarkService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = new Dictionary<string, object>();
            _processedItems = new List<string>();
        }

        /// <summary>
        /// Processes a collection of items asynchronously.
        /// </summary>
        /// <param name="items">The items to process.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ProcessItemsAsync(IEnumerable<string> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var itemList = items.ToList();
            _logger.LogInformation("Processing {Count} items", itemList.Count);

            foreach (var item in itemList)
            {
                await ProcessSingleItemAsync(item);
            }

            _logger.LogInformation("Completed processing {Count} items", itemList.Count);
        }

        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="item">The item to process.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ProcessSingleItemAsync(string item)
        {
            try
            {
                // Simulate some processing work
                await Task.Delay(10);

                // Validate the item
                if (string.IsNullOrWhiteSpace(item))
                {
                    _logger.LogWarning("Skipping empty or whitespace item");
                    return;
                }

                // Check cache first
                if (_cache.ContainsKey(item))
                {
                    _logger.LogDebug("Item '{Item}' found in cache", item);
                    return;
                }

                // Process the item
                var processedItem = await TransformItemAsync(item);
                _processedItems.Add(processedItem);
                _cache[item] = processedItem;

                _logger.LogDebug("Processed item: {Item}", item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing item: {Item}", item);
                throw;
            }
        }

        /// <summary>
        /// Transforms an item by applying various operations.
        /// </summary>
        /// <param name="item">The item to transform.</param>
        /// <returns>The transformed item.</returns>
        private async Task<string> TransformItemAsync(string item)
        {
            // Simulate transformation work
            await Task.Delay(5);

            // Apply transformations
            var transformed = item
                .Trim()
                .ToUpperInvariant()
                .Replace("OLD", "NEW");

            return transformed;
        }

        /// <summary>
        /// Gets the count of processed items.
        /// </summary>
        /// <returns>The number of processed items.</returns>
        public int GetProcessedCount()
        {
            return _processedItems.Count;
        }

        /// <summary>
        /// Clears all cached and processed data.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _processedItems.Clear();
            _logger.LogInformation("Cache and processed items cleared");
        }

        /// <summary>
        /// Gets statistics about the service.
        /// </summary>
        /// <returns>A dictionary containing statistics.</returns>
        public IReadOnlyDictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["ProcessedCount"] = _processedItems.Count,
                ["CacheSize"] = _cache.Count,
                ["AverageItemLength"] = _processedItems.Any()
                    ? _processedItems.Average(item => item.Length)
                    : 0
            };
        }
    }

    /// <summary>
    /// Configuration class for benchmark settings.
    /// </summary>
    public class BenchmarkConfig
    {
        /// <summary>
        /// Gets or sets the maximum chunk size.
        /// </summary>
        public int MaxChunkSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the overlap size.
        /// </summary>
        public int Overlap { get; set; } = 100;

        /// <summary>
        /// Gets or sets the batch size for processing.
        /// </summary>
        public int BatchSize { get; set; } = 50;

        /// <summary>
        /// Gets or sets whether parallel processing is enabled.
        /// </summary>
        public bool EnableParallelProcessing { get; set; } = true;
    }

    /// <summary>
    /// Result class for benchmark operations.
    /// </summary>
    public class BenchmarkResult
    {
        /// <summary>
        /// Gets or sets the operation name.
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the elapsed time in milliseconds.
        /// </summary>
        public long ElapsedMilliseconds { get; set; }

        /// <summary>
        /// Gets or sets the number of items processed.
        /// </summary>
        public int ItemsProcessed { get; set; }

        /// <summary>
        /// Gets or sets any error that occurred.
        /// </summary>
        public string? Error { get; set; }
    }
}