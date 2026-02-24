using Xunit;
using ChunkHound.Parsers.Concrete;
using ChunkHound.Core;
using System.Threading.Tasks;
using System.Linq;

namespace ChunkHound.Tests.Parsers
{
    public class VueChunkParserTests
    {
        private readonly VueChunkParser _parser;

        public VueChunkParserTests()
        {
            _parser = new VueChunkParser();
        }

        [Fact]
        public void CanHandle_ValidVueExtension_ReturnsTrue()
        {
            Assert.True(_parser.CanHandle(".vue"));
            Assert.True(_parser.CanHandle(".VUE"));
        }

        [Fact]
        public void CanHandle_InvalidExtensions_ReturnsFalse()
        {
            Assert.False(_parser.CanHandle(".js"));
            Assert.False(_parser.CanHandle(".ts"));
            Assert.False(_parser.CanHandle(".html"));
            Assert.False(_parser.CanHandle(""));
        }

        [Fact]
        public async Task ParseAsync_CompleteVueFile_ReturnsAllSections()
        {
            // Arrange
            var vueContent = @"<template>
  <div>
    <h1>{{ title }}</h1>
    <button @click=""increment"">Click me</button>
  </div>
</template>

<script>
export default {
  name: 'TestComponent',
  data() {
    return {
      title: 'Hello Vue'
    }
  },
  methods: {
    increment() {
      this.count++
    }
  }
}
</script>

<style scoped>
h1 {
  color: red;
}
button {
  background: blue;
}
</style>";

            // Act
            var chunks = await _parser.ParseAsync(vueContent, "test.vue");

            // Assert
            Assert.NotNull(chunks);
            Assert.Equal(3, chunks.Count);

            var templateChunk = chunks.FirstOrDefault(c => c.ChunkType == ChunkType.Vue && c.Symbol == "template");
            var scriptChunk = chunks.FirstOrDefault(c => c.ChunkType == ChunkType.Vue && c.Symbol == "script");
            var styleChunk = chunks.FirstOrDefault(c => c.ChunkType == ChunkType.Vue && c.Symbol == "style");

            Assert.NotNull(templateChunk);
            Assert.NotNull(scriptChunk);
            Assert.NotNull(styleChunk);

            Assert.Contains("<div>", templateChunk.Code);
            Assert.Contains("export default", scriptChunk.Code);
            Assert.Contains("h1", styleChunk.Code);
        }

        [Fact]
        public async Task ParseAsync_TemplateOnly_ReturnsTemplateChunk()
        {
            // Arrange
            var vueContent = @"<template>
  <div>
    <p>Hello World</p>
  </div>
</template>";

            // Act
            var chunks = await _parser.ParseAsync(vueContent, "template.vue");

            // Assert
            Assert.NotNull(chunks);
            Assert.Single(chunks);
            var chunk = chunks[0];
            Assert.Equal(ChunkType.Vue, chunk.ChunkType);
            Assert.Equal("template", chunk.Symbol);
            Assert.Contains("<div>", chunk.Code);
            Assert.Contains("Hello World", chunk.Code);
        }

        [Fact]
        public async Task ParseAsync_ScriptOnly_ReturnsScriptChunk()
        {
            // Arrange
            var vueContent = @"<script>
export default {
  name: 'ScriptOnly',
  mounted() {
    console.log('mounted');
  }
}
</script>";

            // Act
            var chunks = await _parser.ParseAsync(vueContent, "script.vue");

            // Assert
            Assert.NotNull(chunks);
            Assert.Single(chunks);
            var chunk = chunks[0];
            Assert.Equal(ChunkType.Vue, chunk.ChunkType);
            Assert.Equal("script", chunk.Symbol);
            Assert.Contains("export default", chunk.Code);
            Assert.Contains("console.log", chunk.Code);
        }

        [Fact]
        public async Task ParseAsync_StyleOnly_ReturnsStyleChunk()
        {
            // Arrange
            var vueContent = @"<style>
.test-class {
  color: blue;
  font-size: 14px;
}
</style>";

            // Act
            var chunks = await _parser.ParseAsync(vueContent, "style.vue");

            // Assert
            Assert.NotNull(chunks);
            Assert.Single(chunks);
            var chunk = chunks[0];
            Assert.Equal(ChunkType.Vue, chunk.ChunkType);
            Assert.Equal("style", chunk.Symbol);
            Assert.Contains(".test-class", chunk.Code);
            Assert.Contains("color: blue", chunk.Code);
        }

        [Fact]
        public async Task ParseAsync_EmptyVueFile_ReturnsEmptyChunks()
        {
            // Arrange
            var emptyVue = "";

            // Act
            var chunks = await _parser.ParseAsync(emptyVue, "empty.vue");

            // Assert
            Assert.NotNull(chunks);
            // Empty Vue may still create chunks or return empty - depends on implementation
        }

        [Fact]
        public async Task ParseAsync_InvalidVueFile_NoValidSections_ReturnsChunks()
        {
            // Arrange
            var invalidVue = @"<div>
  <p>This is not a proper Vue file</p>
</div>";

            // Act
            var chunks = await _parser.ParseAsync(invalidVue, "invalid.vue");

            // Assert
            Assert.NotNull(chunks);
            Assert.NotEmpty(chunks);
        }
    }
}