using System.Threading;
using System.Threading.Tasks;

namespace ChunkHound.Core
{
    /// <summary>
    /// Interface for processing individual files.
    /// </summary>
    public interface IFileProcessor
    {
        Task<FileProcessingResult> ProcessFileAsync(string filePath, CancellationToken cancellationToken = default);
    }
}