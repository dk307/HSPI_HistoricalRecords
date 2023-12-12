using System;
using System.Collections.Generic;
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
    internal sealed class SqliteManager : IDisposable
    {
        public SqliteManager(IHsController hs,
                             RecordDataProducerConsumerQueue queue,
                             IDBSettings settings,
                             HsFeatureCachedDataProvider hsFeatureCachedDataProvider,
                             IGlobalTimerAndClock globalTimerAndClock,
                             CancellationToken shutdownToken)
        {
            this.settings = settings;
            this.globalTimerAndClock = globalTimerAndClock;
            this.startDatabaseTimerInterval = (int)globalTimerAndClock.IntervalToRetrySqliteCollection.TotalMilliseconds;
            this.queue = queue;
            this.hs = hs;
            this.hsFeatureCachedDataProvider = hsFeatureCachedDataProvider;
            this.shutdownToken = shutdownToken;
        }

        public SqliteDatabaseCollector Collector
        {
            get
            {
                return Volatile.Read(ref this.collector) ??
                throw new InvalidOperationException("Sqlite initialize failed Or Backup in progress");
            }
        }

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

        public IDictionary<int, string> GetStatisticDeviceDataAsJson(int refId)
        {
            return statisticsDeviceUpdater?.GetDataFromFeatureAsJson(refId) ??
                        throw new HsDeviceInvalidException($"Not initialized");
        }

        public void OnDeviceDeletedInHS(int refId)
        {
            if (statisticsDeviceUpdater != null)
            {
                // currently these events are only for devices not features
                if (statisticsDeviceUpdater.HasDeviceOrFeatureRefId(refId))
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
            using var unLock = CreateStartStopLock();

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

        public void StopForBackup()
        {
            StopTimerWithWait();
            StopNow();
            startTimer = new Timer((_) => StartDatabase(), null,
                                   (int)globalTimerAndClock.TimeoutForBackup.TotalMilliseconds, startDatabaseTimerInterval);

            void StopNow()
            {
                using var unLock = CreateStartStopLock();
                StopImpl();
            }
        }

        public void TryStart()
        {
            StartDatabase();
            StopTimerWithWait();

            //check for db in startDatabaseTimerInterval again
            startTimer = new Timer((_) => StartDatabase(), null, startDatabaseTimerInterval, startDatabaseTimerInterval);
        }

        public bool TryUpdateStatisticDeviceData(int refId)
        {
            return statisticsDeviceUpdater?.UpdateData(refId) ?? false;
        }

        private Disposable CreateStartStopLock()
        {
            startStopMutex.Wait(shutdownToken);
            var unLock = Disposable.Create(() => startStopMutex.Release());
            return unLock;
        }

        private void RestartStatisticsDeviceUpdateImpl()
        {
            statisticsDeviceUpdater?.Dispose();
            statisticsDeviceUpdater = new StatisticsDeviceUpdater(hs, Collector, globalTimerAndClock, hsFeatureCachedDataProvider, shutdownToken);
        }

        private void StartDatabase()
        {
            using var unLock = CreateStartStopLock();
            try
            {
                if (!started)
                {
                    StopImpl();
                    combinedToken = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
                    if (CreateCollector())
                    {
                        RestartStatisticsDeviceUpdateImpl();
                    }

                    started = true;
                }

                startTimer?.Dispose();
            }
            catch (Exception ex) when (!ex.IsCancelException())
            {
                string errorMessage = ex.GetFullMessage();
                Log.Error("Failed to setup Sqlite db with {error}", errorMessage);
                this.collectorInitException = ex;
                started = false;
            }

            bool CreateCollector()
            {
                var collectorNew = new SqliteDatabaseCollector(settings, globalTimerAndClock, queue, combinedToken.Token);
                Interlocked.Exchange(ref collector, collectorNew);
                this.collectorInitException = null;
                return true;
            }
        }

        private void StopImpl()
        {
            started = false;
            combinedToken?.Cancel();
            statisticsDeviceUpdater?.Dispose();
            collector?.Dispose();
            statisticsDeviceUpdater = null;
            collector = null;
        }

        private void StopTimerWithWait()
        {
            if (startTimer != null)
            {
                using var waitHandle = new ManualResetEvent(false);
                if (startTimer.Dispose(waitHandle))
                {
                    waitHandle.WaitOne();
                }
            }
        }

        private readonly IGlobalTimerAndClock globalTimerAndClock;
        private readonly IHsController hs;
        private readonly HsFeatureCachedDataProvider hsFeatureCachedDataProvider;
        private readonly RecordDataProducerConsumerQueue queue;
        private readonly IDBSettings settings;
        private readonly CancellationToken shutdownToken;
        private readonly int startDatabaseTimerInterval;
        private readonly SemaphoreSlim startStopMutex = new(1, 1);
        private SqliteDatabaseCollector? collector;
        private Exception? collectorInitException;
        private CancellationTokenSource? combinedToken;
        private volatile bool started = false;
        private volatile Timer? startTimer;
        private StatisticsDeviceUpdater? statisticsDeviceUpdater;
    }
}