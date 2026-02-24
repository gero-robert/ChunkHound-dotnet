using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ChunkHound.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace ChunkHound.Core.Workers;

public abstract class PipelineWorker<TIn, TOut> where TIn : class where TOut : class
{
    protected readonly ILogger Log;
    protected readonly int BatchSize;

    protected PipelineWorker(ILogger log, WorkerConfig config)
    {
        Log = log;
        BatchSize = config.BatchSize;
    }

    public virtual async Task RunAsync(ChannelReader<TIn> reader, ChannelWriter<TOut> writer, CancellationToken ct)
    {
        try
        {
            await foreach (var batch in reader.ReadAllBatchesAsync(BatchSize, ct))
            {
                var processed = await ProcessBatchAsync(batch, ct);
                foreach (var p in processed)
                    await writer.WriteAsync(p, ct);
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            Log.LogError(ex, "PipelineWorker<TIn,TOut> failed");
            writer.TryComplete(ex);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    protected abstract Task<IReadOnlyList<TOut>> ProcessBatchAsync(IReadOnlyList<TIn> batch, CancellationToken ct);
}