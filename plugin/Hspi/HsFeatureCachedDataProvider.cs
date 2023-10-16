using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;

#nullable enable

namespace Hspi.Hspi
{
    public sealed class HsFeatureCachedDataProvider
    {
        public HsFeatureCachedDataProvider(IHsController hs)
        {
            this.homeSeerSystem = hs;
        }

        public string? GetFeatureUnit(int refId)
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

            CacheFeatureUnit(refId, unit);

            return unit;

            void CacheFeatureUnit(int refId, string? unit)
            {
                var builder = featureUnitCache.ToBuilder();
                builder.Add(refId, unit);
                featureUnitCache = builder.ToImmutable();
            }
        }

        public void Invalidate(int refId)
        {
            InvalidateFeatureUnit(refId);
            InvalidateMonitored(refId);

            void InvalidateFeatureUnit(int refId)
            {
                var builder = featureUnitCache.ToBuilder();
                builder.Remove(refId);
                featureUnitCache = builder.ToImmutable();
            }

            void InvalidateMonitored(int refId)
            {
                var builder = monitoredFeatureCache.ToBuilder();
                builder.Remove(refId);
                monitoredFeatureCache = builder.ToImmutable();
            }
        }

        public bool IsMonitored(int refId)
        {
            if (monitoredFeatureCache.TryGetValue(refId, out var state))
            {
                return state;
            }

            bool monitored = IsTimerOrCounter(refId);

            // cache the value
            UpdateCachedValue(refId, monitored);

            return monitored;

            void UpdateCachedValue(int refId, bool monitored)
            {
                var builder = monitoredFeatureCache.ToBuilder();
                builder.Add(refId, monitored);
                monitoredFeatureCache = builder.ToImmutable();
            }

            bool IsTimerOrCounter(int refId)
            {
                var plugExtraData = GetPropertyValue<PlugExtraData>(refId, EProperty.PlugExtraData);
                if (plugExtraData.NamedKeys.Contains("timername") || plugExtraData.NamedKeys.Contains("countername"))
                {
                    return false;
                }

                return true;
            }
        }

        private T GetPropertyValue<T>(int refId, EProperty prop)
        {
            return (T)homeSeerSystem.GetPropertyByRef(refId, prop);
        }

        private readonly IHsController homeSeerSystem;
        private ImmutableDictionary<int, string?> featureUnitCache = ImmutableDictionary<int, string?>.Empty;
        private ImmutableDictionary<int, bool> monitoredFeatureCache = ImmutableDictionary<int, bool>.Empty;
    }
}