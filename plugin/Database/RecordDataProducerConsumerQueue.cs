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
            RecordData result;
            while (!queue.TryDequeue(out result))
            {
                WaitHandle.WaitAny(new WaitHandle[] { addedEvent, cancellationToken.WaitHandle }, -1, true);
                cancellationToken.ThrowIfCancellationRequested();
            }

            return result;
        }

        private readonly AutoResetEvent addedEvent = new(false);
        private readonly ConcurrentQueue<RecordData> queue = new();
    }
}