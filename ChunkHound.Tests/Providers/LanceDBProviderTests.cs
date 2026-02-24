using Xunit;
using Xunit.Abstractions;
using Serilog;
using Serilog.Events;
using ChunkHound.Providers;
using ChunkHound.Core;
using ChunkHound.Core.Tests.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Python.Runtime;

namespace ChunkHound.Core.Tests.Providers
{
    [CollectionDefinition("LanceDB", DisableParallelization = true)]
    public class LanceDBCollectionDefinition { }

    [Collection("LanceDB")]
    public class LanceDBProviderTests
    {
        public LanceDBProviderTests(ITestOutputHelper output)
        {
            string testLogFile = Path.Combine("ChunkHound", "logs", "lancedb-tests.log");
            if (System.IO.File.Exists(testLogFile)) System.IO.File.Delete(testLogFile);
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(testLogFile)
                .MinimumLevel.Debug()
                .CreateLogger();
            var testLoggerFactory = new LoggerFactory().AddSerilog();
            _testLogger = testLoggerFactory.CreateLogger<LanceDBProviderTests>();
        }

        private readonly ILogger<LanceDBProviderTests> _testLogger;

        // ONLY A SINGLE PROVIDER IS ALLOWED TO BE CREATED, otherwise tests will hang.
        private (string dbPath, LanceDBProvider provider) CreateTestProvider()
        {
            string tempDbPath = Path.Combine(Path.GetTempPath(), "ChunkHoundTestDB", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDbPath);
            LanceDBProvider provider = new LanceDBProvider(tempDbPath, _testLogger);
            return (tempDbPath, provider);
        }

