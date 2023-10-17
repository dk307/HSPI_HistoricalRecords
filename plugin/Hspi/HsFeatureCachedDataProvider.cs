using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Nito.AsyncEx;

#nullable enable

namespace Hspi.Hspi
{
    public sealed class HsFeatureCachedDataProvider
    {
        public HsFeatureCachedDataProvider(IHsController hs)
        {
            this.homeSeerSystem = hs;
        }

        public async Task<int> GetPrecision(int refId)
        {
            if (featurePrecisionCache.TryGetValue(refId, out var value))
            {
                return value;
            }

            var typeInfo = GetPropertyValue<TypeInfo>(refId, EProperty.DeviceType);

            int? precision = null;
            if (typeInfo.ApiType == EApiType.Feature)
            {
                UpdateMaxPrecision(refId, ref precision);
            }

            precision ??= 3;
            await CacheFeaturePrecision(refId, precision.Value).ConfigureAwait(false);
            return precision.Value;

            async Task CacheFeaturePrecision(int refId, int value)
            {
                using var builderLock = await featurePrecisionCacheBuilderLock.LockAsync().ConfigureAwait(false);
                var builder = featurePrecisionCache.ToBuilder();
                builder.Add(refId, value);
                featurePrecisionCache = builder.ToImmutable();
            }

            void UpdateMaxPrecision(int refId, ref int? precision)
            {
                var graphics = GetPropertyValue<List<StatusGraphic>>(refId, EProperty.StatusGraphics);

                if (graphics != null)
                {
                    foreach (StatusGraphic graphic in graphics)
                    {
                        if (graphic.IsRange)
                        {
                            if (precision == null)
                            {
                                precision = graphic.TargetRange.DecimalPlaces;
                            }
                            else
                            {
                                precision = Math.Max(precision.Value, graphic.TargetRange.DecimalPlaces);
                            }
                        }
                    }
                }
            }
        }

        public async Task<string?> GetUnit(int refId)
        {
            if (featureUnitCache.TryGetValue(refId, out var value))
            {
                return value;
            }

            var validUnits = new List<string>()
            {
                " Watts", " W",
                " kWh", " kW Hours",
                " Volts", " V",
                " vah",
                " F", " C", " K", "°F", "°C", "°K",
                " lux", " lx",
                " %",
                " A",
                " ppm", " ppb",
                " db", " dbm",
                " μs", " ms", " s", " min",
                " g", "kg", " mg", " uq", " oz", " lb",
            };

            //  an ugly way to get unit, but there is no universal way to get them in HS4
            var displayStatus = GetPropertyValue<string>(refId, EProperty.DisplayedStatus);
            var unitFound = validUnits.Find(x => displayStatus.EndsWith(x, StringComparison.OrdinalIgnoreCase));
            var unit = unitFound?.Substring(1);

            await CacheFeatureUnit(refId, unit).ConfigureAwait(false);

            return unit;

            async Task CacheFeatureUnit(int refId, string? unit)
            {
                using var builderLock = await featureUnitCacheBuilderLock.LockAsync().ConfigureAwait(false);
                var builder = featureUnitCache.ToBuilder();
                builder.Add(refId, unit);
                featureUnitCache = builder.ToImmutable();
            }
        }

        public async Task Invalidate(int refId)
        {
            await InvalidateFeatureUnit(refId).ConfigureAwait(false);
            await InvalidateMonitored(refId).ConfigureAwait(false);
            await InvalidatePrecision(refId).ConfigureAwait(false);

            async Task InvalidateFeatureUnit(int refId)
            {
                using var builderLock = await featureUnitCacheBuilderLock.LockAsync().ConfigureAwait(false);
                var builder = featureUnitCache.ToBuilder();
                if (builder.Remove(refId))
                {
                    featureUnitCache = builder.ToImmutable();
                }
            }

            async Task InvalidateMonitored(int refId)
            {
                using var builderLock = await monitoredFeatureCacheBuilderLock.LockAsync().ConfigureAwait(false);
                var builder = monitoredFeatureCache.ToBuilder();
                if (builder.Remove(refId))
                {
                    monitoredFeatureCache = builder.ToImmutable();
                }
            }

            async Task InvalidatePrecision(int refId)
            {
                using var builderLock = await featurePrecisionCacheBuilderLock.LockAsync().ConfigureAwait(false);
                var builder = featurePrecisionCache.ToBuilder();
                if (builder.Remove(refId))
                {
                    featurePrecisionCache = builder.ToImmutable();
                }
            }
        }

        public async Task<bool> IsMonitored(int refId)
        {
            if (monitoredFeatureCache.TryGetValue(refId, out var state))
            {
                return state;
            }

            bool monitored = !IsTimerOrCounter(refId);

            // cache the value
            await UpdateCachedValue(refId, monitored).ConfigureAwait(false);

            return monitored;

            async Task UpdateCachedValue(int refId, bool monitored)
            {
                using var builderLock = await monitoredFeatureCacheBuilderLock.LockAsync().ConfigureAwait(false);
                var builder = monitoredFeatureCache.ToBuilder();
                builder.Add(refId, monitored);
                monitoredFeatureCache = builder.ToImmutable();
            }

            bool IsTimerOrCounter(int refId)
            {
                var featureInterface = GetPropertyValue<string>(refId, EProperty.Interface);
                if (string.IsNullOrEmpty(featureInterface))
                {
                    var plugExtraData = GetPropertyValue<PlugExtraData>(refId, EProperty.PlugExtraData);
                    if (plugExtraData.NamedKeys.Contains("timername") || plugExtraData.NamedKeys.Contains("countername"))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private T GetPropertyValue<T>(int refId, EProperty prop)
        {
            return (T)homeSeerSystem.GetPropertyByRef(refId, prop);
        }

        private readonly AsyncLock featurePrecisionCacheBuilderLock = new();
        private readonly AsyncLock featureUnitCacheBuilderLock = new();
        private readonly IHsController homeSeerSystem;
        private readonly AsyncLock monitoredFeatureCacheBuilderLock = new();
        private ImmutableDictionary<int, int> featurePrecisionCache = ImmutableDictionary<int, int>.Empty;
        private ImmutableDictionary<int, string?> featureUnitCache = ImmutableDictionary<int, string?>.Empty;
        private ImmutableDictionary<int, bool> monitoredFeatureCache = ImmutableDictionary<int, bool>.Empty;
    }
}