using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChunkHound.Core
{
    /// <summary>
    /// Interface for handling errors and determining retry/abort conditions.
    /// </summary>
    public interface IErrorHandler
    {
        Task TrackErrorAsync(string error, bool isPermanent, CancellationToken cancellationToken = default);
        bool ShouldAbort();
        Dictionary<string, int> GetErrorStats();
    }
}