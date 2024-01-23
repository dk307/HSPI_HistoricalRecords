using System;
using System.Collections.Immutable;

#nullable enable

namespace Hspi
{
    internal sealed class HsFeatureCachedProperty<T>(Func<int, T> propertyGetter)
    {
        public T Get(int refId)
        {
            return ImmutableInterlocked.GetOrAdd(ref cache, refId, propertyGetter);
        }

        public void Invalidate(int refId)
        {
            ImmutableInterlocked.TryRemove(ref cache, refId, out var _);
        }

        private readonly Func<int, T> propertyGetter = propertyGetter;
        private ImmutableDictionary<int, T> cache = ImmutableDictionary<int, T>.Empty;
    }
}