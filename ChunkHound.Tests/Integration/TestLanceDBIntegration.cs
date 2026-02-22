using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using ChunkHound.Providers;
using ChunkHound.Core;
using ChunkHound.Core.Utilities;
using Microsoft.Extensions.Logging;

/// <summary>
/// Simple integration test for LanceDB pythonnet integration.
/// Run this manually after installing Python and required packages.
/// </summary>
public class TestLanceDBIntegration
{
    public static async Task RunTest()
    {
        Console.WriteLine("Testing LanceDB pythonnet integration...");

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var logger = loggerFactory.CreateLogger<LanceDBProvider>();

        var testDbPath = Path.Combine(Path.GetTempPath(), "ChunkHoundIntegrationTest", Guid.NewGuid().ToString());

        try
        {
            // Create provider
            Console.WriteLine("Creating LanceDBProvider...");
            var provider = new LanceDBProvider(testDbPath, logger);

            // Initialize
            Console.WriteLine("Initializing database...");
            await provider.InitializeAsync();

            // Create test chunks
            Console.WriteLine("Creating test chunks...");
            var chunks = new List<Chunk>
            {
                new Chunk("TestFunction", 1, 10, "public void Test() { Console.WriteLine(\"Hello\"); }", ChunkType.Function, 1, Language.CSharp),
                new Chunk("TestClass", 15, 25, "public class TestClass { }", ChunkType.Class, 1, Language.CSharp)
            };

            // Store chunks
            Console.WriteLine("Storing chunks...");
            var ids = await provider.StoreChunksAsync(chunks);
            Console.WriteLine($"Stored {ids.Count} chunks with IDs: {string.Join(", ", ids)}");

            // Retrieve by hashes
            Console.WriteLine("Retrieving chunks by hash...");
            var hash = HashUtility.ComputeContentHash(chunks[0].Code);
            var retrieved = await provider.GetChunksByHashesAsync(new List<string> { hash });
            Console.WriteLine($"Retrieved {retrieved.Count} chunks by hash");

            // Test embeddings
            Console.WriteLine("Testing embeddings insertion...");
            var embeddingsData = new List<EmbeddingData>
            {
                new EmbeddingData(ids[0], "test-provider", "test-model", 1536,
                    new List<float> { 0.1f, 0.2f, 0.3f }, "success")
            };
            await provider.InsertEmbeddingsBatchAsync(embeddingsData, new Dictionary<long, string>());

            // Test fragment count
            Console.WriteLine("Getting fragment counts...");
            var fragments = await provider.GetFragmentCountAsync();
            Console.WriteLine($"Fragment counts: {string.Join(", ", fragments.Select(kv => $"{kv.Key}={kv.Value}"))}");

            // Optimize
            Console.WriteLine("Optimizing tables...");
            await provider.OptimizeTablesAsync();

            Console.WriteLine("✅ All tests passed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDbPath))
            {
                Directory.Delete(testDbPath, true);
            }
        }
    }
}