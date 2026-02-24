using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ChunkHound.Core.Utilities;

public static class ChannelReaderExtensions
{
    public static async IAsyncEnumerable<IReadOnlyList<T>> ReadAllBatchesAsync<T>(this ChannelReader<T> reader, int batchSize, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var batch = new List<T>(batchSize);
        await foreach(var item in reader.ReadAllAsync(ct))
        {
            batch.Add(item);
            if(batch.Count == batchSize)
            {
                yield return batch.AsReadOnly();
                batch.Clear();
            }
        }
        if(batch.Count > 0) yield return batch.AsReadOnly();
    }
}