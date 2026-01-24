using Xunit;
using ChunkHound.Core.Utilities;

namespace ChunkHound.Core.Tests.Utilities
{
    public class HashUtilityTests
    {
        /// <summary>
        /// Tests that HashUtility.ComputeContentHash() returns consistent hashes for identical content.
        /// This validates hash determinism and proper SHA256 implementation.
        /// </summary>
        [Fact]
        public void ComputeContentHash_ReturnsConsistentHashForSameContent()
        {
            // Arrange
            var content = "test content";

            // Act
            var hash1 = HashUtility.ComputeContentHash(content);
            var hash2 = HashUtility.ComputeContentHash(content);

            // Assert
            Assert.Equal(hash1, hash2);
            Assert.NotEmpty(hash1);
            Assert.True(hash1.Length == 64); // SHA256 produces 64 character hex string
        }

        /// <summary>
        /// Tests that HashUtility.ComputeContentHash() produces different hashes for different content.
        /// This validates hash uniqueness and collision resistance for content identification.
        /// </summary>
        [Fact]
        public void ComputeContentHash_ReturnsDifferentHashesForDifferentContent()
        {
            // Arrange
            var content1 = "test content 1";
            var content2 = "test content 2";

            // Act
            var hash1 = HashUtility.ComputeContentHash(content1);
            var hash2 = HashUtility.ComputeContentHash(content2);

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that HashUtility.ComputeContentHash() correctly handles empty string input.
        /// This validates edge case handling and ensures consistent behavior with empty content.
        /// </summary>
        [Fact]
        public void ComputeContentHash_HandlesEmptyString()
        {
            // Arrange
            var content = string.Empty;

            // Act
            var hash = HashUtility.ComputeContentHash(content);

            // Assert
            Assert.NotEmpty(hash);
            Assert.True(hash.Length == 64);
            // SHA256 of empty string should be a known value
            Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
        }

        /// <summary>
        /// Tests that HashUtility.ComputeContentHash() properly handles Unicode characters and emojis.
        /// This validates UTF-8 encoding support and international content compatibility.
        /// </summary>
        [Fact]
        public void ComputeContentHash_HandlesUnicodeContent()
        {
            // Arrange
            var content = "Hello, 世界! 🌍";

            // Act
            var hash = HashUtility.ComputeContentHash(content);

            // Assert
            Assert.NotEmpty(hash);
            Assert.True(hash.Length == 64);
            Assert.True(hash.All(c => char.IsLower(c) || char.IsDigit(c))); // Should be lowercase hex
        }

        /// <summary>
        /// Tests that HashUtility.ComputeContentHash() returns expected SHA256 hash values for known inputs.
        /// This validates the correctness of the hash implementation against known test vectors.
        /// </summary>
        [Theory]
        [InlineData("a", "ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb")]
        [InlineData("hello", "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824")]
        public void ComputeContentHash_ReturnsExpectedHash(string content, string expectedHash)
        {
            // Act
            var hash = HashUtility.ComputeContentHash(content);

            // Assert
            Assert.Equal(expectedHash, hash);
        }
    }
}