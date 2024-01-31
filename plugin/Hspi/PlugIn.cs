using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Types;
using Hspi.Database;
using Hspi.Graph;
using Hspi.Hspi.Utils;
using Hspi.Utils;
using Serilog;
using SQLitePCL;
using Constants = HomeSeer.PluginSdk.Constants;

#nullable enable

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public PlugIn()
            : base(PlugInData.PlugInId, PlugInData.PlugInName)
        {
        }

        public override int AccessLevel => (int)EAccessLevel.RequiresLicense;
        public override bool SupportsConfigDevice => true;
        public override bool SupportsConfigDeviceAll => true;
        public override bool SupportsConfigFeature => true;

        private SqliteDatabaseCollector Collector => sqliteManager?.Collector ?? throw new InvalidOperationException("Plugin Not Initialized");

        private CustomGraphManager CustomGraphManager
        {
            get
            {
                CheckNotNull(customGraphManager);
                return customGraphManager;
            }
        }

        private HsFeatureCachedDataProvider FeatureCachedDataProvider
        {
            get
            {
                CheckNotNull(featureCachedDataProvider);
                return featureCachedDataProvider;
            }
        }

        private SettingsPages SettingsPages
        {
            get
            {
                CheckNotNull(settingsPages);
                return settingsPages;
            }
        }

        public List<Dictionary<string, object?>> GetAllDevicesProperties()
        {
            var recordCounts = Collector.GetRecordsWithCount(int.MaxValue)
                                        .ToDictionary(x => x.Key, x => x.Value);

            List<Dictionary<string, object?>> result = [];
            foreach (var refId in HomeSeerSystem.GetAllRefs())
            {
                var (minValue, maxValue) = SettingsPages.GetDeviceRangeForValidValues(refId);

                Dictionary<string, object?> row = new()
                {
                    ["ref"] = refId,
                    ["records"] = recordCounts.TryGetValue((long)refId, out var count) ? count : 0,
                    ["monitorableType"] = FeatureCachedDataProvider.IsMonitorableTypeFeature(refId),
                    ["tracked"] = SettingsPages.IsTracked(refId),
                    ["minValue"] = minValue,
                    ["maxValue"] = maxValue,
                };
                result.Add(row);
            }

            return result;
        }

        public Dictionary<string, string> GetDatabaseStats()
        {
            try
            {
                return new Dictionary<string, string>()
                {
                    { "path", SettingsPages.DBPath },
                    { "version", raw.sqlite3_libversion().utf8_to_string() },
                    { "size", GetTotalFileSize().ToString(CultureInfo.InvariantCulture) },
                    { "retentionPeriod", ((long)SettingsPages.GlobalRetentionPeriod.TotalSeconds).ToString(CultureInfo.InvariantCulture) },
                };
            }
            catch (Exception ex)
            {
                Log.Warning("GetDatabaseStats for failed with {error}", ex.GetFullMessage());
                throw;
            }

            long GetTotalFileSize()
            {
                return GetFileSizeIfExists(SettingsPages.DBPath) +
                       GetFileSizeIfExists(SettingsPages.DBPath + "-shm") +
                       GetFileSizeIfExists(SettingsPages.DBPath + "-wal");
            }

            long GetFileSizeIfExists(string dBPath)
            {
                try
                {
                    var info = new FileInfo(dBPath);
                    return info.Length;
                }
                catch (IOException)
                {
                    return 0;
                }
            }
        }

        public override string GetJuiDeviceConfigPage(int devOrFeatRef)
        {
            try
            {
                string iFrameName = IsThisPlugInFeature(devOrFeatRef) ? "editdevice.html" : "history.html";
                return CreateTrackedDeviceConfigPage(devOrFeatRef, iFrameName);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to create page for {deviceRef} with error:{error}", devOrFeatRef, ex.GetFullMessage());
                var page = PageFactory.CreateDeviceConfigPage(PlugInData.PlugInId, PlugInData.PlugInName);
                page = page.WithView(new LabelView("exception", string.Empty, ex.GetFullMessage())
                {
                    LabelType = HomeSeer.Jui.Types.ELabelType.Preformatted
                });
                return page.Page.ToJsonString();
            }
        }

        public override bool HasJuiDeviceConfigPage(int devOrFeatRef)
        {
            bool hasPage = FeatureCachedDataProvider.IsMonitorableTypeFeature(devOrFeatRef) || IsThisPlugInFeature(devOrFeatRef);
            return hasPage;
        }

        public override void HsEvent(Constants.HSEvent eventType, object[] @params)
        {
            try
            {
                HSEventImpl(eventType, @params);
            }
            catch (Exception ex)
            {
                if (!ex.IsCancelException() && @params.Length > 0)
                {
                    Log.Warning("Error in event {type} with {error}", @params[0], ex.GetFullMessage());
                }
            }
        }

        public override EPollResponse UpdateStatusNow(int devOrFeatRef)
        {
            try
            {
                return this.UpdateStatisticsFeature(devOrFeatRef) ? EPollResponse.Ok : EPollResponse.NotFound;
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to update statistics device {deviceOrFeature} with {error}", devOrFeatRef, ex.GetFullMessage());
                return EPollResponse.UnknownError;
            }
        }

        internal void PruneDatabase() => Collector.DoMaintainance();

        protected override void BeforeReturnStatus()
        {
            var exceptions = new Exception?[] { topLevelException, sqliteManager?.Status };

            foreach (var exception in exceptions)
            {
                if (exception != null)
                {
                    this.Status = new PluginStatus(PluginStatus.EPluginStatus.Critical, exception.GetFullMessage());
                    return;
                }
            }

            this.Status = PluginStatus.Ok();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                sqliteManager?.Dispose();
                queue?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void Initialize()
        {
            try
            {
                Log.Information("Plugin Starting");
                featureCachedDataProvider = new HsFeatureCachedDataProvider(HomeSeerSystem);
                Settings.Add(SettingsPages.CreateDefault());
                LoadSettingsFromIni();
                settingsPages = new SettingsPages(HomeSeerSystem, Settings);
                UpdateDebugLevel();

                string graphsJsonPath = Path.Combine(HomeSeerSystem.GetAppPath(), "data", PlugInData.PlugInId, "graphs.json");
                customGraphManager = new CustomGraphManager(graphsJsonPath);

                CheckMonoVersion();

                sqliteManager = new SqliteManager(HomeSeerSystem, queue, settingsPages, featureCachedDataProvider, CreateClock(), ShutdownCancellationToken);
                sqliteManager.TryStart();

                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.VALUE_CHANGE, PlugInData.PlugInId);
                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.STRING_CHANGE, PlugInData.PlugInId);
                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.CONFIG_CHANGE, PlugInData.PlugInId);
                HomeSeerSystem.RegisterEventCB(BackupEvent, PlugInData.PlugInId);
                HomeSeerSystem.RegisterFeaturePage(this.Id, "customgraphs.html", "Graphs");
                HomeSeerSystem.RegisterDeviceIncPage(this.Id, "adddevice.html", "Add a statistics device");
                HomeSeerSystem.RegisterFeaturePage(this.Id, "alldevices.html", "Device statistics");
                HomeSeerSystem.RegisterFeaturePage(this.Id, "dbstats.html", "Database statistics");

                new Thread(RecordAllDevices).Start();
                Log.Information("Plugin Started");
            }
            catch (Exception ex)
            {
                Log.Error("Failed to initialize PlugIn with {error}", ex.GetFullMessage());
                topLevelException = ex;
            }
        }

        protected override bool OnSettingChange(string pageId, AbstractView currentView, AbstractView changedView)
        {
            Log.Information("Page:{pageId} has changed value of id:{id} to {value}", pageId, changedView.Id, changedView.GetStringValue());

            if (SettingsPages.OnSettingChange(changedView))
            {
                UpdateDebugLevel();
                return true;
            }

            return base.OnSettingChange(pageId, currentView, changedView);
        }

        protected override void OnShutdown()
        {
            Log.Information("Shutting down");
            base.OnShutdown();
        }

        private static void CheckMonoVersion()
        {
            var monoVersion = MonoHelper.GetMonoVersion();
            if (monoVersion != null)
            {
                Log.Debug("Mono version is {version}", monoVersion);
                Version minVersion = new(6, 0, 0);
                if (monoVersion < minVersion)
                {
                    throw new Exception($"Mono Version is less than {minVersion}. Need {minVersion} or higher;");
                }
            }
        }

        private static void CheckNotNull([NotNull] object? obj)
        {
            if (obj is null)
            {
                throw new InvalidOperationException("Plugin Not Initialized");
            }
        }

        private void HSEventImpl(Constants.HSEvent eventType, object[] parameters)
        {
            switch (eventType)
            {
                case Constants.HSEvent.VALUE_CHANGE when parameters.Length > 4:
                    {
                        int deviceRefId = ConvertToInt32(4);
                        RecordDeviceValue(deviceRefId);
                        break;
                    }

                case Constants.HSEvent.STRING_CHANGE when parameters.Length > 3:
                    {
                        int deviceRefId = ConvertToInt32(3);
                        RecordDeviceValue(deviceRefId);
                        break;
                    }

                case Constants.HSEvent.CONFIG_CHANGE when parameters.Length > 4 && ConvertToInt32(1) == 0:
                    HandleConfigChange();
                    break;

                case BackupEvent when parameters.Length > 1:
                    HandleBackupStartStop();
                    break;
            }

            int ConvertToInt32(int index)
            {
                return Convert.ToInt32(parameters[index], CultureInfo.InvariantCulture);
            }

            void HandleConfigChange()
            {
                int refId = ConvertToInt32(3);
                FeatureCachedDataProvider.Invalidate(refId);

                const int DeleteDevice = 2;
                if (ConvertToInt32(4) == DeleteDevice)
                {
                    sqliteManager?.OnDeviceDeletedInHS(refId);
                }
            }

            void HandleBackupStartStop()
            {
                switch (ConvertToInt32(1))
                {
                    case 1:
                        Log.Information("Back up starting. Shutting down database connection");
                        sqliteManager?.StopForBackup();
                        break;

                    case 2:
                        Log.Information("Back up finished. Starting database connection");
                        sqliteManager?.TryStart();
                        break;
                }
            }
        }

        private bool IsThisPlugInFeature(int devOrFeatRef)
        => (string)HomeSeerSystem.GetPropertyByRef(devOrFeatRef, HomeSeer.PluginSdk.Devices.EProperty.Interface) == this.Id;

        private void RecordAllDevices()
        {
            try
            {
                Log.Debug("Starting RecordAllDevices");

                var deviceEnumerator = HomeSeerSystem.GetAllRefs().ToImmutableHashSet();
                foreach (var refId in deviceEnumerator)
                {
                    RecordDeviceValue(refId);
                    ShutdownCancellationToken.ThrowIfCancellationRequested();
                }

                // remove the records for devices which were deleted
                var dbRefIds = Collector.GetRefIdsWithRecords();
                foreach (var refId in dbRefIds.Where(refId => !deviceEnumerator.Contains((int)refId)))
                {
                    Collector.DeleteAllRecordsForRef(refId);
                    ShutdownCancellationToken.ThrowIfCancellationRequested();
                }

                Log.Debug("Finished RecordAllDevices");
            }
            catch (Exception ex) when (!ex.IsCancelException())
            {
                Log.Warning("Error in RecordAllDevices with {error}", ex.GetFullMessage());
            }
        }

        private void RecordDeviceValue(int deviceRefId)
        {
            if (IsFeatureTracked(deviceRefId))
            {
                var feature = new HsFeatureData(HomeSeerSystem, deviceRefId);

                var deviceValue = feature.Value;
                var lastChange = feature.LastChange;
                var deviceString = feature.DisplayedStatus;

                bool validValue = CheckValidValue(deviceRefId, deviceValue);

                if (validValue)
                {
                    RecordData recordData = new(feature.Ref, deviceValue, deviceString, lastChange);
                    if (recordData.UnixTimeSeconds >= 0)
                    {
                        Log.Verbose("Recording {@record}", recordData);
                        this.queue.Add(recordData);
                    }
                    else
                    {
                        Log.Verbose("Not Recording {@record} as last change time is invalid", recordData);
                    }
                }
                else
                {
                    Log.Warning("Not adding {name} value to db as it has not valid value :{value}",
                                HsHelper.GetNameForLog(HomeSeerSystem, deviceRefId), deviceValue);
                }
            }
            else
            {
                Log.Verbose("Not adding {refId}'s value to db as it is not tracked", deviceRefId);
            }

            bool CheckValidValue(int deviceRefId, double deviceValue)
            {
                if (!HasValue(deviceValue))
                {
                    return false;
                }

                var (minValue, maxValue) = SettingsPages.GetDeviceRangeForValidValues(deviceRefId);

                if (minValue != null && deviceValue < minValue ||
                    maxValue != null && deviceValue > maxValue)
                {
                    return false;
                }

                return true;

                static bool HasValue(double value)
                {
                    return !double.IsNaN(value) && !double.IsInfinity(value);
                }
            }
        }

        private void UpdateDebugLevel()
        {
            bool debugLevel = SettingsPages.DebugLoggingEnabled;
            bool logToFile = SettingsPages.LogtoFileEnabled;
            this.LogDebug = debugLevel;
            Logger.ConfigureLogging(SettingsPages.LogLevel, logToFile, HomeSeerSystem);
        }

        private const Constants.HSEvent BackupEvent = (Constants.HSEvent)0x200;
        private readonly RecordDataProducerConsumerQueue queue = new();
        private CustomGraphManager? customGraphManager;
        private HsFeatureCachedDataProvider? featureCachedDataProvider;
        private SettingsPages? settingsPages;
        private SqliteManager? sqliteManager;
        private Exception? topLevelException;
    }
}