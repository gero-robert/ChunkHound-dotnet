using Xunit;
using ChunkHound.Core;
using ChunkHound.Core.Exceptions;
using System.Collections.Immutable;

namespace ChunkHound.Core.Tests.Models
{
    public class ChunkTests
    {
        /// <summary>
        /// Tests that the Chunk constructor throws ArgumentNullException for null id.
        /// </summary>
        [Fact]
        public void Constructor_NullId_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new Chunk(null!, 1, "content", 1, 1));
        }

        /// <summary>
        /// Tests that the Chunk constructor throws ArgumentNullException for null content.
        /// </summary>
        [Fact]
        public void Constructor_NullContent_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new Chunk("id1", 1, null!, 1, 1));
        }

        /// <summary>
        /// Tests that the Chunk constructor throws ValidationException for invalid line range.
        /// </summary>
        [Fact]
        public void Constructor_InvalidLineRange_ThrowsValidationException()
        {
            Assert.Throws<ValidationException>(() =>
                new Chunk("id1", 1, "content", 0, 1));
            Assert.Throws<ValidationException>(() =>
                new Chunk("id1", 1, "content", 2, 1));
        }

        /// <summary>
        /// Tests that With* methods create new instances not equal to the original.
        /// </summary>
        [Fact]
        public void WithMethods_CreateNewInstances()
        {
            var original = new Chunk("id1", 1, "content", 1, 1);
            var withId = original.WithId("id2");
            var withContent = original.WithContent("new content");

            Assert.NotEqual(original, withId);
            Assert.NotEqual(original, withContent);
            Assert.Equal("id2", withId.Id);
            Assert.Equal("new content", withContent.Content);
        }

        /// <summary>
        /// Tests that FromDict and ToDict roundtrip correctly for valid data.
        /// </summary>
        [Fact]
        public void FromDict_ToDict_Roundtrip()
        {
            var fixedTime = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var original = new Chunk("id1", 1, "content", 1, 2, Language.Unknown, ChunkType.Unknown, null, null,
                null,
                null, fixedTime, fixedTime);

            var dict = original.ToDict();
            var roundtrip = Chunk.FromDict(dict);

            Assert.True(original == roundtrip);
        }

        /// <summary>
        /// Tests that FromDict throws ValidationException for invalid dictionary.
        /// </summary>
        [Fact]
        public void FromDict_InvalidDict_ThrowsValidationException()
        {
            var invalidDict = new Dictionary<string, object>
            {
                ["id"] = "",
                ["file_id"] = "file1",
                ["content"] = "content",
                ["start_line"] = 1,
                ["end_line"] = 1
            };

            Assert.Throws<ValidationException>(() => Chunk.FromDict(invalidDict));
        }
    }
}