using System;
using System.Collections.Concurrent;
using System.Threading;

#nullable enable

namespace Hspi.Database
{
    internal sealed class RecordDataProducerConsumerQueue : IDisposable
    {
        public void Add(in RecordData t)
        {
            queue.Enqueue(t);
            addedEvent.Set();
        }

        public void Dispose()
        {
            addedEvent.Dispose();
        }

        public RecordData Take(CancellationToken cancellationToken)
        {
            WaitHandle[] waitHandles = new[] { addedEvent, cancellationToken.WaitHandle };

            RecordData result;
            while (!queue.TryDequeue(out result))
            {
                WaitHandle.WaitAny(waitHandles, -1, false);
                cancellationToken.ThrowIfCancellationRequested();
            }

            return result;
        }

        private readonly AutoResetEvent addedEvent = new(false);
        private readonly ConcurrentQueue<RecordData> queue = new();
    }
}