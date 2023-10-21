﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;

#nullable enable

namespace Hspi
{
    public sealed class HsFeatureCachedDataProvider
    {
        public HsFeatureCachedDataProvider(IHsController hs)
        {
            this.homeSeerSystem = hs;
            monitoredFeatureCache = new HsFeatureCachedProperty<bool>(x => IsMonitoredFeature(x));
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
            monitoredFeatureCache.Invalidate(refId);
            featurePrecisionCache.Invalidate(refId);
        }

        public bool IsMonitoried(int refId)
        {
            return monitoredFeatureCache.Get(refId);
        }

        private static ImmutableSortedSet<string> CreateFeatureUnitsSet()
        {
            var list = new List<string>()
            {
                "Watts", "W",
                "kWh", "kW Hours",
                "Volts", "V",
                "vah",
                "F", "C", "K", "°F", "°C", "°K",
                "lux", "lx",
                "%",
                "A",
                "ppm", "ppb",
                "db", "dbm",
                "μs", "ms", "s", "min",
                "g", "kg", "mg", "uq", "oz", "lb",
            };

            return list.ToImmutableSortedSet(StringComparer.OrdinalIgnoreCase);
        }

        private int GetFeaturePrecision(int refId)
        {
            var typeInfo = GetPropertyValue<TypeInfo>(refId, EProperty.DeviceType);

            int? precision = null;
            if (typeInfo.ApiType == EApiType.Feature)
            {
                UpdateMaxPrecision(refId, ref precision);
            }

            precision ??= 3;
            return precision.Value;

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

        private string? GetFeatureUnit(int refId)
        {
            //  an ugly way to get unit, but there is no universal way to get them in HS4
            var displayStatus = GetPropertyValue<string>(refId, EProperty.DisplayedStatus);
            var match = unitExtractionRegEx.Match(displayStatus);

            if (match.Success)
            {
                var unit = match.Groups[1].Value;
                if (validUnits.Contains(unit))
                {
                    return unit;
                }
            }

            return null;
        }

        private T GetPropertyValue<T>(int refId, EProperty prop)
        {
            return (T)homeSeerSystem.GetPropertyByRef(refId, prop);
        }

        private bool IsMonitoredFeature(int refId)
        {
            bool monitored = !IsTimerOrCounter(refId) &&
                             !IsHS4RootDevice(refId) &&
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

            bool IsHS4RootDevice(int refId)
            {
                var typeInfo = GetPropertyValue<TypeInfo>(refId, EProperty.DeviceType);
                return (typeInfo.ApiType == EApiType.Device);
            }
        }

        private static readonly Regex unitExtractionRegEx = new(@"^\s*-?\d+(?:\.\d+)?\s*(.*)$",
                                                                RegexOptions.Compiled | RegexOptions.CultureInvariant,
                                                                TimeSpan.FromSeconds(5));

        private static readonly ImmutableSortedSet<string> validUnits = CreateFeatureUnitsSet();
        private readonly HsFeatureCachedProperty<int> featurePrecisionCache;
        private readonly HsFeatureCachedProperty<string?> featureUnitCache;
        private readonly IHsController homeSeerSystem;
        private readonly HsFeatureCachedProperty<bool> monitoredFeatureCache;
    }
}