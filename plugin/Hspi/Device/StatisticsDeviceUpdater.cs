using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
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

            var (list, dict) = GetCurrentDevices();
            this.allRefIds = list.ToImmutableList();
            this.devices = dict.ToImmutableDictionary();
        }

        public void Dispose()
        {
            foreach (var device in devices)
            {
                device.Value.Dispose();
            }
        }

        public bool HasRefId(int refId)
        {
            return allRefIds.Contains(refId);
        }

        public bool UpdateData(int refId)
        {
            if (devices.TryGetValue(refId, out var data))
            {
                data.UpdateNow();
                return true;
            }

            return false;
        }

        private (List<int>, Dictionary<int, StatisticsDevice>) GetCurrentDevices()
        {
            var refIds = HS.GetRefsByInterface(PlugInData.PlugInId);

            var currentChildDevices = new Dictionary<int, StatisticsDevice>();

            foreach (var refId in refIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    ERelationship relationship = (ERelationship)HS.GetPropertyByRef(refId, EProperty.Relationship);

                    //data is stored in feature(child)
                    if (relationship == ERelationship.Feature)
                    {
                        var importDevice = new StatisticsDevice(HS, collector, refId, systemClock, hsFeatureCachedDataProvider, cancellationToken);
                        currentChildDevices.Add(refId, importDevice);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("{id} has invalid plugin data. Load failed with {error}. Please recreate it.", refId, ex.GetFullMessage());
                }
            }

            return (refIds, currentChildDevices);
        }

        private readonly ImmutableList<int> allRefIds;
        private readonly CancellationToken cancellationToken;
        private readonly SqliteDatabaseCollector collector;
        private readonly ImmutableDictionary<int, StatisticsDevice> devices;
        private readonly IHsController HS;
        private readonly HsFeatureCachedDataProvider hsFeatureCachedDataProvider;
        private readonly ISystemClock systemClock;
    }
}