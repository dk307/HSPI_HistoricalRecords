using System;
using System.Collections.Concurrent;
using System.Threading;

#nullable enable

namespace Hspi.Database
{
    internal sealed class ProducerConsumerQueue<T> : IDisposable
    {
        public void Add(in T t)
        {
            queue.Enqueue(t);
            addedEvent.Set();
        }

        public void Dispose()
        {
            addedEvent.Dispose();
        }

        public T Take(CancellationToken cancellationToken)
        {
            T result;
            while (!queue.TryDequeue(out result))
            {
                WaitHandle.WaitAny(new WaitHandle[] { addedEvent, cancellationToken.WaitHandle }, -1, true);
                cancellationToken.ThrowIfCancellationRequested();
            }

            return result;
        }

        private readonly AutoResetEvent addedEvent = new(false);
        private readonly ConcurrentQueue<T> queue = new();
    }
}