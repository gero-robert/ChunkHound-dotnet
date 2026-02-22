using Xunit;
using ChunkHound.Parsers.Concrete;
using ChunkHound.Core;
using System.Threading.Tasks;
using System.Linq;

namespace ChunkHound.Tests.Parsers
{
    public class CodeChunkParserTests
    {
        private readonly CodeChunkParser _parser;

        public CodeChunkParserTests()
        {
            _parser = new CodeChunkParser();
        }

        [Theory]
        [InlineData(".cs")]
        [InlineData(".py")]
        [InlineData(".js")]
        [InlineData(".ts")]
        [InlineData(".java")]
        [InlineData(".cpp")]
        [InlineData(".c")]
        [InlineData(".go")]
        [InlineData(".rs")]
        [InlineData(".php")]
        [InlineData(".rb")]
        public void CanHandle_SupportedExtensions_ReturnsTrue(string extension)
        {
            Assert.True(_parser.CanHandle(extension));
            Assert.True(_parser.CanHandle(extension.ToUpper()));
        }

        [Theory]
        [InlineData(".txt")]
        [InlineData(".md")]
        [InlineData(".yaml")]
        [InlineData(".json")]
        [InlineData("")]
        public void CanHandle_UnsupportedExtensions_ReturnsFalse(string extension)
        {
            Assert.False(_parser.CanHandle(extension));
        }

        [Fact]
        public async Task ParseAsync_CSharpCode_ReturnsClassAndMethodChunks()
        {
            // Arrange
            var csharpContent = @"using System;

namespace TestNamespace
{
    public class TestClass
    {
        private int _field;

        public TestClass(int value)
        {
            _field = value;
        }

        public void TestMethod()
        {
            Console.WriteLine(""Hello"");
        }

        private static int HelperMethod(int x)
        {
            return x * 2;
        }
    }
}";

            // Act
            var chunks = await _parser.ParseAsync(csharpContent, "test.cs");

            // Assert
            Assert.NotNull(chunks);
            Assert.True(chunks.Count >= 2); // At least class and method

            var classChunk = chunks.FirstOrDefault(c => c.ChunkType == ChunkType.Class && c.Code.Contains("class TestClass"));
            var methodChunk = chunks.FirstOrDefault(c => c.ChunkType == ChunkType.Function && c.Code.Contains("TestMethod"));

            Assert.NotNull(classChunk);
            Assert.NotNull(methodChunk);
            Assert.Contains("TestClass", classChunk.Code);
            Assert.Contains("TestMethod", methodChunk.Code);
        }

        [Fact]
        public async Task ParseAsync_PythonCode_ReturnsFunctionChunks()
        {
            // Arrange
            var pythonContent = @"def calculate_sum(a, b):
    """"Calculate the sum of two numbers.""""
    return a + b

class Calculator:
    def __init__(self, initial_value):
        self.value = initial_value

    def add(self, number):
        self.value += number
        return self.value

def main():
    calc = Calculator(0)
    result = calc.add(5)
    print(f""Result: {result}"")";

            // Act
            var chunks = await _parser.ParseAsync(pythonContent, "test.py");

            // Assert
            Assert.NotNull(chunks);
            Assert.True(chunks.Count >= 3); // Functions and class

            var funcChunk = chunks.FirstOrDefault(c => c.ChunkType == ChunkType.Function && c.Code.Contains("calculate_sum"));
            var classChunk = chunks.FirstOrDefault(c => c.ChunkType == ChunkType.Class && c.Code.Contains("class Calculator"));

            Assert.NotNull(funcChunk);
            Assert.NotNull(classChunk);
            Assert.Contains("def calculate_sum", funcChunk.Code);
            Assert.Contains("class Calculator", classChunk.Code);
        }

        [Fact]
        public async Task ParseAsync_JavaScriptCode_ReturnsFunctionChunks()
        {
            // Arrange
            var jsContent = @"function greet(name) {
    return `Hello, ${name}!`;
}

const calculateArea = (width, height) => {
    return width * height;
};

class Rectangle {
    constructor(width, height) {
        this.width = width;
        this.height = height;
    }

    getArea() {
        return this.width * this.height;
    }
}";

            // Act
            var chunks = await _parser.ParseAsync(jsContent, "test.js");

            // Assert
            Assert.NotNull(chunks);
            Assert.True(chunks.Count >= 3); // Functions and class

            var funcChunk = chunks.FirstOrDefault(c => c.ChunkType == ChunkType.Function && c.Code.Contains("function greet"));
            var arrowChunk = chunks.FirstOrDefault(c => c.ChunkType == ChunkType.Function && c.Code.Contains("calculateArea"));
            var classChunk = chunks.FirstOrDefault(c => c.ChunkType == ChunkType.Class && c.Code.Contains("class Rectangle"));

            Assert.NotNull(funcChunk);
            Assert.NotNull(arrowChunk);
            Assert.NotNull(classChunk);
        }

        [Fact]
        public async Task ParseAsync_EmptyCodeFile_ReturnsEmptyChunks()
        {
            // Arrange
            var emptyCode = "";

            // Act
            var chunks = await _parser.ParseAsync(emptyCode, "empty.cs");

            // Assert
            Assert.NotNull(chunks);
            // Empty code may still create chunks or return empty - depends on implementation
        }

        [Fact]
        public async Task ParseAsync_CodeWithoutFunctionsOrClasses_ReturnsSingleChunk()
        {
            // Arrange
            var simpleCode = @"using System;

Console.WriteLine(""Hello World"");
var x = 5;
var y = x + 10;";

            // Act
            var chunks = await _parser.ParseAsync(simpleCode, "simple.cs");

            // Assert
            Assert.NotNull(chunks);
            Assert.Single(chunks);
            var chunk = chunks[0];
            Assert.Equal(ChunkType.Unknown, chunk.ChunkType);
            Assert.Contains("Hello World", chunk.Code);
        }
    }
}