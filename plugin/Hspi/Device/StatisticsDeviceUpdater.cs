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
            this.HS = hs;
            this.collector = collector;
            this.systemClock = systemClock;
            this.hsFeatureCachedDataProvider = hsFeatureCachedDataProvider;
            this.cancellationToken = cancellationToken;
            this.deviceAndFeatures = GetCurrentDevices();
        }

        public void Dispose()
        {
            foreach (var device in deviceAndFeatures.Values.SelectMany(x => x))
            {
                device.Dispose();
            }
        }

        public bool HasRefId(int refId)
        {
            return deviceAndFeatures.ContainsKey(refId) ||
                   deviceAndFeatures.Values.SelectMany(x => x).Any(x => x.RefId == refId);
        }

        public bool UpdateData(int refId)
        {
            if (deviceAndFeatures.TryGetValue(refId, out var childDevices))
            {
                foreach (var entry in childDevices)
                {
                    entry.UpdateNow();
                }
                return true;
            }

            var feature = deviceAndFeatures.Values.SelectMany(x => x).FirstOrDefault(x => x.RefId == refId);
            if (feature != null)
            {
                feature.UpdateNow();
                return true;
            }

            return false;
        }

        private ImmutableDictionary<int, ImmutableList<StatisticsDevice>> GetCurrentDevices()
        {
            var refDeviceIds = HS.GetRefsByInterface(PlugInData.PlugInId, true);

            var result = new Dictionary<int, ImmutableList<StatisticsDevice>>();

            foreach (var refId in refDeviceIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var childDevices = new List<StatisticsDevice>();
                    var features = (HashSet<int>)HS.GetPropertyByRef(refId, EProperty.AssociatedDevices);

                    //data is stored in feature(child)
                    foreach (var featureRefId in features)
                    {
                        var importDevice = new StatisticsDevice(HS, collector, featureRefId, systemClock, hsFeatureCachedDataProvider, cancellationToken);
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
        private readonly SqliteDatabaseCollector collector;
        private readonly ImmutableDictionary<int, ImmutableList<StatisticsDevice>> deviceAndFeatures;
        private readonly IHsController HS;
        private readonly HsFeatureCachedDataProvider hsFeatureCachedDataProvider;
        private readonly ISystemClock systemClock;
    }
}