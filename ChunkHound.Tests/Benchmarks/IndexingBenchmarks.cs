using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ChunkHound.Providers;
using ChunkHound.Services;
using ChunkHound.Core;
using ChunkHound.Parsers;
using ChunkHound.Parsers.Concrete;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

// Performance benchmarks comparing C# implementation to Python baseline
// These benchmarks measure key operations for indexing performance

namespace ChunkHound.Core.Tests.Benchmarks
{
    [SimpleJob]
    [MemoryDiagnoser]
    public class IndexingBenchmarks
    {
        private FakeConstantEmbeddingProvider _embeddingProvider = null!;
        private FakeDatabaseProvider _databaseProvider = null!;
        private LanguageConfigProvider _languageConfigProvider = null!;
        private UniversalParser _parser = null!;
        private List<Chunk> _testChunks = null!;
        private List<string> _testTexts = null!;

        // Test files for parsing benchmarks
        private string _markdownFile = null!;
        private string _yamlFile = null!;
        private string _vueFile = null!;
        private string _csharpFile = null!;

        [GlobalSetup]
        public void Setup()
        {
            _embeddingProvider = new FakeConstantEmbeddingProvider();
            _databaseProvider = new FakeDatabaseProvider();
            _languageConfigProvider = new LanguageConfigProvider();

            // Create parser factory and splitter for UniversalParser
            var parsers = new List<IChunkParser>
            {
                new MarkdownParser(),
                new RapidYamlParser(),
                new VueChunkParser(),
                new CodeChunkParser(),
                new UniversalTextParser()
            };
            var parserFactory = new ParserFactory(parsers);
            var splitter = new RecursiveChunkSplitter(_languageConfigProvider);
            _parser = new UniversalParser(NullLogger<UniversalParser>.Instance, _languageConfigProvider, new Dictionary<Language, IUniversalParser>(), parserFactory, splitter);

            // Generate test data
            _testChunks = GenerateTestChunks(1000);
            _testTexts = GenerateTestTexts(1000);

            // Set up test files
            var testFilesDir = Path.Combine(Directory.GetCurrentDirectory(), "ChunkHound.Tests", "Benchmarks", "TestFiles");
            _markdownFile = Path.Combine(testFilesDir, "sample.md");
            _yamlFile = Path.Combine(testFilesDir, "sample.yaml");
            _vueFile = Path.Combine(testFilesDir, "sample.vue");
            _csharpFile = Path.Combine(testFilesDir, "sample.cs");
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
        public async Task FileParsing_MarkdownFile()
        {
            var testFile = new File(_markdownFile, 1234567890, Language.Unknown, 1000, null, "hash");
            await _parser.ParseAsync(testFile);
        }

        [Benchmark]
        public async Task FileParsing_YamlFile()
        {
            var testFile = new File(_yamlFile, 1234567890, Language.Yaml, 1000, null, "hash");
            await _parser.ParseAsync(testFile);
        }

        [Benchmark]
        public async Task FileParsing_VueFile()
        {
            var testFile = new File(_vueFile, 1234567890, Language.Unknown, 1000, null, "hash");
            await _parser.ParseAsync(testFile);
        }

        [Benchmark]
        public async Task FileParsing_CSharpFile()
        {
            var testFile = new File(_csharpFile, 1234567890, Language.CSharp, 1000, null, "hash");
            await _parser.ParseAsync(testFile);
        }

        [Benchmark]
        public async Task EndToEndParsingAndEmbedding_Markdown()
        {
            var testFile = new File(_markdownFile, 1234567890, Language.Unknown, 1000, null, "hash");
            var chunks = await _parser.ParseAsync(testFile);
            var texts = chunks.Select(c => c.Code).ToList();
            await _embeddingProvider.EmbedAsync(texts);
        }

        [Benchmark]
        public async Task EndToEndParsingAndEmbedding_Yaml()
        {
            var testFile = new File(_yamlFile, 1234567890, Language.Yaml, 1000, null, "hash");
            var chunks = await _parser.ParseAsync(testFile);
            var texts = chunks.Select(c => c.Code).ToList();
            await _embeddingProvider.EmbedAsync(texts);
        }

        [Benchmark]
        public async Task EndToEndParsingAndEmbedding_Vue()
        {
            var testFile = new File(_vueFile, 1234567890, Language.Unknown, 1000, null, "hash");
            var chunks = await _parser.ParseAsync(testFile);
            var texts = chunks.Select(c => c.Code).ToList();
            await _embeddingProvider.EmbedAsync(texts);
        }

        [Benchmark]
        public async Task EndToEndParsingAndEmbedding_CSharp()
        {
            var testFile = new File(_csharpFile, 1234567890, Language.CSharp, 1000, null, "hash");
            var chunks = await _parser.ParseAsync(testFile);
            var texts = chunks.Select(c => c.Code).ToList();
            await _embeddingProvider.EmbedAsync(texts);
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
                    id: Guid.NewGuid().ToString(),
                    fileId: 1,
                    content: $"public void Function{i}() {{ Console.WriteLine(\"{i}\"); }}",
                    startLine: 1,
                    endLine: 10,
                    language: Language.CSharp,
                    chunkType: ChunkType.Function,
                    symbol: $"Function{i}"));
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