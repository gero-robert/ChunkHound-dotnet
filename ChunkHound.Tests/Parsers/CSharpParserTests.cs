using Xunit;
using ChunkHound.Parsers.Concrete;
using ChunkHound.Core;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

namespace ChunkHound.Tests.Parsers
{
    public class CSharpParserTests
    {
        private readonly CSharpParser _parser;

        public CSharpParserTests()
        {
            _parser = new CSharpParser();
        }

        /// <summary>
        /// Tests that CSharpParser correctly parses C# files using Roslyn,
        /// creating chunks for classes and methods with appropriate metadata.
        /// Verifies chunk content, line numbers, and metadata fields.
        /// </summary>
        [Fact]
        public async Task ParseAsync_ValidCSharpFile_ReturnsClassAndMethodChunks()
        {
            // Arrange
            var testFile = Path.GetTempFileName();
            var content = @"using System;

namespace TestNamespace
{
    public class TestClass
    {
        private readonly string _field;

        public TestClass(string field)
        {
            _field = field;
        }

        public void TestMethod()
        {
            Console.WriteLine(""Hello World"");
        }

        public int Calculate(int a, int b)
        {
            return a + b;
        }
    }
}";
            await System.IO.File.WriteAllTextAsync(testFile, content);

            var file = new Core.File(
                id: 1,
                path: testFile,
                mtime: 1234567890,
                language: Language.CSharp,
                sizeBytes: content.Length
            );

            try
            {
                // Act
                var chunks = await _parser.ParseAsync(file);

                // Assert
                Assert.NotNull(chunks);
                Assert.NotEmpty(chunks);

                // Should have class chunk
                var classChunk = chunks.FirstOrDefault(c => c.Metadata.ContainsKey("type") && c.Metadata["type"].ToString() == "class");
                Assert.NotNull(classChunk);
                Assert.Equal("TestClass", classChunk.Metadata["name"].ToString());
                Assert.Equal(4, classChunk.StartLine); // class starts at line 5
                Assert.Equal(22, classChunk.EndLine); // class ends at line 22

                // Should have method chunks
                var methodChunks = chunks.Where(c => c.Metadata.ContainsKey("type") && c.Metadata["type"].ToString() == "method").ToList();
                Assert.Equal(2, methodChunks.Count);

                var testMethod = methodChunks.FirstOrDefault(m => m.Metadata["name"].ToString() == "TestMethod");
                Assert.NotNull(testMethod);
                Assert.Equal("void", testMethod.Metadata["return"].ToString());

                var calculateMethod = methodChunks.FirstOrDefault(m => m.Metadata["name"].ToString() == "Calculate");
                Assert.NotNull(calculateMethod);
                Assert.Equal("int", calculateMethod.Metadata["return"].ToString());
            }
            finally
            {
                // Cleanup
                System.IO.File.Delete(testFile);
            }
        }
    }
}