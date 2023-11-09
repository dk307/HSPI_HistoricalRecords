using System;
using System.Threading;
using HomeSeer.PluginSdk;
using Hspi.Database;
using Hspi.Device;
using Hspi.Utils;
using Serilog;

#nullable enable

namespace Hspi
{
    public sealed class SqliteManager : IDisposable
    {
        public SqliteManager(IHsController hs,
                             RecordDataProducerConsumerQueue queue,
                             IDBSettings settings,
                             HsFeatureCachedDataProvider hsFeatureCachedDataProvider,
                             ISystemClock systemClock,
                             CancellationToken shutdownToken)
        {
            this.settings = settings;
            this.systemClock = systemClock;
            this.queue = queue;
            this.hs = hs;
            this.hsFeatureCachedDataProvider = hsFeatureCachedDataProvider;
            this.shutdownToken = shutdownToken;
        }

        public SqliteDatabaseCollector Collector =>
            Volatile.Read(ref this.collector) ?? throw new InvalidOperationException("Sqlite Not Initialized");

        public PluginStatus Status
        {
            get
            {
                if (collectorInitException != null)
                {
                    return PluginStatus.Critical(this.collectorInitException.GetFullMessage());
                }
                else
                {
                    var collectorCopy = Volatile.Read(ref this.collector);
                    var collectorError = collectorCopy?.RecordUpdateException;
                    if (collectorError != null)
                    {
                        return PluginStatus.Warning(collectorError.GetFullMessage());
                    }
                    else
                    {
                        return PluginStatus.Ok();
                    }
                }
            }
        }

        public void Dispose()
        {
            statisticsDeviceUpdater?.Dispose();
            collector?.Dispose();
        }

        public void OnDeviceDeletedInHS(int refId)
        {
            // currently these events are only for devices not features
            if ((statisticsDeviceUpdater?.HasRefId(refId) ?? false))
            {
                RestartStatisticsDeviceUpdate();
            }
            else
            {
                Collector.DeleteAllRecordsForRef(refId);
            }
        }

        public void RestartStatisticsDeviceUpdate()
        {
            Log.Debug("Restarting statistics device update");
            statisticsDeviceUpdater?.Dispose();
            statisticsDeviceUpdater = new StatisticsDeviceUpdater(hs, Collector, systemClock, hsFeatureCachedDataProvider, shutdownToken);
        }

        public void Start()
        {
            if (CreateCollector())
            {
                RestartStatisticsDeviceUpdate();
            }
        }

        public bool UpdateStatisticDeviceData(int refId)
        {
            return statisticsDeviceUpdater?.UpdateData(refId) ?? false;
        }

        //public void Stop()
        //{
        //    statisticsDeviceUpdater?.Dispose();
        //    collector?.Dispose();
        //    statisticsDeviceUpdater = null;
        //    collector = null;
        //}
        private bool CreateCollector()
        {
            try
            {
                var collectorNew = new SqliteDatabaseCollector(settings, systemClock, queue, shutdownToken);
                Interlocked.Exchange(ref collector, collectorNew);
                this.collectorInitException = null;
                return true;
            }
            catch (Exception ex) when (!ex.IsCancelException())
            {
                string errorMessage = ex.GetFullMessage();
                Log.Error("Failed to setup Sqlite db with {error}", errorMessage);
                this.collectorInitException = ex;
                return false;
            }
        }

        private readonly IHsController hs;
        private readonly HsFeatureCachedDataProvider hsFeatureCachedDataProvider;
        private readonly RecordDataProducerConsumerQueue queue;
        private readonly IDBSettings settings;
        private readonly CancellationToken shutdownToken;
        private readonly ISystemClock systemClock;
        private SqliteDatabaseCollector? collector;
        private Exception? collectorInitException;
        private StatisticsDeviceUpdater? statisticsDeviceUpdater;
    }
}