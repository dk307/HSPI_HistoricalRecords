﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;

#nullable enable

namespace Hspi
{
    internal sealed class HsFeatureCachedDataProvider
    {
        public HsFeatureCachedDataProvider(IHsController hs)
        {
            this.homeSeerSystem = hs;
            monitorableTypeFeatureCache = new HsFeatureCachedProperty<bool>(x => IsMonitorableTypeFeature2(x));
            featureUnitCache = new HsFeatureCachedProperty<string?>(x => GetFeatureUnit(x));
            featurePrecisionCache = new HsFeatureCachedProperty<int>(x => GetFeaturePrecision(x));
        }

        public int GetPrecision(int refId)
        {
            return featurePrecisionCache.Get(refId);
        }

        public string? GetUnit(int refId)
        {
            return featureUnitCache.Get(refId);
        }

        public void Invalidate(int refId)
        {
            featureUnitCache.Invalidate(refId);
            monitorableTypeFeatureCache.Invalidate(refId);
            featurePrecisionCache.Invalidate(refId);
        }

        public bool IsMonitorableTypeFeature(int refId)
        {
            return monitorableTypeFeatureCache.Get(refId);
        }

        private static ImmutableSortedSet<string> CreateFeatureUnitsSet()
        {
            var list = new List<string>()
            {
                "Watts", "W", "kW",
                "kWh", "kW Hours",
                "Volts", "V",
                "vah",
                "F", "C", "K", "°F", "°C", "°K",
                "lux", "lx",
                "Hz", "kHz", "MHz", "GHz",
                "m³", "ft³", "CCF",
                "%",
                "A", "Amp", "mA",
                "ppm", "ppb",
                "db", "dbm", "dBA",
                "μs", "ms", "s", "min", "hr",
                "g", "kg", "mg", "uq", "oz", "lb",
                "µg/m³",
            };

            return list.ToImmutableSortedSet(StringComparer.OrdinalIgnoreCase);
        }

        private int GetFeaturePrecision(int refId)
        {
            return UpdateMaxPrecision(refId) ?? 3;

            int? UpdateMaxPrecision(int refId)
            {
                var graphics = GetPropertyValue<List<StatusGraphic>>(refId, EProperty.StatusGraphics);

                if (graphics != null)
                {
                    var decimalPlaces = graphics.Where(x => x.IsRange).Select(x => x.TargetRange.DecimalPlaces);
                    if (decimalPlaces.Any())
                    {
                        return decimalPlaces.Max();
                    }
                }

                return null;
            }
        }

        private string? GetFeatureUnit(int refId)
        {
            //  an ugly way to get unit, but there is no universal way to get them in HS4
            var displayStatus = GetPropertyValue<string>(refId, EProperty.DisplayedStatus);
            if (!string.IsNullOrWhiteSpace(displayStatus))
            {
                var match = unitExtractionRegEx.Match(displayStatus);

                if (match.Success)
                {
                    var unit = match.Groups[1].Value;
                    if (validUnits.Contains(unit))
                    {
                        return unit;
                    }
                }
            }

            return null;
        }

        private T GetPropertyValue<T>(int refId, EProperty prop) => (T)homeSeerSystem.GetPropertyByRef(refId, prop);

        private bool IsMonitorableTypeFeature2(int refId)
        {
            bool monitored = !IsTimerOrCounter(refId) &&
                             !IsSameDeviceInterface(refId);

            return monitored;

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

            bool IsSameDeviceInterface(int refId)
            {
                var featureInterface = GetPropertyValue<string>(refId, EProperty.Interface);
                return featureInterface == PlugInData.PlugInId;
            }
        }

        private static readonly Regex unitExtractionRegEx = new(@"^\s*-?\d+(?:\.\d+)?\s*(.*)$",
                                                                RegexOptions.Compiled | RegexOptions.CultureInvariant,
                                                                TimeSpan.FromSeconds(5));

        private static readonly ImmutableSortedSet<string> validUnits = CreateFeatureUnitsSet();
        private readonly HsFeatureCachedProperty<int> featurePrecisionCache;
        private readonly HsFeatureCachedProperty<string?> featureUnitCache;
        private readonly IHsController homeSeerSystem;
        private readonly HsFeatureCachedProperty<bool> monitorableTypeFeatureCache;
    }
}