        // run all tests from a single test method to share the proivder, this is to overcome a limitation in creating multiple
        // python lancedb instances which causes the test to hang while waiting for GIL. 
        [Fact]
        public async Task RunAllTests()
        {
            // Skip if Python is not available
            if (!PythonTestHelper.IsPythonAvailable())
            {
                return; // Skip test gracefully
            }

            string logFile = Path.Combine("ChunkHound", "logs", "lancedb-provider-test.log");
            if (System.IO.File.Exists(logFile)) System.IO.File.Delete(logFile);

            var (dbPath, provider) = CreateTestProvider();

            try
            {
                _testLogger.LogInformation("Starting TestInitializeAsyncCompletesSuccessfully");
                await TestInitializeAsyncCompletesSuccessfully(provider);
                _testLogger.LogInformation("Completed TestInitializeAsyncCompletesSuccessfully");

                _testLogger.LogInformation("Starting ClearAllDataAsync after TestInitializeAsyncCompletesSuccessfully");
                await provider.ClearAllDataAsync();
                _testLogger.LogInformation("Completed ClearAllDataAsync after TestInitializeAsyncCompletesSuccessfully");

                _testLogger.LogInformation("Starting TestInitializeAsyncCalledTwiceOnlyInitializesOnce");
                await TestInitializeAsyncCalledTwiceOnlyInitializesOnce(provider);
                _testLogger.LogInformation("Completed TestInitializeAsyncCalledTwiceOnlyInitializesOnce");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestStoreChunksAsyncEmptyListReturnsEmptyList");
                await TestStoreChunksAsyncEmptyListReturnsEmptyList(provider);
                _testLogger.LogInformation("Completed TestStoreChunksAsyncEmptyListReturnsEmptyList");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestStoreChunksAsyncWithChunksReturnsIds");
                await TestStoreChunksAsyncWithChunksReturnsIds(provider);
                _testLogger.LogInformation("Completed TestStoreChunksAsyncWithChunksReturnsIds");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestStoreChunksAsyncWithExistingIdsPreservesIds");
                await TestStoreChunksAsyncWithExistingIdsPreservesIds(provider);
                _testLogger.LogInformation("Completed TestStoreChunksAsyncWithExistingIdsPreservesIds");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestGetChunksByHashesAsyncEmptyListReturnsEmptyList");
                await TestGetChunksByHashesAsyncEmptyListReturnsEmptyList(provider);
                _testLogger.LogInformation("Completed TestGetChunksByHashesAsyncEmptyListReturnsEmptyList");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestGetChunksByHashesAsyncNotInitializedThrowsInvalidOperationException");
                await TestGetChunksByHashesAsyncNotInitializedThrowsInvalidOperationException(provider);
                _testLogger.LogInformation("Completed TestGetChunksByHashesAsyncNotInitializedThrowsInvalidOperationException");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestGetFragmentCountAsyncReturnsDictionary");
                await TestGetFragmentCountAsyncReturnsDictionary(provider);
                _testLogger.LogInformation("Completed TestGetFragmentCountAsyncReturnsDictionary");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestInsertChunksBatchAsyncCallsStoreChunksAsync");
                await TestInsertChunksBatchAsyncCallsStoreChunksAsync(provider);
                _testLogger.LogInformation("Completed TestInsertChunksBatchAsyncCallsStoreChunksAsync");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestFilterExistingEmbeddingsAsyncReturnsEmptyList");
                await TestFilterExistingEmbeddingsAsyncReturnsEmptyList(provider);
                _testLogger.LogInformation("Completed TestFilterExistingEmbeddingsAsyncReturnsEmptyList");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestInsertEmbeddingsBatchAsyncReturnsEmbeddingsCount");
                await TestInsertEmbeddingsBatchAsyncReturnsEmbeddingsCount(provider);
                _testLogger.LogInformation("Completed TestInsertEmbeddingsBatchAsyncReturnsEmbeddingsCount");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestInsertEmbeddingsBatchAsyncWithEmbeddingDataCompletesSuccessfully");
                await TestInsertEmbeddingsBatchAsyncWithEmbeddingDataCompletesSuccessfully(provider);
                _testLogger.LogInformation("Completed TestInsertEmbeddingsBatchAsyncWithEmbeddingDataCompletesSuccessfully");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestDeleteEmbeddingsForChunksAsyncCompletesSuccessfully");
                await TestDeleteEmbeddingsForChunksAsyncCompletesSuccessfully(provider);
                _testLogger.LogInformation("Completed TestDeleteEmbeddingsForChunksAsyncCompletesSuccessfully");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestGetChunksByFilePathAsyncReturnsEmptyList");
                await TestGetChunksByFilePathAsyncReturnsEmptyList(provider);
                _testLogger.LogInformation("Completed TestGetChunksByFilePathAsyncReturnsEmptyList");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestGetChunksByIdsAsyncReturnsEmptyList");
                await TestGetChunksByIdsAsyncReturnsEmptyList(provider);
                _testLogger.LogInformation("Completed TestGetChunksByIdsAsyncReturnsEmptyList");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestGetFileByPathAsyncReturnsNull");
                await TestGetFileByPathAsyncReturnsNull(provider);
                _testLogger.LogInformation("Completed TestGetFileByPathAsyncReturnsNull");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestUpsertFileAsyncReturnsFileId");
                await TestUpsertFileAsyncReturnsFileId(provider);
                _testLogger.LogInformation("Completed TestUpsertFileAsyncReturnsFileId");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestUpsertFileAsyncWithExistingIdReturnsExistingId");
                await TestUpsertFileAsyncWithExistingIdReturnsExistingId(provider);
                _testLogger.LogInformation("Completed TestUpsertFileAsyncWithExistingIdReturnsExistingId");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestOptimizeTablesAsyncCompletesSuccessfully");
                await TestOptimizeTablesAsyncCompletesSuccessfully(provider);
                _testLogger.LogInformation("Completed TestOptimizeTablesAsyncCompletesSuccessfully");
                await provider.ClearAllDataAsync();

                _testLogger.LogInformation("Starting TestClearAllDataAsyncCompletesSuccessfully");
                await TestClearAllDataAsyncCompletesSuccessfully(provider);
                _testLogger.LogInformation("Completed TestClearAllDataAsyncCompletesSuccessfully");
            }
            catch (Exception ex)
            {
                _testLogger.LogError(ex.ToString());
            }
            finally
            {
                provider.Dispose();
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
        }

        /// <summary>
        /// Tests the successful initialization of the LanceDB provider, verifying that InitializeAsync completes without exceptions
        /// and logs the initialization success message.
        /// </summary>
        private async Task TestInitializeAsyncCompletesSuccessfully(LanceDBProvider provider)
        {
            // Act
            await provider.InitializeAsync();

            // Assert - No exception thrown
        }

        /// <summary>
        /// Tests that calling InitializeAsync multiple times only initializes the database once, ensuring idempotent behavior
        /// and verifying that the initialization log is only written on the first call.
        /// </summary>
        private async Task TestInitializeAsyncCalledTwiceOnlyInitializesOnce(LanceDBProvider provider)
        {
            // Act
            await provider.InitializeAsync();
            await provider.InitializeAsync();

            // Assert - Idempotent initialization (log called only once)
        }

        /// <summary>
        /// Tests that StoreChunksAsync returns an empty list when provided with an empty list of chunks,
        /// ensuring no chunks are stored and no IDs are generated for empty input.
        /// </summary>
        private async Task TestStoreChunksAsyncEmptyListReturnsEmptyList(LanceDBProvider provider)
        {
            // Act
            var ids = await provider.StoreChunksAsync(new List<Chunk>());

            // Assert
            Assert.Empty(ids);
        }

        /// <summary>
        /// Tests that StoreChunksAsync correctly stores a list of chunks and returns sequential IDs starting from 1,
        /// verifying the chunk storage functionality with valid chunk data.
        /// </summary>
        private async Task TestStoreChunksAsyncWithChunksReturnsIds(LanceDBProvider provider)
        {
            var chunks = new List<Chunk>
            {
                new Chunk("test1", 1, 10, "code1", ChunkType.Function, 1, Language.CSharp),
                new Chunk("test2", 1, 10, "code2", ChunkType.Function, 1, Language.CSharp)
            };

            // Act
            var ids = await provider.StoreChunksAsync(chunks);

            // Assert
            Assert.Equal(2, ids.Count);
            Assert.Equal(1, ids[0]);
            Assert.Equal(2, ids[1]);
        }

        /// <summary>
        /// Tests that StoreChunksAsync preserves existing IDs when chunks already have IDs assigned,
        /// ensuring that pre-assigned IDs are not overwritten during storage.
        /// </summary>
        private async Task TestStoreChunksAsyncWithExistingIdsPreservesIds(LanceDBProvider provider)
        {
            var chunk = new Chunk("999", 1, "code", 1, 10, Language.CSharp, ChunkType.Function, "test");

            // Act
            var ids = await provider.StoreChunksAsync(new List<Chunk> { chunk });

            // Assert
            Assert.Single(ids);
            Assert.Equal(999, ids[0]);
        }

        /// <summary>
        /// Tests that GetChunksByHashesAsync returns an empty list when provided with an empty list of hashes,
        /// ensuring no database queries are performed for empty input.
        /// </summary>
        private async Task TestGetChunksByHashesAsyncEmptyListReturnsEmptyList(LanceDBProvider provider)
        {
            // Act
            var chunks = await provider.GetChunksByHashesAsync(new List<string>());

            // Assert
            Assert.Empty(chunks);
        }

        /// <summary>
        /// Tests that GetChunksByHashesAsync throws InvalidOperationException when called on a non-initialized provider,
        /// ensuring proper error handling for uninitialized database state. This test creates a new provider instance
        /// without calling InitializeAsync to simulate the uninitialized state.
        /// </summary>
        private async Task TestGetChunksByHashesAsyncNotInitializedThrowsInvalidOperationException(LanceDBProvider provider)
        {
            // Create a new provider without initializing to test uninitialized state
            string tempDbPath = Path.Combine(Path.GetTempPath(), "ChunkHoundTestDB", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDbPath);
            LanceDBProvider nonInitializedProvider = new LanceDBProvider(tempDbPath, _testLogger);

            try
            {
                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    nonInitializedProvider.GetChunksByHashesAsync(new List<string> { "hash" }));
            }
            finally
            {
                nonInitializedProvider.Dispose();
                if (Directory.Exists(tempDbPath))
                    Directory.Delete(tempDbPath, true);
            }
        }

