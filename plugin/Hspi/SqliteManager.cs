using System;
using System.Runtime.InteropServices;
using System.Threading;
using HomeSeer.PluginSdk;
using Hspi.Database;
using Hspi.Device;
using Hspi.Utils;
using Nito.Disposables;
using Serilog;
using SQLitePCL;

#nullable enable

namespace Hspi
{
    public sealed class SqliteManager : IDisposable
    {
        static SqliteManager()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Batteries_V2.Init();
            }
            else
            {
                SQLitePCL.raw.SetProvider(new SQLite3Provider_sqlite3());
            }
        }

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
            Volatile.Read(ref this.collector) ??
            throw new InvalidOperationException("Sqlite initialize failed Or Backup in progress");

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
                    if (started)
                    {
                        var collectorCopy = Volatile.Read(ref this.collector);
                        var collectorError = collectorCopy?.RecordUpdateException;
                        return collectorError != null ?
                                    PluginStatus.Warning(collectorError.GetFullMessage()) : PluginStatus.Ok();
                    }
                    else
                    {
                        return PluginStatus.Warning("Device records are not being stored");
                    }
                }
            }
        }

        public void Dispose()
        {
            startTimer?.Dispose();
            statisticsDeviceUpdater?.Dispose();
            collector?.Dispose();
            startStopMutex?.Dispose();
            combinedToken?.Dispose();
        }

        public void OnDeviceDeletedInHS(int refId)
        {
            if (statisticsDeviceUpdater != null)
            {
                // currently these events are only for devices not features
                if (statisticsDeviceUpdater.HasRefId(refId))
                {
                    RestartStatisticsDeviceUpdate();
                }
                else
                {
                    Collector.DeleteAllRecordsForRef(refId);
                }
            }
        }

        public bool RestartStatisticsDeviceUpdate()
        {
            startStopMutex.Wait(shutdownToken);
            using var unLock = Disposable.Create(() => startStopMutex.Release());

            if (started)
            {
                Log.Debug("Restarting statistics device update");
                RestartStatisticsDeviceUpdateImpl();
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Stop(int restartTimer = 30000)
        {
            startStopMutex.Wait(shutdownToken);
            using var unLock = Disposable.Create(() => startStopMutex.Release());
            started = false;
            StopImpl();
            startTimer = new Timer(StartTimer, null, restartTimer, Timeout.Infinite);
        }

        public bool TryStart()
        {
            startStopMutex.Wait(shutdownToken);
            using var unLock = Disposable.Create(() => startStopMutex.Release());

            if (!started)
            {
                startTimer?.Dispose();
                StopImpl();
                combinedToken = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
                if (CreateCollector())
                {
                    RestartStatisticsDeviceUpdateImpl();
                }

                started = true;
            }

            return started;

            bool CreateCollector()
            {
                try
                {
                    var collectorNew = new SqliteDatabaseCollector(settings, systemClock, queue, combinedToken.Token);
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
        }

        public bool TryUpdateStatisticDeviceData(int refId)
        {
            return statisticsDeviceUpdater?.UpdateData(refId) ?? false;
        }

        private void RestartStatisticsDeviceUpdateImpl()
        {
            statisticsDeviceUpdater?.Dispose();
            statisticsDeviceUpdater = new StatisticsDeviceUpdater(hs, Collector, systemClock, hsFeatureCachedDataProvider, shutdownToken);
        }

        private void StartTimer(object state) => TryStart();

        private void StopImpl()
        {
            combinedToken?.Cancel();
            statisticsDeviceUpdater?.Dispose();
            collector?.Dispose();
            statisticsDeviceUpdater = null;
            collector = null;
        }

        public string GetStatisticDeviceDataAsJson(int refId)
        {
            return statisticsDeviceUpdater?.GetDataFromFeatureAsJson(refId) ??
                    throw new HsDeviceInvalidException($"Not initialized");
        }

        private readonly IHsController hs;
        private readonly HsFeatureCachedDataProvider hsFeatureCachedDataProvider;
        private readonly RecordDataProducerConsumerQueue queue;
        private readonly IDBSettings settings;
        private readonly CancellationToken shutdownToken;
        private readonly SemaphoreSlim startStopMutex = new(1, 1);
        private readonly ISystemClock systemClock;
        private SqliteDatabaseCollector? collector;
        private Exception? collectorInitException;
        private CancellationTokenSource? combinedToken;
        private volatile bool started = false;
        private Timer? startTimer;
        private StatisticsDeviceUpdater? statisticsDeviceUpdater;
    }
}