using System.Security.Cryptography;
using System.Text;

namespace ChunkHound.Core.Utilities;

/// <summary>
/// Utility class for computing content hashes.
/// </summary>
public static class HashUtility
{
    /// <summary>
    /// Computes a SHA256 hash of the content for deduplication.
    /// </summary>
    /// <param name="content">The content to hash.</param>
    /// <returns>The hexadecimal string representation of the hash.</returns>
    public static string ComputeContentHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}