        /// <summary>
        /// Tests that GetFragmentCountAsync returns a dictionary with 'chunks' and 'files' keys both set to 0
        /// for an empty database, verifying the fragment count retrieval functionality.
        /// </summary>
        private async Task TestGetFragmentCountAsyncReturnsDictionary(LanceDBProvider provider)
        {
            // Act
            var counts = await provider.GetFragmentCountAsync();

            // Assert
            Assert.NotNull(counts);
            Assert.Contains("chunks", counts.Keys);
            Assert.Contains("files", counts.Keys);
            Assert.Equal(0, counts["chunks"]);
            Assert.Equal(0, counts["files"]);
        }

        /// <summary>
        /// Tests that InsertChunksBatchAsync delegates to StoreChunksAsync and returns the correct IDs,
        /// ensuring batch insertion works as expected for chunk data.
        /// </summary>
        private async Task TestInsertChunksBatchAsyncCallsStoreChunksAsync(LanceDBProvider provider)
        {
            var chunks = new List<Chunk>
            {
                new Chunk("test", 1, 10, "code", ChunkType.Function, 1, Language.CSharp)
            };

            // Act
            var ids = await provider.InsertChunksBatchAsync(chunks);

            // Assert
            Assert.Single(ids);
            Assert.Equal(1, ids[0]);
        }

