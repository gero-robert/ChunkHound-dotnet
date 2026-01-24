using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ChunkHound.Providers;
using ChunkHound.Services;
using ChunkHound.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Threading.Tasks;

// Performance benchmarks comparing C# implementation to Python baseline
// These benchmarks measure key operations for indexing performance

namespace ChunkHound.Core.Tests.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    public class IndexingBenchmarks
    {
        private FakeConstantEmbeddingProvider _embeddingProvider = null!;
        private FakeDatabaseProvider _databaseProvider = null!;
        private LanguageConfigProvider _languageConfigProvider = null!;
        private UniversalParser _parser = null!;
        private List<Chunk> _testChunks = null!;
        private List<string> _testTexts = null!;

        [GlobalSetup]
        public void Setup()
        {
            _embeddingProvider = new FakeConstantEmbeddingProvider();
            _databaseProvider = new FakeDatabaseProvider();
            _languageConfigProvider = new LanguageConfigProvider();
            _parser = new UniversalParser(NullLogger<UniversalParser>.Instance, _languageConfigProvider);

            // Generate test data
            _testChunks = GenerateTestChunks(1000);
            _testTexts = GenerateTestTexts(1000);
        }

        [Benchmark]
        public async Task EmbeddingGeneration_1000Texts()
        {
            await _embeddingProvider.EmbedAsync(_testTexts);
        }

        [Benchmark]
        public async Task ChunkStorage_1000Chunks()
        {
            await _databaseProvider.StoreChunksAsync(_testChunks);
        }

        [Benchmark]
        public async Task ChunkRetrieval_ByHashes()
        {
            var hashes = _testChunks.Select(c => ChunkHound.Core.Utilities.HashUtility.ComputeContentHash(c.Code)).ToList();
            await _databaseProvider.GetChunksByHashesAsync(hashes);
        }

        [Benchmark]
        public async Task FileParsing_CSharpFile()
        {
            var testFile = new File("/test/file.cs", 1234567890, Language.CSharp, 1000, null, "hash");
            var code = GenerateCSharpCode();
            // Note: This would require actual parsing implementation
            // await _parser.ParseAsync(testFile);
        }

        [Benchmark]
        public void HashComputation_1000Strings()
        {
            foreach (var text in _testTexts)
            {
                ChunkHound.Core.Utilities.HashUtility.ComputeContentHash(text);
            }
        }

        [Benchmark]
        public async Task BatchEmbeddingProcessing()
        {
            // Simulate batch processing like in production
            var batchSize = 100;
            for (int i = 0; i < _testTexts.Count; i += batchSize)
            {
                var batch = _testTexts.Skip(i).Take(batchSize).ToList();
                await _embeddingProvider.EmbedAsync(batch);
            }
        }

        private List<Chunk> GenerateTestChunks(int count)
        {
            var chunks = new List<Chunk>();
            for (int i = 0; i < count; i++)
            {
                chunks.Add(new Chunk(
                    symbol: $"Function{i}",
                    startLine: 1,
                    endLine: 10,
                    code: $"public void Function{i}() {{ Console.WriteLine(\"{i}\"); }}",
                    chunkType: ChunkType.Function,
                    fileId: 1,
                    language: Language.CSharp));
            }
            return chunks;
        }

        private List<string> GenerateTestTexts(int count)
        {
            var texts = new List<string>();
            for (int i = 0; i < count; i++)
            {
                texts.Add($"This is test text number {i} with some content to embed.");
            }
            return texts;
        }

        private string GenerateCSharpCode()
        {
            return @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    public class TestClass
    {
        private readonly List<string> _items;

        public TestClass()
        {
            _items = new List<string>();
        }

        public void AddItem(string item)
        {
            _items.Add(item);
        }

        public IEnumerable<string> GetItems()
        {
            return _items;
        }

        public async Task ProcessItemsAsync()
        {
            foreach (var item in _items)
            {
                await Task.Delay(1);
                Console.WriteLine(item);
            }
        }
    }
}";
        }
    }

    // Additional benchmark for comparing different embedding providers
    public class EmbeddingProviderBenchmarks
    {
        private FakeConstantEmbeddingProvider _fakeProvider = null!;
        private List<string> _testTexts = null!;

        [GlobalSetup]
        public void Setup()
        {
            _fakeProvider = new FakeConstantEmbeddingProvider();
            _testTexts = new List<string>();
            for (int i = 0; i < 100; i++)
            {
                _testTexts.Add($"Test text {i} for embedding performance measurement.");
            }
        }

        [Benchmark]
        public async Task FakeConstantEmbedding_100Texts()
        {
            await _fakeProvider.EmbedAsync(_testTexts);
        }

        [Benchmark]
        public async Task FakeConstantEmbedding_SingleLargeBatch()
        {
            await _fakeProvider.EmbedAsync(_testTexts);
        }
    }
}