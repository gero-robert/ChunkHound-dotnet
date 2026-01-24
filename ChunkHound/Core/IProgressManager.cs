using System.Threading;
using System.Threading.Tasks;

namespace ChunkHound.Core
{
    /// <summary>
    /// Interface for managing progress reporting.
    /// </summary>
    public interface IProgressManager
    {
        Task IncrementTotalAsync(int increment, CancellationToken cancellationToken = default);
        Task AdvanceAsync(int advance, CancellationToken cancellationToken = default);
        Task UpdateInfoAsync(string info, CancellationToken cancellationToken = default);
    }
}