        /// <summary>
        /// Tests that InsertEmbeddingsBatchAsync correctly inserts embeddings and returns the count of inserted embeddings,
        /// verifying the batch embedding insertion functionality.
        /// </summary>
        private async Task TestInsertEmbeddingsBatchAsyncReturnsEmbeddingsCount(LanceDBProvider provider)
        {
            var chunkIds = new List<int> { 1, 2 };
            var embeddings = new List<List<float>>
            {
                new List<float> { 0.1f, 0.2f },
                new List<float> { 0.3f, 0.4f }
            };

            // Act
            var result = await provider.InsertEmbeddingsBatchAsync(chunkIds, embeddings);

            // Assert
            Assert.Equal(2, result);
        }

        /// <summary>
        /// Tests that FilterExistingEmbeddingsAsync returns an empty list when no embeddings exist for the given parameters,
        /// ensuring proper filtering of existing embeddings.
        /// </summary>
        private async Task TestFilterExistingEmbeddingsAsyncReturnsEmptyList(LanceDBProvider provider)
        {
            var chunkIds = new List<long> { 1, 2, 3 };

            // Act
            var result = await provider.FilterExistingEmbeddingsAsync(chunkIds, "provider", "model");

            // Assert
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that InsertEmbeddingsBatchAsync with EmbeddingData completes successfully without exceptions,
        /// verifying the embedding data insertion functionality.
        /// </summary>
        private async Task TestInsertEmbeddingsBatchAsyncWithEmbeddingDataCompletesSuccessfully(LanceDBProvider provider)
        {
            var embeddingsData = new List<EmbeddingData>
            {
                new EmbeddingData(1, "provider", "model", 1536, new List<float> { 0.1f }, "success")
            };
            var chunkIdToStatus = new Dictionary<long, string> { { 1, "success" } };

            // Act
            await provider.InsertEmbeddingsBatchAsync(embeddingsData, chunkIdToStatus);

            // Assert - No exception thrown
        }

        /// <summary>
        /// Tests that DeleteEmbeddingsForChunksAsync completes successfully without exceptions,
        /// ensuring embeddings can be deleted for specified chunks and parameters.
        /// </summary>
        private async Task TestDeleteEmbeddingsForChunksAsyncCompletesSuccessfully(LanceDBProvider provider)
        {
            var chunkIds = new List<long> { 1, 2, 3 };

            // Act
            await provider.DeleteEmbeddingsForChunksAsync(chunkIds, "provider", "model");

            // Assert - No exception thrown
        }

        /// <summary>
        /// Tests that GetChunksByFilePathAsync returns an empty list when no chunks exist for the given file path,
        /// verifying the file path-based chunk retrieval functionality.
        /// </summary>
        private async Task TestGetChunksByFilePathAsyncReturnsEmptyList(LanceDBProvider provider)
        {
            // Act
            var chunks = await provider.GetChunksByFilePathAsync("/test/file.cs");

            // Assert
            Assert.Empty(chunks);
        }

        /// <summary>
        /// Tests that GetChunksByIdsAsync returns an empty list when no chunks exist for the given IDs,
        /// ensuring proper handling of non-existent chunk IDs.
        /// </summary>
        private async Task TestGetChunksByIdsAsyncReturnsEmptyList(LanceDBProvider provider)
        {
            var chunkIds = new List<long> { 1, 2, 3 };

            // Act
            var chunks = await provider.GetChunksByIdsAsync(chunkIds);

            // Assert
            Assert.Empty(chunks);
        }

        /// <summary>
        /// Tests that GetFileByPathAsync returns null when no file exists for the given path,
        /// verifying the file retrieval functionality for non-existent files.
        /// </summary>
        private async Task TestGetFileByPathAsyncReturnsNull(LanceDBProvider provider)
        {
            // Act
            var file = await provider.GetFileByPathAsync("/test/file.cs");

            // Assert
            Assert.Null(file);
        }

        /// <summary>
        /// Tests that UpsertFileAsync inserts a new file and returns a generated ID (1),
        /// ensuring file upsertion works correctly for new files.
        /// </summary>
        private async Task TestUpsertFileAsyncReturnsFileId(LanceDBProvider provider)
        {
            var file = new File("/test/file.cs", 1234567890, Language.CSharp, 100, null, "hash123");

            // Act
            var id = await provider.UpsertFileAsync(file);

            // Assert
            Assert.Equal(1, id);
        }

        /// <summary>
        /// Tests that UpsertFileAsync preserves an existing ID when provided,
        /// ensuring that pre-assigned file IDs are not overwritten.
        /// </summary>
        private async Task TestUpsertFileAsyncWithExistingIdReturnsExistingId(LanceDBProvider provider)
        {
            var file = new File("/test/file.cs", 1234567890, Language.CSharp, 100, 42, "hash123");

            // Act
            var id = await provider.UpsertFileAsync(file);

            // Assert
            Assert.Equal(42, id);
        }

        /// <summary>
        /// Tests that OptimizeTablesAsync completes successfully without exceptions,
        /// verifying the table optimization functionality.
        /// </summary>
        private async Task TestOptimizeTablesAsyncCompletesSuccessfully(LanceDBProvider provider)
        {
            // Act
            await provider.OptimizeTablesAsync();

            // Assert - No exception thrown
        }

        /// <summary>
        /// Tests that ClearAllDataAsync clears all data, recreates tables, and logs the operation,
        /// ensuring complete data reset functionality.
        /// </summary>
        private async Task TestClearAllDataAsyncCompletesSuccessfully(LanceDBProvider provider)
        {
            // Add some data first
            var chunks = new List<Chunk>
            {
                new Chunk("test", 1, 10, "code", ChunkType.Function, 1, Language.CSharp)
            };
            await provider.StoreChunksAsync(chunks);

            // Act
            await provider.ClearAllDataAsync();

            // Assert - Data cleared successfully
            var counts = await provider.GetFragmentCountAsync();
            Assert.Equal(0, counts["chunks"]);
            Assert.Equal(0, counts["files"]);
        }


    }
}