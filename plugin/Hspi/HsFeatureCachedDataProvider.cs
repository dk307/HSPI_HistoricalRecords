using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

        public async Task<int> GetPrecision(int refId)
        {
            return await featurePrecisionCache.Get(refId).ConfigureAwait(false);
        }

        public async Task<string?> GetUnit(int refId)
        {
            return await featureUnitCache.Get(refId).ConfigureAwait(false);
        }

        public async Task Invalidate(int refId)
        {
            await featureUnitCache.Invalidate(refId).ConfigureAwait(false);
            await monitoredFeatureCache.Invalidate(refId).ConfigureAwait(false);
            await featurePrecisionCache.Invalidate(refId).ConfigureAwait(false);
        }

        public async Task<bool> IsMonitored(int refId)
        {
            return await monitoredFeatureCache.Get(refId).ConfigureAwait(false);
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
                return match.Groups[1].Value;
            }

            return null;
        }

        private T GetPropertyValue<T>(int refId, EProperty prop)
        {
            return (T)homeSeerSystem.GetPropertyByRef(refId, prop);
        }

        private bool IsMonitoredFeature(int refId)
        {
            bool monitored = !IsTimerOrCounter(refId);

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
        }

        private readonly HsFeatureCachedProperty<int> featurePrecisionCache;
        private readonly HsFeatureCachedProperty<string?> featureUnitCache;
        private readonly IHsController homeSeerSystem;
        private readonly HsFeatureCachedProperty<bool> monitoredFeatureCache;
        private readonly Regex unitExtractionRegEx = new(@"^\s*-?\d+(?:\.\d+)?\s*(.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }
}