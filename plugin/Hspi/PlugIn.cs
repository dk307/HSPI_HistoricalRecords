using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Types;
using Hspi.Database;
using Hspi.Device;
using Hspi.Utils;
using Serilog;
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

        private SqliteDatabaseCollector Collector
        {
            get
            {
                CheckNotNull(collector);
                return collector;
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

            List<Dictionary<string, object?>> result = new();
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

        public IDictionary<string, string> GetDatabaseStats() => Collector.GetDatabaseStats();

        public override string GetJuiDeviceConfigPage(int devOrFeatRef)
        {
            try
            {
                string iFrameName = IsThisPlugInFeature(devOrFeatRef) ? "editdevice.html" : "devicehistoricalrecords.html";
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
                if (ex.IsCancelException())
                {
                    return;
                }

                Log.Warning("Error in recording event with {error}", ex.GetFullMessage());
            }
        }

        public void PruneDatabase() => Collector.DoMaintainance();

        protected override void BeforeReturnStatus() => this.Status = PluginStatus.Ok();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                statisticsDeviceUpdater?.Dispose();
                collector?.Dispose();
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

                collector = new SqliteDatabaseCollector(SettingsPages, CreateClock(), ShutdownCancellationToken);
                statisticsDeviceUpdater = new StatisticsDeviceUpdater(HomeSeerSystem, collector, CreateClock(), featureCachedDataProvider, ShutdownCancellationToken);

                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.VALUE_CHANGE, PlugInData.PlugInId);
                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.STRING_CHANGE, PlugInData.PlugInId);
                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.CONFIG_CHANGE, PlugInData.PlugInId);
                HomeSeerSystem.RegisterDeviceIncPage(this.Id, "adddevice.html", "Add a statistics device");
                HomeSeerSystem.RegisterFeaturePage(this.Id, "alldevices.html", "Device statistics");
                HomeSeerSystem.RegisterFeaturePage(this.Id, "dbstats.html", "Database statistics");

                new Thread(RecordAllDevices).Start();
                Log.Information("Plugin Started");
            }
            catch (Exception ex)
            {
                Log.Error("Failed to initialize PlugIn with {error}", ex.GetFullMessage());
                throw;
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

        private static void CheckNotNull([NotNull] object? obj)
        {
            if (obj is null)
            {
                throw new InvalidOperationException("Plugin Not Initialized");
            }
        }

        private void HSEventImpl(Constants.HSEvent eventType, object[] parameters)
        {
            if ((eventType == Constants.HSEvent.VALUE_CHANGE) && (parameters.Length > 4))
            {
                int deviceRefId = ConvertToInt32(4);
                RecordDeviceValue(deviceRefId);
            }
            else if ((eventType == Constants.HSEvent.STRING_CHANGE) && (parameters.Length > 3))
            {
                int deviceRefId = ConvertToInt32(3);
                RecordDeviceValue(deviceRefId);
            }
            else if ((eventType == Constants.HSEvent.CONFIG_CHANGE) && (parameters.Length > 4) && ConvertToInt32(1) == 0)
            {
                HandleConfigChange();
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
                    Log.Verbose("Recording {@record}", recordData);

                    Collector.Record(recordData);
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

                if (minValue != null && deviceValue < minValue)
                {
                    return false;
                }

                if (maxValue != null && deviceValue > maxValue)
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

        private SqliteDatabaseCollector? collector;
        private HsFeatureCachedDataProvider? featureCachedDataProvider;
        private SettingsPages? settingsPages;
    }
}