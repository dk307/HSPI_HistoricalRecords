using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Nito.AsyncEx;

#nullable enable

namespace Hspi
{
    public sealed class HsFeatureCachedProperty<T>
    {
        public HsFeatureCachedProperty(Func<int, T> propertyGetter)
        {
            this.propertyGetter = propertyGetter;
        }

        public async Task<T> Get(int refId)
        {
            if (cache.TryGetValue(refId, out var value))
            {
                return value;
            }

            var devicePropertyValue = propertyGetter(refId);

            await Update(refId, devicePropertyValue).ConfigureAwait(false);
            return devicePropertyValue;
        }

        public async Task Invalidate(int refId)
        {
            using var builderLock = await cacheLock.LockAsync().ConfigureAwait(false);
            var builder = cache.ToBuilder();
            if (builder.Remove(refId))
            {
                cache = builder.ToImmutable();
            }
        }

        private async Task Update(int refId, T unit)
        {
            using var builderLock = await cacheLock.LockAsync().ConfigureAwait(false);
            var builder = cache.ToBuilder();
            builder.Add(refId, unit);
            cache = builder.ToImmutable();
        }

        private readonly AsyncLock cacheLock = new();
        private readonly Func<int, T> propertyGetter;
        private ImmutableDictionary<int, T> cache = ImmutableDictionary<int, T>.Empty;
    }
}