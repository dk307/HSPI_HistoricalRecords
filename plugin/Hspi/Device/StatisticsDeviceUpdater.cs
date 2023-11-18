using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi.Database;
using Hspi.Utils;
using Serilog;

#nullable enable

namespace Hspi.Device
{
    internal sealed class StatisticsDeviceUpdater : IDisposable
    {
        public StatisticsDeviceUpdater(IHsController hs,
                                       SqliteDatabaseCollector collector,
                                       ISystemClock systemClock,
                                       HsFeatureCachedDataProvider hsFeatureCachedDataProvider,
                                       CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            this.deviceAndFeatures = GetCurrentDevices(hs, collector, systemClock, hsFeatureCachedDataProvider);
        }

        private IEnumerable<StatisticsDevice> AllStatisticsFeatures => deviceAndFeatures.Values.SelectMany(x => x);

        public void Dispose()
        {
            foreach (var device in AllStatisticsFeatures)
            {
                device.Dispose();
            }
        }

        public string GetDataFromFeatureAsJson(int devOrFeatRefId)
        {
            if (deviceAndFeatures.TryGetValue(devOrFeatRefId, out var childDevices))
            {
                // find child
                return childDevices.Count == 1
                    ? childDevices[0].DataFromFeatureAsJson
                    : throw new HsDeviceInvalidException($"{devOrFeatRefId} has invalid number of features({childDevices.Count})");
            }

            var feature = AllStatisticsFeatures.FirstOrDefault(x => x.FeatureRefId == devOrFeatRefId);
            return feature != null
                ? feature.DataFromFeatureAsJson
                : throw new HsDeviceInvalidException($"{devOrFeatRefId} is not a plugin device or feature)");
        }

        public bool HasRefId(int devOrFeatRefId)
        {
            return deviceAndFeatures.ContainsKey(devOrFeatRefId) ||
                   AllStatisticsFeatures.Any(x => x.FeatureRefId == devOrFeatRefId);
        }

        public bool UpdateData(int devOrFeatRefId)
        {
            if (deviceAndFeatures.TryGetValue(devOrFeatRefId, out var childDevices))
            {
                foreach (var entry in childDevices)
                {
                    entry.UpdateNow();
                }
                return true;
            }

            var feature = AllStatisticsFeatures.FirstOrDefault(x => x.FeatureRefId == devOrFeatRefId);
            if (feature != null)
            {
                feature.UpdateNow();
                return true;
            }

            return false;
        }

        private ImmutableDictionary<int, ImmutableList<StatisticsDevice>> GetCurrentDevices(
            IHsController hs, SqliteDatabaseCollector collector,
                                       ISystemClock systemClock,
                                       HsFeatureCachedDataProvider hsFeatureCachedDataProvider)
        {
            var refDeviceIds = hs.GetRefsByInterface(PlugInData.PlugInId, true);

            var result = new Dictionary<int, ImmutableList<StatisticsDevice>>();

            foreach (var refId in refDeviceIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var childDevices = new List<StatisticsDevice>();
                    var features = (HashSet<int>)hs.GetPropertyByRef(refId, EProperty.AssociatedDevices);

                    //data is stored in feature(child)
                    foreach (var featureRefId in features)
                    {
                        var importDevice = new StatisticsDevice(hs, collector, featureRefId, systemClock, hsFeatureCachedDataProvider, cancellationToken);
                        childDevices.Add(importDevice);
                    }

                    result.Add(refId, childDevices.ToImmutableList());
                }
                catch (Exception ex)
                {
                    Log.Warning("{id} has invalid plugin data. Load failed with {error}. Please recreate it.", refId, ex.GetFullMessage());
                }
            }

            return result.ToImmutableDictionary();
        }

        private readonly CancellationToken cancellationToken;
        private readonly ImmutableDictionary<int, ImmutableList<StatisticsDevice>> deviceAndFeatures;
    }
}